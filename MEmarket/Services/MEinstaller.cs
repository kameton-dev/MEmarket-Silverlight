using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Windows.Storage;
using Windows.Storage.Streams;
using ICSharpCode.SharpZipLib.Zip;

namespace MEmarket.Services
{
    // Класс для возврата детального результата установки
    public class InstallResult
    {
        public bool IsSuccess { get; set; }
        public string ErrorCode { get; set; }
        public string ErrorDescription { get; set; }
        public string CleanLog { get; set; }
    }

    public class MEinstaller
    {
        private StorageFolder _localDataFolder;
        private CancellationTokenSource _cts;
        private TelnetClient _telnet;
        private StringBuilder _sessionLog = new StringBuilder();

        // Вспомогательный класс для хранения информации об извлеченном пакете
        private class AppxInfo
        {
            public StorageFile File { get; set; }
            public string ProductId { get; set; }
        }

        // События для вывода логов в UI
        public event Action<string> LogUpdated;
        public event Action<bool> ProgressStateChanged;

        // Вспомогательный метод для логгирования
        private void WriteLog(string message)
        {
            _sessionLog.AppendLine($"[{DateTime.Now:HH:mm:ss}] {message}");
            LogUpdated?.Invoke(message);
        }

        public async Task<InstallResult> InstallAppAsync(StorageFile xapFile, bool installToSdCard = false)
        {
            ProgressStateChanged?.Invoke(true);
            _cts = new CancellationTokenSource();
            _sessionLog.Clear();

            var finalResult = new InstallResult { IsSuccess = false };

            // Уникальные маркеры для каждого шага
            string baseName = Path.GetFileNameWithoutExtension(xapFile.Name);
            string confirmFileName1 = baseName + "Confirm1.txt";
            string confirmFileName2 = baseName + "Confirm2.txt";
            string outputFileName = baseName + "Output.txt";
            string batFileName1 = baseName + "Attempt1.bat";
            string batFileName2 = baseName + "Attempt2.bat";
            string localLogName = "MEinstaller_Error_Log.txt";
            string tempExtractedAppxName = "temp_extracted_package.appx";

            string confirmFilePath1 = "";
            string confirmFilePath2 = "";
            string outputFilePath = "";
            string batFilePath1 = "";
            string batFilePath2 = "";

            try
            {
                // Шаг 1: подготовка папок приложения
                if (_localDataFolder == null)
                {
                    _localDataFolder = await ApplicationData.Current.LocalFolder.CreateFolderAsync(
                        "XapInstallersTemp",
                        CreationCollisionOption.OpenIfExists
                    );
                }

                confirmFilePath1 = Path.Combine(_localDataFolder.Path, confirmFileName1);
                confirmFilePath2 = Path.Combine(_localDataFolder.Path, confirmFileName2);
                outputFilePath = Path.Combine(_localDataFolder.Path, outputFileName);
                batFilePath1 = Path.Combine(_localDataFolder.Path, batFileName1);
                batFilePath2 = Path.Combine(_localDataFolder.Path, batFileName2);

                string fileExtension = Path.GetExtension(xapFile.Name).ToLower();
                if (string.IsNullOrEmpty(fileExtension)) fileExtension = ".xap";

                string publicXapPath = $@"C:\Data\Users\Public\Documents\temp_install{fileExtension}";
                string deployParam = installToSdCard ? "-dis" : "-di";

                // Подключение к локальному Telnet-серверу
                _telnet = new TelnetClient(TimeSpan.FromSeconds(3), _cts.Token);
                _telnet.MessageReceived += (s, msg) => _sessionLog.AppendLine($"[Telnet] {msg}");
                _telnet.MessageReceived += (s, msg) => LogUpdated?.Invoke($"[Telnet] {msg}");
                _telnet.ErrorReceived += (s, err) => _sessionLog.AppendLine($"[Telnet Ошибка] {err}");
                _telnet.ErrorReceived += (s, err) => LogUpdated?.Invoke($"[Telnet Ошибка] {err}");

                WriteLog("Подключение к локальному Telnet-серверу...");
                _telnet.Connect("127.0.0.1", 23);
                await Task.Delay(1000);

                bool isInstalled = false;

                // ==========================================
                // ЭТАП 1: Метод "None" (Стандартный деплой -di / -dis)
                // ==========================================
                WriteLog("Попытка 1: Запуск стандартной установки (None)...");

                string batContent1 = $"copy \"{xapFile.Path}\" \"{publicXapPath}\"\r\n" +
                                     $"th \"{publicXapPath}\" -u\r\n" +
                                     "sleep 3\r\n" +
                                     $"th \"{publicXapPath}\" {deployParam} > \"{outputFilePath}\"\r\n" +
                                     $"del /f /q /a \"C:\\Data\\Users\\Public\\Documents\\temp_install.*\"\r\n" +
                                     "if %errorlevel% equ 0 (\r\n" +
                                     $"  echo Done > \"{confirmFilePath1}\"\r\n" +
                                     ") else (\r\n" +
                                     $"  echo Failed %errorlevel% > \"{confirmFilePath1}\"\r\n" +
                                     ")\r\n";

                StorageFile batFile1 = await _localDataFolder.CreateFileAsync(batFileName1, CreationCollisionOption.ReplaceExisting);
                byte[] batBytes1 = new UTF8Encoding(false).GetBytes(batContent1);
                await FileIO.WriteBytesAsync(batFile1, batBytes1);

                // Запуск первой попытки
                await Task.Run(() => _telnet.Send($"call \"{batFilePath1}\""));

                // Ожидание выполнения первого батника
                int elapsed = 0;
                while (elapsed < 60)
                {
                    if (File.Exists(confirmFilePath1))
                    {
                        string status = "";
                        using (var fs = new FileStream(confirmFilePath1, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                        using (var reader = new StreamReader(fs, Encoding.UTF8))
                        {
                            status = reader.ReadToEnd().Trim();
                        }

                        if (status.StartsWith("Done"))
                        {
                            isInstalled = true;
                        }
                        break;
                    }
                    await Task.Delay(1000);
                    elapsed++;
                }

                // Чтение лога первого этапа
                string attempt1Log = "";
                try
                {
                    StorageFile outputFile = await _localDataFolder.GetFileAsync(outputFileName);
                    attempt1Log = await FileIO.ReadTextAsync(outputFile);
                }
                catch { }

                if (isInstalled && attempt1Log.Contains("Completed Successfully."))
                {
                    WriteLog("Попытка 1 успешна! Приложение установлено.");
                    finalResult.IsSuccess = true;
                }
                else
                {
                    WriteLog("Стандартный метод завершился с ошибкой (или таймаутом).");
                    isInstalled = false;

                    // ==========================================
                    // ЭТАП 2: Метод "Deployment Mode" (Обход подписи, вернее ее попытка)
                    // TODO: Прошерстить инфу есть ли возможность устанавливать подписанные прил-я
                    // ==========================================
                    if (fileExtension == ".appx" || fileExtension == ".appxbundle")
                    {
                        WriteLog("Попытка 2: Запуск обхода подписи (Deployment Mode)...");
                        string productId = "";

                        // Если это бандл, сначала извлекаем из него совместимый .appx
                        if (fileExtension == ".appxbundle")
                        {
                            WriteLog("Обнаружен .appxbundle. Извлечение совместимого .appx пакета...");
                            var bundleInfo = await ExtractAppxFromBundleAsync(xapFile);

                            if (bundleInfo != null)
                            {
                                WriteLog("Совместимый пакет .appx успешно извлечен из бандла.");
                                xapFile = bundleInfo.File;
                                productId = bundleInfo.ProductId;
                                publicXapPath = $@"C:\Data\Users\Public\Documents\temp_install.appx";
                            }
                            else
                            {
                                WriteLog("[Ошибка] Не удалось найти совместимый .appx пакет внутри бандла.");
                            }
                        }
                        else
                        {
                            WriteLog("Анализ манифеста APPX...");
                            productId = await GetPhoneProductIdAsync(xapFile);
                        }

                        if (!string.IsNullOrEmpty(productId))
                        {
                            WriteLog($"Найден ProductID приложения: {{{productId}}}");
                            string targetProgramPath = $@"C:\Data\Programs\{{{productId}}}";

                            string batContent2 = $"TH.exe \"{publicXapPath}\" -e \"{targetProgramPath}\"\r\n" +
                                                 "sleep 2\r\n" +
                                                 $"del /f /q /a \"{targetProgramPath}\\AppxSignature.p7x\"\r\n" +
                                                 $"del /f /q /a \"{targetProgramPath}\\AppxMetadata\\AppxSignature.p7x\"\r\n" +
                                                 "sleep 1\r\n" +
                                                 $"TH.exe \"{targetProgramPath}\\AppxManifest.xml\" -de > \"{outputFilePath}\"\r\n" +
                                                 $"del /f /q /a \"C:\\Data\\Users\\Public\\Documents\\temp_install.*\"\r\n" +
                                                 "if %errorlevel% equ 0 (\r\n" +
                                                 $"  echo Done > \"{confirmFilePath2}\"\r\n" +
                                                 ") else (\r\n" +
                                                 $"  echo Failed %errorlevel% > \"{confirmFilePath2}\"\r\n" +
                                                 ")\r\n";

                            StorageFile batFile2 = await _localDataFolder.CreateFileAsync(batFileName2, CreationCollisionOption.ReplaceExisting);
                            byte[] batBytes2 = new UTF8Encoding(false).GetBytes(batContent2);
                            await FileIO.WriteBytesAsync(batFile2, batBytes2);

                            // Запуск второй попытки
                            await Task.Run(() => _telnet.Send($"call \"{batFilePath2}\""));

                            // Ожидание выполнения второго батника
                            elapsed = 0;
                            while (elapsed < 60)
                            {
                                if (File.Exists(confirmFilePath2))
                                {
                                    string status = "";
                                    using (var fs = new FileStream(confirmFilePath2, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                                    using (var reader = new StreamReader(fs, Encoding.UTF8))
                                    {
                                        status = reader.ReadToEnd().Trim();
                                    }

                                    if (status.StartsWith("Done"))
                                    {
                                        isInstalled = true;
                                    }
                                    break;
                                }
                                await Task.Delay(1000);
                                elapsed++;
                            }

                            // Читаем лог второго этапа
                            string attempt2Log = "";
                            try
                            {
                                StorageFile outputFile = await _localDataFolder.GetFileAsync(outputFileName);
                                attempt2Log = await FileIO.ReadTextAsync(outputFile);
                            }
                            catch { }

                            if (isInstalled && attempt2Log.Contains("Completed Successfully."))
                            {
                                WriteLog("Попытка 2 успешна! Приложение установлено через Deployment Mode.");
                                finalResult.IsSuccess = true;
                            }
                            else
                            {
                                WriteLog("Попытка 2 также завершилась неудачей.");
                                finalResult.IsSuccess = false;
                            }
                        }
                        else
                        {
                            WriteLog("Не удалось получить ProductID. Обход подписи невозможен.");
                        }
                    }
                    else
                    {
                        WriteLog("Обход подписи (Deployment Mode) доступен только для пакетов .appx и .appxbundle.");
                    }
                }

                // Чтение лога ошибок и его парс ДО ОЧИСТКИ ФАЙЛОВ
                string finalLogText = "";
                try
                {
                    StorageFile outputFile = await _localDataFolder.GetFileAsync(outputFileName);
                    if (outputFile != null)
                    {
                        finalLogText = await FileIO.ReadTextAsync(outputFile);
                        ParseErrorLog(finalLogText, finalResult);
                    }
                }
                catch { }

                // Чистый от лишнего лог отладки (т. е. строки только установщика)
                finalResult.CleanLog = CleanLogLines(_sessionLog.ToString() + "\r\n" + finalLogText);

                // Очистка и завершение
                if (finalResult.IsSuccess)
                {
                    WriteLog("Установка завершена успешно.");
                    await Task.Run(() => _telnet.Send($"del \"{publicXapPath}\""));
                }
                else
                {
                    WriteLog("Установка не удалась.");

                    // ОБХОД ПЕСОЧНИЦЫ:
                    try
                    {
                        // 1. Запись лога в папку приложения
                        string localLogPath = Path.Combine(_localDataFolder.Path, localLogName);
                        using (var fs = new FileStream(localLogPath, FileMode.Create, FileAccess.Write, FileShare.Read))
                        using (var writer = new StreamWriter(fs, new UTF8Encoding(false)))
                        {
                            writer.Write(finalResult.CleanLog);
                        }

                        // 2. Копирование логов из песочницы в общую папку Documents с помощью telnet
                        string publicLogPath = @"C:\Data\Users\Public\Documents\MEmarket_Error_Log.txt";
                        await Task.Run(() => _telnet.Send($"copy \"{localLogPath}\" \"{publicLogPath}\""));

                        WriteLog("Лог ошибок сохранен на телефон.");
                    }
                    catch (Exception ex)
                    {
                        WriteLog("Ошибка экспорта лога: " + ex.Message);
                    }
                    //try
                    //{
                    //    await Task.Run(() => _telnet.Send(@"del /f /q /a ""C:\Data\Users\Public\Documents\temp_install.*"""));
                    //}
                    //catch (Exception ex)
                    //{
                    //    WriteLog("Ошибка финальной очистки папки документов: " + ex.Message);
                    //}
                }

                // Очистка файлов сценария в песочнице
                await SafeDeleteFileAsync(confirmFileName1);
                await SafeDeleteFileAsync(confirmFileName2);
                await SafeDeleteFileAsync(batFileName1);
                await SafeDeleteFileAsync(batFileName2);
                await SafeDeleteFileAsync(outputFileName);
                await SafeDeleteFileAsync(localLogName);
                await SafeDeleteFileAsync(tempExtractedAppxName);

                return finalResult;
            }
            catch (Exception ex)
            {
                WriteLog("Произошел сбой: " + ex.Message);
                finalResult.IsSuccess = false;
                finalResult.ErrorDescription = ex.Message;
                finalResult.CleanLog = CleanLogLines(_sessionLog.ToString());
                return finalResult;
            }
            finally
            {
                _telnet?.Disconnect();
                _telnet?.Dispose();
                ProgressStateChanged?.Invoke(false);
            }
        }

        // Асинхронный метод парсинга AppxManifest.xml
        private async Task<string> GetPhoneProductIdAsync(StorageFile appxFile)
        {
            ZipFile file = null;
            try
            {
                FileStream fs = File.OpenRead(appxFile.Path);
                file = new ZipFile(fs);

                foreach (ZipEntry zipEntry in file)
                {
                    if (zipEntry.Name.ToLower() == "appxmanifest.xml")
                    {
                        using (Stream zipStream = file.GetInputStream(zipEntry))
                        using (StreamReader reader = new StreamReader(zipStream))
                        {
                            string xmlContent = await reader.ReadToEndAsync();
                            return ParseProductIdFromXml(xmlContent);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("[Ошибка парсинга AppxManifest.xml] " + ex.Message);
            }
            finally
            {
                if (file != null)
                {
                    file.IsStreamOwner = true;
                    file.Close();
                }
            }
            return null;
        }

        // Метод распаковки и извлечения совместимого .appx пакета из .appxbundle
        private async Task<AppxInfo> ExtractAppxFromBundleAsync(StorageFile bundleFile)
        {
            ZipFile bundleZip = null;
            try
            {
                FileStream fs = File.OpenRead(bundleFile.Path);
                bundleZip = new ZipFile(fs);

                foreach (ZipEntry bundleEntry in bundleZip)
                {
                    if (bundleEntry.Name.ToLower().EndsWith(".appx"))
                    {
                        using (Stream appxStream = bundleZip.GetInputStream(bundleEntry))
                        {
                            string tempAppxName = "temp_extracted_package.appx";
                            StorageFile tempAppxFile = await _localDataFolder.CreateFileAsync(tempAppxName, CreationCollisionOption.ReplaceExisting);

                            using (var outFs = await tempAppxFile.OpenStreamForWriteAsync())
                            {
                                outFs.SetLength(0);
                                await appxStream.CopyToAsync(outFs);
                            }

                            ZipFile appxZip = null;
                            bool isTargetAppx = false;
                            string productId = null;

                            try
                            {
                                FileStream appxFs = File.OpenRead(tempAppxFile.Path);
                                appxZip = new ZipFile(appxFs);

                                bool hasExe = false;
                                bool hasManifest = false;

                                foreach (ZipEntry appxEntry in appxZip)
                                {
                                    if (appxEntry.Name.ToLower().EndsWith(".exe"))
                                    {
                                        hasExe = true;
                                    }
                                    if (appxEntry.Name.ToLower() == "appxmanifest.xml")
                                    {
                                        hasManifest = true;
                                    }
                                }

                                if (hasExe && hasManifest)
                                {
                                    isTargetAppx = true;
                                    ZipEntry manifestEntry = appxZip.GetEntry("AppxManifest.xml");
                                    if (manifestEntry != null)
                                    {
                                        using (Stream manifestStream = appxZip.GetInputStream(manifestEntry))
                                        using (StreamReader reader = new StreamReader(manifestStream))
                                        {
                                            string xmlContent = reader.ReadToEnd();
                                            productId = ParseProductIdFromXml(xmlContent);
                                        }
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                System.Diagnostics.Debug.WriteLine("Ошибка анализа вложенного appx: " + ex.Message);
                            }
                            finally
                            {
                                if (appxZip != null)
                                {
                                    appxZip.IsStreamOwner = true;
                                    appxZip.Close();
                                }
                            }

                            if (isTargetAppx && !string.IsNullOrEmpty(productId))
                            {
                                return new AppxInfo
                                {
                                    File = tempAppxFile,
                                    ProductId = productId
                                };
                            }
                            else
                            {
                                await tempAppxFile.DeleteAsync();
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Ошибка обработки бандла: " + ex.Message);
            }
            finally
            {
                if (bundleZip != null)
                {
                    bundleZip.IsStreamOwner = true;
                    bundleZip.Close();
                }
            }
            return null;
        }

        // Вспомогательный метод парсинга XML-манифеста
        private string ParseProductIdFromXml(string xmlContent)
        {
            try
            {
                XDocument doc = XDocument.Parse(xmlContent);

                // Пространство имен Windows Phone 8.1
                XNamespace mp2014 = "http://schemas.microsoft.com/appx/2014/phone/manifest";
                var phoneIdentity = doc.Descendants(mp2014 + "PhoneIdentity").FirstOrDefault();

                if (phoneIdentity == null)
                {
                    // альтернативное пространство имен 2013 года
                    XNamespace mp2013 = "http://schemas.microsoft.com/appx/2013/phone/manifest";
                    phoneIdentity = doc.Descendants(mp2013 + "PhoneIdentity").FirstOrDefault();
                }

                if (phoneIdentity != null)
                {
                    var attribute = phoneIdentity.Attribute("PhoneProductId");
                    if (attribute != null)
                    {
                        return attribute.Value;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("[Ошибка разбора XML] " + ex.Message);
            }
            return null;
        }

        // Парсинг лога утилиты th.exe для извлечения кода ошибки
        private void ParseErrorLog(string logText, InstallResult result)
        {
            if (string.IsNullOrEmpty(logText)) return;

            // Ищем код ошибки PacMan (например, 0x800b0109)
            var codeMatch = Regex.Match(logText, @"PacMan Error Code\s*=\s*(0x[0-9a-fA-F]+)", RegexOptions.IgnoreCase);
            if (codeMatch.Success)
            {
                result.ErrorCode = codeMatch.Groups[1].Value;
            }
            else
            {
                var hrMatch = Regex.Match(logText, @"(?:hr|dwCode)\s*=\s*(0x[0-9a-fA-F]+)", RegexOptions.IgnoreCase);
                if (hrMatch.Success)
                {
                    result.ErrorCode = hrMatch.Groups[1].Value;
                }
            }

            // Поиск текстового описания ошибки
            var descMatch = Regex.Match(logText, @"PacMan Error Description\s*=\s*([^\r\n]+)", RegexOptions.IgnoreCase);
            if (descMatch.Success)
            {
                result.ErrorDescription = descMatch.Groups[1].Value.Trim();
            }
            else
            {
                var errorLineMatch = Regex.Match(logText, @"ERROR:\s*([^\r\n]+)", RegexOptions.IgnoreCase);
                if (errorLineMatch.Success)
                {
                    result.ErrorDescription = errorLineMatch.Groups[1].Value.Trim();
                }
            }
        }

        // Очистка лога от системных строк отладчика VS
        private string CleanLogLines(string rawLog)
        {
            if (string.IsNullOrEmpty(rawLog)) return string.Empty;

            var lines = rawLog.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
            var cleanLines = lines.Where(line =>
                !line.Contains("The thread 0x") &&
                !line.Contains("exited with code") &&
                !line.Contains("The program '") &&
                !line.Contains("AgHost.exe") &&
                !string.IsNullOrWhiteSpace(line)
            );

            return string.Join("\r\n", cleanLines);
        }

        private async Task SafeDeleteFileAsync(string fileName)
        {
            try
            {
                var file = await _localDataFolder.GetFileAsync(fileName);
                if (file != null)
                {
                    await file.DeleteAsync();
                }
            }
            catch
            {
            }
        }

        public void Cancel()
        {
            _cts?.Cancel();
        }
    }
}