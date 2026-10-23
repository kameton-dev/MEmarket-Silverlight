using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Windows.Storage;
using ICSharpCode.SharpZipLib.Zip;

namespace MEmarket.Services
{
    public class InstallResult
    {
        public bool IsSuccess { get; set; }
        public string ErrorCode { get; set; }
        public string ErrorDescription { get; set; }
        public string CleanLog { get; set; }
    }

    public class MEinstaller
    {
        private const string PublicDir = @"C:\Data\Users\Public\Documents";
        private const string ProgramsDir = @"C:\Data\Programs";
        private const string PublicErrorLogPath = PublicDir + @"\MEmarket_Error_Log.txt";
        private const string TelnetHost = "127.0.0.1";
        private const int TelnetPort = 23;
        private const int StepTimeoutSeconds = 60;
        private const string SuccessMarker = "Completed Successfully.";

        private StorageFolder _localDataFolder;
        private CancellationTokenSource _cts;
        private readonly StringBuilder _sessionLog = new StringBuilder();
        private int _busy;

        private class AppxInfo
        {
            public StorageFile File { get; set; }
            public string ProductId { get; set; }
        }

        /// <summary>
        /// Внимание: события вызываются из фоновых потоков (в т.ч. из потока чтения telnet).
        /// Подписчик должен сам переключаться на UI-поток.
        /// </summary>
        public event Action<string> LogUpdated;
        public event Action<bool> ProgressStateChanged;

        // ------------------------------------------------------------------
        // Публичный API
        // ------------------------------------------------------------------

        public async Task<InstallResult> InstallAppAsync(StorageFile packageFile, bool installToSdCard = false, bool uninstallFirst = true)
        {
            if (Interlocked.CompareExchange(ref _busy, 1, 0) != 0)
            {
                return new InstallResult { IsSuccess = false, ErrorDescription = "Установка уже выполняется." };
            }

            ProgressStateChanged?.Invoke(true);

            var cts = new CancellationTokenSource();
            _cts = cts;
            lock (_sessionLog) { _sessionLog.Clear(); }

            var result = new InstallResult { IsSuccess = false };
            var tempLocalFiles = new List<string>(); // имена файлов в _localDataFolder
            TelnetClient telnet = null;
            string id = Guid.NewGuid().ToString("N").Substring(0, 8);

            try
            {
                if (_localDataFolder == null)
                {
                    _localDataFolder = await ApplicationData.Current.LocalFolder.CreateFolderAsync(
                        "XapInstallersTemp",
                        CreationCollisionOption.OpenIfExists);
                }

                telnet = CreateTelnet(cts.Token);
                await RunInstallAsync(packageFile, installToSdCard, uninstallFirst, id, telnet, tempLocalFiles, result, cts.Token);
            }
            catch (OperationCanceledException)
            {
                WriteLog("Установка отменена.");
                result.IsSuccess = false;
                result.ErrorDescription = "Установка отменена.";
                result.CleanLog = BuildCleanLog(null);
            }
            catch (Exception ex)
            {
                WriteLog("Произошел сбой: " + ex.Message);
                result.IsSuccess = false;
                result.ErrorDescription = ex.Message;
                result.CleanLog = BuildCleanLog(null);
            }
            finally
            {
                telnet?.Dispose();

                foreach (string name in tempLocalFiles)
                {
                    await SafeDeleteFileAsync(name);
                }

                _cts = null;
                cts.Dispose();
                Interlocked.Exchange(ref _busy, 0);
                ProgressStateChanged?.Invoke(false);
            }

            return result;
        }

        public void Cancel()
        {
            try { _cts?.Cancel(); }
            catch (ObjectDisposedException) { }
        }

        // ------------------------------------------------------------------
        // Основной сценарий
        // ------------------------------------------------------------------

        private async Task RunInstallAsync(
            StorageFile package, bool installToSdCard, bool uninstallFirst,
            string id, TelnetClient telnet, List<string> tempLocalFiles,
            InstallResult result, CancellationToken ct)
        {
            string ext = Path.GetExtension(package.Name).ToLowerInvariant();
            if (string.IsNullOrEmpty(ext)) ext = ".xap";
            string deployParam = installToSdCard ? "-dis" : "-di";

            var outputs = new StringBuilder(); // содержимое всех output-файлов для итогового лога
            string failReason = null;
            string lastDeployLog = "";

            // 0. Копируем пакет в нашу папку под ASCII-именем (GUID).
            //    Так в bat не попадают пользовательские имена (кириллица, %, & и т.п.)
            WriteLog("Подготовка пакета...");
            StorageFile staged = await package.CopyAsync(_localDataFolder, id + ext, NameCollisionOption.ReplaceExisting);
            string stagedPath = TrackLocal(tempLocalFiles, staged.Name);

            // 1. Telnet
            WriteLog("Подключение к локальному Telnet-серверу...");
            if (!await telnet.ConnectAsync(TelnetHost, TelnetPort))
            {
                result.IsSuccess = false;
                result.ErrorDescription = "Не удалось подключиться к Telnet-серверу (" + TelnetHost + ":" + TelnetPort + "). Проверьте, что он включён.";
                result.CleanLog = BuildCleanLog(null);
                return;
            }
            await Task.Delay(1000, ct);

            // ==============================================================
            // ПОПЫТКА 1: стандартный деплой (-di / -dis)
            // ==============================================================
            WriteLog("Попытка 1: стандартная установка...");

            string pub1 = $@"{PublicDir}\mei_{id}{ext}";
            string bat1 = TrackLocal(tempLocalFiles, id + "_1.bat");
            string ok1 = TrackLocal(tempLocalFiles, id + "_1.ok");
            string out1u = TrackLocal(tempLocalFiles, id + "_1u.txt");
            string out1 = TrackLocal(tempLocalFiles, id + "_1.txt");

            var script1 = new List<string>();
            script1.Add($"copy {Q(stagedPath)} {Q(pub1)}");
            if (uninstallFirst)
            {
                // Вывод удаления - в отдельный файл, чтобы его "Completed Successfully."
                // не принять за успех установки.
                script1.Add($"TH.exe {Q(pub1)} -u > {Q(out1u)} 2>&1");
                script1.Add("sleep 3");
            }
            script1.Add($"TH.exe {Q(pub1)} {deployParam} > {Q(out1)} 2>&1");
            script1.Add("set RC=%errorlevel%");               // сохраняем код СРАЗУ после TH
            script1.Add($"del /f /q /a {Q(pub1)}");
            script1.AddRange(ConfirmLines(ok1));

            string status1 = await RunScriptAsync(telnet, bat1, script1, ok1, ct);
            string log1 = ReadTextShared(out1);
            AppendOutput(outputs, "Attempt 1: uninstall", ReadTextShared(out1u));
            AppendOutput(outputs, "Attempt 1: deploy", log1);
            lastDeployLog = log1;

            if (IsStepSuccess(status1, log1))
            {
                WriteLog("Попытка 1 успешна! Приложение установлено.");
                result.IsSuccess = true;
            }
            else
            {
                failReason = DescribeFailure(status1);
                WriteLog("Стандартный метод завершился с ошибкой: " + failReason);
            }

            // ==============================================================
            // ПОПЫТКА 2: распаковка + удаление подписи + деплой манифеста
            // ==============================================================
            if (!result.IsSuccess)
            {
                if (ext == ".appx" || ext == ".appxbundle")
                {
                    WriteLog("Попытка 2: установка без подписи (Deployment Mode)...");

                    string appxPath = null;
                    string productId = null;

                    if (ext == ".appxbundle")
                    {
                        WriteLog("Обнаружен .appxbundle. Извлечение совместимого .appx пакета...");
                        AppxInfo info = await ExtractAppxFromBundleAsync(stagedPath, id, tempLocalFiles);
                        if (info != null)
                        {
                            WriteLog("Совместимый пакет .appx успешно извлечен из бандла.");
                            appxPath = info.File.Path;
                            productId = info.ProductId;
                        }
                        else
                        {
                            WriteLog("[Ошибка] Не удалось найти совместимый .appx пакет внутри бандла.");
                        }
                    }
                    else
                    {
                        WriteLog("Анализ манифеста APPX...");
                        appxPath = stagedPath;
                        productId = ReadProductIdFromAppx(stagedPath, false);
                    }

                    if (!string.IsNullOrEmpty(productId))
                    {
                        WriteLog($"Найден ProductID приложения: {{{productId}}}");

                        string target = $@"{ProgramsDir}\{{{productId}}}";
                        string pub2 = $@"{PublicDir}\mei_{id}_2.appx";
                        string bat2 = TrackLocal(tempLocalFiles, id + "_2.bat");
                        string ok2 = TrackLocal(tempLocalFiles, id + "_2.ok");
                        string out2pre = TrackLocal(tempLocalFiles, id + "_2pre.txt");
                        string out2 = TrackLocal(tempLocalFiles, id + "_2.txt");

                        var script2 = new List<string>();
                        // ВАЖНО: файл копируется заново. В старой версии он удалялся в конце
                        // первой попытки, и TH.exe -e получал несуществующий путь.
                        script2.Add($"copy {Q(appxPath)} {Q(pub2)}");
                        script2.Add($"TH.exe {Q(pub2)} -e {Q(target)} > {Q(out2pre)} 2>&1");
                        script2.Add("sleep 2");
                        // Листинг папки: по нему видно, что реально распаковалось
                        script2.Add($"dir {Q(target)} >> {Q(out2pre)} 2>&1");
                        script2.Add($"del /f /q /a {Q(target + @"\AppxSignature.p7x")} >> {Q(out2pre)} 2>&1");
                        script2.Add($"del /f /q /a {Q(target + @"\AppxMetadata\AppxSignature.p7x")} >> {Q(out2pre)} 2>&1");
                        script2.Add("sleep 1");
                        script2.Add($"TH.exe {Q(target + @"\AppxManifest.xml")} -de > {Q(out2)} 2>&1");
                        script2.Add("set RC=%errorlevel%");
                        script2.Add($"del /f /q /a {Q(pub2)}");
                        script2.AddRange(ConfirmLines(ok2));

                        string status2 = await RunScriptAsync(telnet, bat2, script2, ok2, ct);
                        string log2 = ReadTextShared(out2);
                        AppendOutput(outputs, "Attempt 2: extract + signature removal", ReadTextShared(out2pre));
                        AppendOutput(outputs, "Attempt 2: deploy manifest", log2);
                        lastDeployLog = log2;

                        if (IsStepSuccess(status2, log2))
                        {
                            WriteLog("Попытка 2 успешна! Приложение установлено через Deployment Mode.");
                            result.IsSuccess = true;
                        }
                        else
                        {
                            failReason = DescribeFailure(status2);
                            WriteLog("Попытка 2 также завершилась неудачей: " + failReason);
                        }
                    }
                    else
                    {
                        failReason = "Не удалось получить ProductID. Обход подписи невозможен.";
                        WriteLog(failReason);
                    }
                }
                else
                {
                    WriteLog("Deployment Mode доступен только для пакетов .appx и .appxbundle.");
                }
            }

            // ==============================================================
            // Итог
            // ==============================================================
            if (!result.IsSuccess)
            {
                ParseErrorLog(lastDeployLog, result);
                if (string.IsNullOrEmpty(result.ErrorDescription))
                {
                    result.ErrorDescription = failReason ?? "Установка не удалась.";
                }
            }

            WriteLog(result.IsSuccess ? "Установка завершена успешно." : "Установка не удалась.");
            result.CleanLog = BuildCleanLog(outputs.ToString());

            if (!result.IsSuccess)
            {
                await ExportErrorLogAsync(telnet, id, tempLocalFiles, result.CleanLog);
            }
        }

        // ------------------------------------------------------------------
        // Выполнение bat через telnet и ожидание подтверждения
        // ------------------------------------------------------------------

        private async Task<string> RunScriptAsync(TelnetClient telnet, string batPath, List<string> lines, string confirmPath, CancellationToken ct)
        {
            string script = string.Join("\r\n", lines) + "\r\n";

            StorageFile batFile = await _localDataFolder.CreateFileAsync(Path.GetFileName(batPath), CreationCollisionOption.ReplaceExisting);
            await FileIO.WriteBytesAsync(batFile, new UTF8Encoding(false).GetBytes(script));

            if (!await telnet.SendAsync($"call {Q(batPath)}"))
            {
                return "Failed: не удалось отправить команду через Telnet";
            }

            return await WaitForConfirmAsync(confirmPath, ct);
        }

        /// <summary>Возвращает текст статуса или null при таймауте.</summary>
        private static async Task<string> WaitForConfirmAsync(string path, CancellationToken ct)
        {
            for (int elapsed = 0; elapsed < StepTimeoutSeconds; elapsed++)
            {
                ct.ThrowIfCancellationRequested();

                if (File.Exists(path))
                {
                    string status = ReadTextShared(path);
                    // Файл мог быть уже создан, но ещё пуст - ждём содержимое.
                    if (!string.IsNullOrWhiteSpace(status)) return status;
                }

                await Task.Delay(1000, ct);
            }
            return null;
        }

        private static List<string> ConfirmLines(string confirmPath)
        {
            return new List<string>
            {
                "if %RC% equ 0 (",
                $"  echo Done > {Q(confirmPath)}",
                ") else (",
                $"  echo Failed %RC% > {Q(confirmPath)}",
                ")"
            };
        }

        private static bool IsStepSuccess(string status, string deployLog)
        {
            return status != null
                && status.StartsWith("Done")
                && deployLog.Contains(SuccessMarker);
        }

        private static string DescribeFailure(string status)
        {
            return status == null
                ? $"таймаут ({StepTimeoutSeconds} с)"
                : status;
        }

        // ------------------------------------------------------------------
        // APPX / APPXBUNDLE
        // ------------------------------------------------------------------

        private async Task<AppxInfo> ExtractAppxFromBundleAsync(string bundlePath, string id, List<string> tempLocalFiles)
        {
            ZipFile bundleZip = null;
            try
            {
                bundleZip = OpenZip(bundlePath);

                foreach (string entryName in GetCandidateAppxEntries(bundleZip))
                {
                    ZipEntry entry = bundleZip.GetEntry(entryName);
                    if (entry == null) continue;

                    string tempName = id + "_extracted.appx";
                    StorageFile temp = await _localDataFolder.CreateFileAsync(tempName, CreationCollisionOption.ReplaceExisting);
                    if (!tempLocalFiles.Contains(tempName)) tempLocalFiles.Add(tempName);

                    using (Stream src = bundleZip.GetInputStream(entry))
                    using (Stream dst = await temp.OpenStreamForWriteAsync())
                    {
                        dst.SetLength(0);
                        await src.CopyToAsync(dst);
                    }

                    // Нужен именно пакет приложения: с .exe и манифестом (ресурсные пакеты отсеиваются)
                    string productId = ReadProductIdFromAppx(temp.Path, true);
                    if (!string.IsNullOrEmpty(productId))
                    {
                        return new AppxInfo { File = temp, ProductId = productId };
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Ошибка обработки бандла: " + ex.Message);
            }
            finally
            {
                CloseZip(bundleZip);
            }
            return null;
        }

        /// <summary>
        /// Порядок кандидатов: сначала ARM, затем neutral (по AppxBundleManifest.xml).
        /// x86/x64 для телефона бесполезны. Если манифест бандла не удалось разобрать - все .appx подряд.
        /// </summary>
        private static List<string> GetCandidateAppxEntries(ZipFile bundleZip)
        {
            var allAppx = new List<string>();
            ZipEntry bundleManifest = null;

            foreach (ZipEntry e in bundleZip)
            {
                if (e.Name.EndsWith(".appx", StringComparison.OrdinalIgnoreCase))
                    allAppx.Add(e.Name);
                if (e.Name.EndsWith("AppxBundleManifest.xml", StringComparison.OrdinalIgnoreCase))
                    bundleManifest = e;
            }

            var ranked = new List<KeyValuePair<int, string>>();
            if (bundleManifest != null)
            {
                try
                {
                    string xml;
                    using (Stream s = bundleZip.GetInputStream(bundleManifest))
                    using (var r = new StreamReader(s))
                    {
                        xml = r.ReadToEnd();
                    }

                    var doc = XDocument.Parse(xml);
                    foreach (var p in doc.Descendants().Where(x => x.Name.LocalName == "Package"))
                    {
                        string type = (string)p.Attribute("Type");
                        if (type != null && !type.Equals("application", StringComparison.OrdinalIgnoreCase)) continue;

                        string fileName = (string)p.Attribute("FileName");
                        if (fileName == null) continue;

                        string arch = ((string)p.Attribute("Architecture") ?? "neutral").ToLowerInvariant();
                        int rank = arch == "arm" ? 0 : (arch == "neutral" ? 1 : -1);
                        if (rank < 0) continue;

                        string entryName = allAppx.FirstOrDefault(n => n.Equals(fileName, StringComparison.OrdinalIgnoreCase));
                        if (entryName != null)
                            ranked.Add(new KeyValuePair<int, string>(rank, entryName));
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine("Ошибка разбора AppxBundleManifest.xml: " + ex.Message);
                }
            }

            var ordered = ranked.OrderBy(k => k.Key).Select(k => k.Value).ToList();
            return ordered.Count > 0 ? ordered : allAppx;
        }

        /// <summary>Читает PhoneProductId из AppxManifest.xml внутри .appx. requireExe - отсеивает ресурсные пакеты.</summary>
        private static string ReadProductIdFromAppx(string appxPath, bool requireExe)
        {
            ZipFile zip = null;
            try
            {
                zip = OpenZip(appxPath);

                bool hasExe = false;
                ZipEntry manifest = null;
                foreach (ZipEntry e in zip)
                {
                    if (e.Name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) hasExe = true;
                    if (e.Name.Equals("AppxManifest.xml", StringComparison.OrdinalIgnoreCase)) manifest = e;
                }

                if (manifest == null || (requireExe && !hasExe)) return null;

                using (Stream s = zip.GetInputStream(manifest))
                using (var reader = new StreamReader(s))
                {
                    return ParseProductIdFromXml(reader.ReadToEnd());
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("[Ошибка парсинга AppxManifest.xml] " + ex.Message);
                return null;
            }
            finally
            {
                CloseZip(zip);
            }
        }

        private static string ParseProductIdFromXml(string xmlContent)
        {
            try
            {
                XDocument doc = XDocument.Parse(xmlContent);

                XNamespace mp2014 = "http://schemas.microsoft.com/appx/2014/phone/manifest";
                var phoneIdentity = doc.Descendants(mp2014 + "PhoneIdentity").FirstOrDefault();

                if (phoneIdentity == null)
                {
                    XNamespace mp2013 = "http://schemas.microsoft.com/appx/2013/phone/manifest";
                    phoneIdentity = doc.Descendants(mp2013 + "PhoneIdentity").FirstOrDefault();
                }

                var attribute = phoneIdentity?.Attribute("PhoneProductId");
                if (attribute != null) return attribute.Value;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("[Ошибка разбора XML] " + ex.Message);
            }
            return null;
        }

        private static ZipFile OpenZip(string path)
        {
            FileStream fs = File.OpenRead(path);
            try
            {
                return new ZipFile(fs);
            }
            catch
            {
                fs.Dispose();
                throw;
            }
        }

        private static void CloseZip(ZipFile zip)
        {
            if (zip == null) return;
            zip.IsStreamOwner = true;
            zip.Close();
        }

        // ------------------------------------------------------------------
        // Логи
        // ------------------------------------------------------------------

        private TelnetClient CreateTelnet(CancellationToken ct)
        {
            var telnet = new TelnetClient(TimeSpan.FromSeconds(3), ct);
            telnet.MessageSent += (s, msg) => WriteLog("[Telnet >] " + msg);
            telnet.MessageReceived += (s, msg) => WriteLog("[Telnet] " + msg);
            telnet.ErrorReceived += (s, err) => WriteLog("[Telnet Ошибка] " + err);
            return telnet;
        }

        private void WriteLog(string message)
        {
            lock (_sessionLog)
            {
                _sessionLog.AppendLine($"[{DateTime.Now:HH:mm:ss}] {message}");
            }
            LogUpdated?.Invoke(message);
        }

        private string BuildCleanLog(string extraOutputs)
        {
            string session;
            lock (_sessionLog) { session = _sessionLog.ToString(); }
            return CleanLogLines(session + "\r\n" + (extraOutputs ?? ""));
        }

        private static void AppendOutput(StringBuilder sb, string title, string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return;
            sb.AppendLine("---- " + title + " ----");
            sb.AppendLine(text);
        }

        private async Task ExportErrorLogAsync(TelnetClient telnet, string id, List<string> tempLocalFiles, string cleanLog)
        {
            try
            {
                string localLogPath = TrackLocal(tempLocalFiles, id + "_errlog.txt");
                using (var fs = new FileStream(localLogPath, FileMode.Create, FileAccess.Write, FileShare.Read))
                using (var writer = new StreamWriter(fs, new UTF8Encoding(false)))
                {
                    writer.Write(cleanLog);
                }

                await telnet.SendAsync($"copy {Q(localLogPath)} {Q(PublicErrorLogPath)}");
                WriteLog("Лог ошибок сохранен на телефон.");
            }
            catch (Exception ex)
            {
                WriteLog("Ошибка экспорта лога: " + ex.Message);
            }
        }

        private static void ParseErrorLog(string logText, InstallResult result)
        {
            if (string.IsNullOrEmpty(logText)) return;

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

        private static string CleanLogLines(string rawLog)
        {
            if (string.IsNullOrEmpty(rawLog)) return string.Empty;

            var lines = rawLog.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
            var cleanLines = lines.Where(line =>
                !line.Contains("The thread 0x") &&
                !line.Contains("exited with code") &&
                !line.Contains("The program '") &&
                !line.Contains("AgHost.exe") &&
                !string.IsNullOrWhiteSpace(line));

            return string.Join("\r\n", cleanLines);
        }

        // ------------------------------------------------------------------
        // Файловые утилиты
        // ------------------------------------------------------------------

        /// <summary>Регистрирует имя временного файла для удаления в finally и возвращает полный путь.</summary>
        private string TrackLocal(List<string> list, string fileName)
        {
            if (!list.Contains(fileName)) list.Add(fileName);
            return Path.Combine(_localDataFolder.Path, fileName);
        }

        private static string ReadTextShared(string path)
        {
            try
            {
                if (!File.Exists(path)) return "";
                using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (var reader = new StreamReader(fs, Encoding.UTF8, true))
                {
                    return reader.ReadToEnd().Trim();
                }
            }
            catch
            {
                return "";
            }
        }

        private async Task SafeDeleteFileAsync(string fileName)
        {
            try
            {
                var file = await _localDataFolder.GetFileAsync(fileName);
                await file.DeleteAsync();
            }
            catch
            {
            }
        }

        private static string Q(string s)
        {
            return "\"" + s + "\"";
        }
    }
}