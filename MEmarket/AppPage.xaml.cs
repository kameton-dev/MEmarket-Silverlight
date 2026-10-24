using System;
using System.Collections.Generic;
using System.IO;
using System.IO.IsolatedStorage;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;
using Microsoft.Phone.Controls;
using Microsoft.Phone.Shell;
using MEmarket.Models;
using MEmarket.Services;
using System.Windows.Media.Imaging;
using System.Threading.Tasks;
using System.Net.Http;
using Newtonsoft.Json.Linq;
using Windows.Storage.Pickers;
using Windows.Networking.Sockets;
using System.Text;
using Windows.Networking;
using Windows.Storage;
using System.Threading;

namespace MEmarket
{
    public partial class AppPage : PhoneApplicationPage
    {
        private AppItem _currentApp;
        private List<AppVersion> _versions;
        private int _currentScreenshotIndex = -1;
        private const double DescriptionCollapsedHeight = 150;
        private const double DescriptionCollapseTolerance = 24;
        private bool _isDescriptionExpanded;

        public AppPage()
        {
            InitializeComponent();
            BuildApplicationBar();
            Loaded += (s, e) => UpdateDescriptionCollapse();
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            _currentApp = App.SelectedApp;
            if (_currentApp == null) return;

            AppPivot.Title = _currentApp.Name ?? "APP";
            AppName.Text = _currentApp.Name ?? string.Empty;
            DeveloperText.Text = _currentApp.Publisher ?? string.Empty;
            CategoryText.Text = GetDisplayCategory(_currentApp.Category);
            AppTypeText.Text = GetDisplayAppType(_currentApp.AppType);

            if (!string.IsNullOrEmpty(_currentApp.Icon) && _currentApp.Icon.StartsWith("http"))
            {
                try { AppIcon.Source = new BitmapImage(new Uri(_currentApp.Icon)); }
                catch { }
            }

            SetDescription(_currentApp.Summary ?? "");
            SummaryText.Text = _currentApp.Summary ?? "";
            PublisherText.Text = _currentApp.Publisher ?? string.Empty;
            RepositoryText.Text = GetRepositoryName(_currentApp);
            //AppSizeText.Text = "Размер: загрузка...";

            if (!string.IsNullOrEmpty(_currentApp.AppUrl))
            {
                await LoadEntryAsync(_currentApp.AppUrl);
            }
        }

 
        private void SetDescription(string markdown)
        {
            MarkdownRenderer.Render(DescriptionText, markdown);
            Dispatcher.BeginInvoke(() => UpdateDescriptionCollapse());
        }

        private void UpdateDescriptionCollapse()
        {
            DescriptionText.MaxHeight = double.PositiveInfinity;
            DescriptionText.UpdateLayout();

            bool needsCollapse = DescriptionText.ActualHeight > DescriptionCollapsedHeight + DescriptionCollapseTolerance;
            if (!needsCollapse)
            {
                DescriptionToggleButton.Visibility = Visibility.Collapsed;
                return;
            }

            DescriptionToggleButton.Visibility = Visibility.Visible;
            ApplyDescriptionState();
        }

        private void ApplyDescriptionState()
        {
            DescriptionText.MaxHeight = _isDescriptionExpanded
                ? double.PositiveInfinity
                : DescriptionCollapsedHeight;
            DescriptionToggleButton.Content = _isDescriptionExpanded
                ? MEmarket.Resources.AppResources.CollapseText
                : MEmarket.Resources.AppResources.ExpandText;
        }

        private void DescriptionToggleButton_Click(object sender, RoutedEventArgs e)
        {
            _isDescriptionExpanded = !_isDescriptionExpanded;
            ApplyDescriptionState();
        }

        private void UpdateAppMetadataText()
        {
            if (_currentApp == null) return;

            CategoryText.Text = GetDisplayCategory(_currentApp.Category);
            AppTypeText.Text = GetDisplayAppType(_currentApp.AppType);
        }

        private string GetDisplayCategory(string category)
        {
            if (string.IsNullOrWhiteSpace(category))
            {
                return MEmarket.Resources.AppResources.CategoryOther;
            }

            var key = category.Trim();
            if (string.IsNullOrEmpty(key))
            {
                return MEmarket.Resources.AppResources.CategoryOther;
            }

            switch (key.ToLowerInvariant())
            {
                case "games":
                    return MEmarket.Resources.AppResources.CategoryGames;
                case "store":
                    return MEmarket.Resources.AppResources.CategoryStore;
                case "entertainment":
                    return MEmarket.Resources.AppResources.CategoryEntertainment;
                case "music+video":
                    return MEmarket.Resources.AppResources.CategoryMusicVideo;
                case "tools":
                    return MEmarket.Resources.AppResources.CategoryTools;
                case "livestyle":
                    return MEmarket.Resources.AppResources.CategoryLifestyle;
                case "news+weather":
                    return MEmarket.Resources.AppResources.CategoryNewsWeather;
                case "health+fitness":
                    return MEmarket.Resources.AppResources.CategoryHealthFitness;
                case "photo":
                    return MEmarket.Resources.AppResources.CategoryPhoto;
                case "social":
                    return MEmarket.Resources.AppResources.CategorySocial;
                case "sports":
                    return MEmarket.Resources.AppResources.CategorySports;
                case "business":
                    return MEmarket.Resources.AppResources.CategoryBusiness;
                case "education":
                    return MEmarket.Resources.AppResources.CategoryEducation;
                default:
                    return MEmarket.Resources.AppResources.CategoryOther;
            }
        }

        private string GetDisplayAppType(string appType)
        {
            if (string.IsNullOrWhiteSpace(appType))
            {
                return MEmarket.Resources.AppResources.IdkLol;
            }

            var normalized = appType.Trim();
            if (string.IsNullOrEmpty(normalized))
            {
                return MEmarket.Resources.AppResources.IdkLol;
            }

            switch (normalized.ToLowerInvariant())
            {
                case "silverlight":
                    return "Silverlight";
                case "winrt":
                    return "WinRT";
                case "uwp":
                    return "UWP";
                default:
                    return FormatReadableText(normalized.Replace("_", " ").Replace("-", " "));
            }
        }

        private string FormatReadableText(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return MEmarket.Resources.AppResources.IdkLol;
            }

            var words = text.Split(' ');
            for (int i = 0; i < words.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(words[i])) continue;

                var word = words[i].Trim();
                if (word.Length == 0) continue;

                var first = char.ToUpperInvariant(word[0]);
                if (word.Length == 1)
                {
                    words[i] = first.ToString();
                    continue;
                }

                words[i] = first + word.Substring(1).ToLowerInvariant();
            }

            return string.Join(" ", words);
        }

        private string GetLocalizedText(JToken token)
        {
            if (token == null || token.Type == JTokenType.Null)
            {
                return string.Empty;
            }

            if (token.Type == JTokenType.String)
            {
                return token.ToString();
            }

            var obj = token as JObject;
            if (obj == null)
            {
                return token.ToString();
            }

            var culture = System.Globalization.CultureInfo.CurrentUICulture;
            var currentLanguage = culture?.Name ?? string.Empty;
            var baseLanguage = culture?.TwoLetterISOLanguageName ?? string.Empty;

            var preferredLanguages = new List<string>();
            if (!string.IsNullOrWhiteSpace(currentLanguage)) preferredLanguages.Add(currentLanguage);
            if (!string.IsNullOrWhiteSpace(baseLanguage)) preferredLanguages.Add(baseLanguage);
            preferredLanguages.Add("en");
            preferredLanguages.Add("ru");

            foreach (var language in preferredLanguages.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                var property = obj.Property(language);
                if (property != null && property.Value != null)
                {
                    return property.Value.ToString();
                }
            }

            foreach (var property in obj.Properties())
            {
                if (property.Value != null)
                {
                    return property.Value.ToString();
                }
            }

            return string.Empty;
        }

        private string GetRepositoryName(AppItem app)
        {
            if (app == null) return MEmarket.Resources.AppResources.IdkLol;

            if (!string.IsNullOrEmpty(app.RepositoryName))
            {
                return app.RepositoryName;
            }

            if (!string.IsNullOrEmpty(app.AppUrl))
            {
                try
                {
                    var appUri = new Uri(app.AppUrl);
                    var appHost = appUri.Host.ToLower();

                    foreach (var repo in App.Repositories)
                    {
                        if (string.IsNullOrEmpty(repo.Url)) continue;
                        try
                        {
                            var repoUri = new Uri(repo.Url);
                            if (repoUri.Host.ToLower() == appHost)
                            {
                                return repo.Name;
                            }
                        }
                        catch { }
                    }
                }
                catch { }
            }

            return MEmarket.Resources.AppResources.IdkLol;
        }

        private async Task LoadEntryAsync(string appUrl)
        {
            try
            {
                var progressIndicator = new ProgressIndicator
                {
                    IsVisible = true,
                    IsIndeterminate = true,
                    Text = MEmarket.Resources.AppResources.LoadingText
                };
                SystemTray.SetProgressIndicator(this, progressIndicator);

                var httpClient = new HttpClient();
                string entryUrl = appUrl.EndsWith("/") ? appUrl + "entry.json" : appUrl + "/entry.json";
                string cacheBuster = DateTime.Now.Ticks.ToString();
                entryUrl += "?v=" + cacheBuster;

                var response = await httpClient.GetStringAsync(entryUrl);
                var root = JObject.Parse(response);

                if (root["id"] != null)
                {
                    _currentApp.Id = root["id"].ToString();
                }

                if (root["title"] != null)
                {
                    var title = root["title"].ToString();
                    AppPivot.Title = title;
                    AppName.Text = title;
                }

                if (root["summary"] != null)
                {
                    SummaryText.Text = GetLocalizedText(root["summary"]);
                }

                if (root["description"] != null)
                {
                    SetDescription(GetLocalizedText(root["description"]));
                }

                var bannerUrl = root["banner"]?.ToString();
                if (!string.IsNullOrWhiteSpace(bannerUrl))
                {
                    var fullBannerUrl = App.GetFullUrl(appUrl, bannerUrl);
                    if (!string.IsNullOrWhiteSpace(fullBannerUrl))
                    {
                        BannerImage.Visibility = Visibility.Visible;
                        BannerImage.Source = new BitmapImage(new Uri(fullBannerUrl));
                    }
                    else
                    {
                        BannerImage.Visibility = Visibility.Collapsed;
                    }
                }
                else
                {
                    BannerImage.Visibility = Visibility.Collapsed;
                }

                if (root["creator"] != null)
                {
                    var creator = root["creator"].ToString();
                    PublisherText.Text = creator;
                    DeveloperText.Text = creator;
                    _currentApp.Publisher = creator;
                }

                if (root["category"] != null)
                {
                    var category = root["category"].ToString();
                    _currentApp.Category = category;
                    CategoryText.Text = GetDisplayCategory(category);
                }

                if (root["app_type"] != null)
                {
                    var appType = root["app_type"].ToString();
                    _currentApp.AppType = appType;
                    AppTypeText.Text = GetDisplayAppType(appType);
                }

                //AppSizeText.Text = "Размер: н/д";

                var screenshotsArray = root["screenshots"] as JArray;
                if (screenshotsArray != null)
                {
                    ScreenshotsPanel.Children.Clear();

                    foreach (var screenshotValue in screenshotsArray)
                    {
                        var screenshotRelUrl = screenshotValue.ToString().Trim();
                        var screenshotFullUrl = App.GetFullUrl(appUrl, screenshotRelUrl);
                        if (!string.IsNullOrEmpty(screenshotFullUrl))
                        {
                            var img = new Image
                            {
                                Source = new BitmapImage(new Uri(screenshotFullUrl)),
                                Width = 240,
                                Height = 427,
                                Margin = new Thickness(0, 0, 8, 0),
                                Stretch = System.Windows.Media.Stretch.UniformToFill
                            };
                            img.ImageFailed += IconImage_ImageFailed;
                            ScreenshotsPanel.Children.Add(img);
                            img.Tap += Screenshot_Tap;
                        }
                    }

                    ScreenshotsScrollViewer.Visibility = ScreenshotsPanel.Children.Count > 0
                        ? Visibility.Visible
                        : Visibility.Collapsed;
                }

                CapabilitiesPanel.Children.Clear();
                var capsArray = root["capabilities"] as JArray;
                if (capsArray != null)
                {
                    foreach (var cap in capsArray)
                    {
                        CapabilitiesPanel.Children.Add(new TextBlock
                        {
                            Text = "• " + cap.ToString(),
                            TextWrapping = TextWrapping.Wrap,
                            Margin = new Thickness(0, 0, 0, 4)
                        });
                    }
                }
                if (CapabilitiesPanel.Children.Count == 0)
                {
                    CapabilitiesPanel.Children.Add(new TextBlock
                    {
                        Text = MEmarket.Resources.AppResources.IdkLol,
                        Opacity = 0.6
                    });
                }

                _versions = new List<AppVersion>();
                var versionsArray = root["versions"] as JArray;
                if (versionsArray != null)
                {
                    foreach (var versionValue in versionsArray)
                    {
                        var vObj = versionValue as JObject;
                        if (vObj != null)
                        {
                            _versions.Add(new AppVersion
                            {
                                Version = vObj["version"]?.ToString() ?? "?",
                                AppFile = vObj["app_file"]?.ToString() ?? ""
                            });
                        }
                    }
                }

                if (_versions.Count == 1)
                {
                    AppVersionText.Text = _versions[0].Version;
                    AppVersionText.Visibility = Visibility.Visible;
                    VersionComboBox.Visibility = Visibility.Collapsed;
                }
                else if (_versions.Count > 1)
                {
                    AppVersionText.Visibility = Visibility.Collapsed;
                    VersionComboBox.Visibility = Visibility.Visible;
                    VersionComboBox.Items.Clear();

                    foreach (var ver in _versions)
                    {
                        VersionComboBox.Items.Add(ver.Version);
                    }
                    VersionComboBox.SelectedIndex = 0;
                }
                else
                {
                    AppVersionText.Text = MEmarket.Resources.AppResources.IdkLol;
                    AppVersionText.Visibility = Visibility.Visible;
                    VersionComboBox.Visibility = Visibility.Collapsed;
                }

                progressIndicator.IsVisible = false;
                SystemTray.SetProgressIndicator(this, null);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("entry.json load error: " + ex.Message);
                SetDescription(_currentApp?.Summary ?? MEmarket.Resources.AppResources.IdkLol);
                SummaryText.Text = _currentApp?.Summary ?? "";
                //AppSizeText.Text = "Размер: н/д";
                UpdateAppMetadataText();
                AppVersionText.Text = (_currentApp?.LatestVersion ?? MEmarket.Resources.AppResources.IdkLol);
                CapabilitiesPanel.Children.Clear();
                CapabilitiesPanel.Children.Add(new TextBlock { Text = MEmarket.Resources.AppResources.IdkLol, Opacity = 0.6 });
                SystemTray.SetProgressIndicator(this, null);
            }
        }

        private AppVersion GetSelectedVersion()
        {
            if (_versions == null || _versions.Count == 0) return null;

            if (VersionComboBox.Visibility == Visibility.Visible && VersionComboBox.SelectedIndex >= 0)
            {
                int index = VersionComboBox.SelectedIndex;
                if (index < _versions.Count)
                {
                    return _versions[index];
                }
            }

            return _versions.FirstOrDefault();
        }

        private string BuildDownloadUrl(AppVersion version)
        {
            if (version == null || string.IsNullOrEmpty(version.AppFile)) return null;
            if (_currentApp == null || string.IsNullOrEmpty(_currentApp.AppUrl)) return null;

            string baseUrl = _currentApp.AppUrl.EndsWith("/")
                ? _currentApp.AppUrl
                : _currentApp.AppUrl + "/";

            return baseUrl + version.AppFile;
        }

        // --- РАЗВЕРТЫВАНИЕ ПРИЛОЖЕНИЙ СРАЗУ ПОСЛЕ СКАЧИВАНИЯ ---

        private async void Install_Click(object sender, EventArgs e)
        {
            var version = GetSelectedVersion();
            var downloadUrl = BuildDownloadUrl(version);

            if (string.IsNullOrEmpty(downloadUrl))
            {
                MessageBox.Show(MEmarket.Resources.AppResources.DownloadUrlError, "╮ (. ❛ ᴗ ❛.) ╭", MessageBoxButton.OK);
                return;
            }

            if (ApplicationBar != null)
            {
                foreach (IApplicationBarIconButton button in ApplicationBar.Buttons)
                {
                    button.IsEnabled = false;
                }
            }

            var progressIndicator = new ProgressIndicator
            {
                IsVisible = true,
                IsIndeterminate = true,
                Text = MEmarket.Resources.AppResources.InstallProgressTextDownloading + (_currentApp?.Name ?? "???") + "..."
            };
            SystemTray.SetProgressIndicator(this, progressIndicator);

            try
            {
                StorageFile downloadedFile = null;
                try
                {
                    StorageFolder localFolder = ApplicationData.Current.LocalFolder;

                    Uri uri = new Uri(downloadUrl);
                    string fileExt = Path.GetExtension(uri.AbsolutePath).ToLower();
                    if (string.IsNullOrEmpty(fileExt)) fileExt = ".xap";

                    // Сохранение временного файла во внутреннюю память приложения
                    string fileName = "temp_install_package" + fileExt;
                    downloadedFile = await localFolder.CreateFileAsync(fileName, CreationCollisionOption.ReplaceExisting);

                    using (HttpClient httpClient = new HttpClient())
                    {
                        byte[] fileBytes = await httpClient.GetByteArrayAsync(uri);
                        await FileIO.WriteBytesAsync(downloadedFile, fileBytes);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show(MEmarket.Resources.AppResources.SavingPkgError + ex.Message, "(╯︵╰)", MessageBoxButton.OK);
                }

                if (downloadedFile != null)
                {
                    // Файл скачан, переход к установке
                    progressIndicator.IsVisible = false;
                    SystemTray.SetProgressIndicator(this, null);

                    await InstallPackageAsync(downloadedFile);
                }
                else
                {
                    progressIndicator.IsVisible = false;
                    SystemTray.SetProgressIndicator(this, null);
                    MessageBox.Show(MEmarket.Resources.AppResources.DownloadingPkgError, "(☆_@)", MessageBoxButton.OK);

                    if (ApplicationBar != null)
                    {
                        foreach (IApplicationBarIconButton button in ApplicationBar.Buttons)
                        {
                            button.IsEnabled = true;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(MEmarket.Resources.AppResources.DownloadingPkgError + ex.Message, "(☆_@)", MessageBoxButton.OK);
                progressIndicator.IsVisible = false;
                SystemTray.SetProgressIndicator(this, null);

                if (ApplicationBar != null)
                {
                    foreach (IApplicationBarIconButton button in ApplicationBar.Buttons)
                    {
                        button.IsEnabled = true;
                    }
                }
            }
        }

        private async Task InstallPackageAsync(StorageFile packageFile)
        {
            string fileName = Path.GetFileNameWithoutExtension(packageFile.Name);

            var progressIndicator = new ProgressIndicator
            {
                IsVisible = true,
                IsIndeterminate = true,
                Text = MEmarket.Resources.AppResources.InstallProgressTextInstalling + (_currentApp?.Name ?? MEmarket.Resources.AppResources.IdkLol) + "..."
            };
            SystemTray.SetProgressIndicator(this, progressIndicator);

            try
            {
                var installer = new MEmarket.Services.MEinstaller();

                try
                {
                    var properties = await packageFile.GetBasicPropertiesAsync();
                    System.Diagnostics.Debug.WriteLine($"[Диагностика] Размер скачанного файла: {properties.Size} bytes");
                }
                catch (Exception sizeEx)
                {
                    System.Diagnostics.Debug.WriteLine("Не удалось узнать размер: " + sizeEx.Message);
                }

                // Логирование для обновления интерфейса
                //installer.LogUpdated += (message) =>
                //{
                //    System.Diagnostics.Debug.WriteLine("[Installer Log] " + message);

                //    this.Dispatcher.BeginInvoke(() =>
                //    {
                //        if (message.Contains("Подключение"))
                //            progressIndicator.Text = "Подключение к Telnet...";
                //        else if (message.Contains("Выполнение"))
                //            progressIndicator.Text = "Установка " + fileName + "...";
                //        else if (message.Contains("очистка"))
                //            progressIndicator.Text = "Очистка временных файлов...";
                //        else
                //            progressIndicator.Text = message;
                //    });
                //};

                installer.ProgressStateChanged += (isInstalling) =>
                {
                    this.Dispatcher.BeginInvoke(() =>
                    {
                        progressIndicator.IsIndeterminate = isInstalling;
                    });
                };

                // Запуск последовательной установки (возвращает объект InstallResult)
                var result = await installer.InstallAppAsync(packageFile, installToSdCard: false);

                // Удаление временный файл пакета из папки приложения
                try
                {
                    await packageFile.DeleteAsync();
                }
                catch { }

                progressIndicator.IsVisible = false;
                SystemTray.SetProgressIndicator(this, null);

                if (result.IsSuccess)
                {
                    // Регистрирация в журнал установленных если скачивание успешное
                    try
                    {
                        var version = GetSelectedVersion();
                        string versionToSave = version != null ? version.Version : (_currentApp.LatestVersion ?? "1.0.0.0");
                        string repoUrl = "";
                        if (!string.IsNullOrEmpty(_currentApp.AppUrl))
                        {
                            var appUri = new Uri(_currentApp.AppUrl);
                            repoUrl = appUri.Scheme + "://" + appUri.Host;
                            if (appUri.Port != 80 && appUri.Port != 443)
                            {
                                repoUrl += ":" + appUri.Port;
                            }
                        }

                        LocalAppsManager.RegisterInstalledApp(
                            _currentApp.Id,
                            _currentApp.Name,
                            _currentApp.Icon,
                            versionToSave,
                            repoUrl,
                            _currentApp.AppUrl
                        );
                    }
                    catch (Exception dbEx)
                    {
                        System.Diagnostics.Debug.WriteLine("Ошибка записи в журнал установленных: " + dbEx.Message);
                    }

                    MessageBox.Show((_currentApp?.Name ?? MEmarket.Resources.AppResources.IdkLol) + MEmarket.Resources.AppResources.AppInstallSuccessMessage, "( ˶ˆᗜˆ˵ )", MessageBoxButton.OK);
                }
                else
                {
                    // Если произошла ошибка - подробный отчет и сообщение о сохраненном логе
                    string errorCode = result.ErrorCode ?? MEmarket.Resources.AppResources.IdkLol;
                    string errorDesc = result.ErrorDescription ?? MEmarket.Resources.AppResources.IdkLol;
                    string appName = _currentApp?.Name ?? MEmarket.Resources.AppResources.IdkLol;

                    string promptMsg = string.Format(
                        MEmarket.Resources.AppResources.DeployingErrorMessageFormat,
                        appName,
                        errorCode,
                        errorDesc
                        );

                    MessageBox.Show(promptMsg, "(╥_╥)", MessageBoxButton.OK);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Исключение в InstallPackageAsync: " + ex.Message);
                progressIndicator.IsVisible = false;
                SystemTray.SetProgressIndicator(this, null);
            }
            finally
            {
                if (ApplicationBar != null)
                {
                    foreach (IApplicationBarIconButton button in ApplicationBar.Buttons)
                    {
                        button.IsEnabled = true;
                    }
                }
            }
        }

        // ---

        private void SaveToDevice_Click(object sender, EventArgs e)
        {
            var version = GetSelectedVersion();
            var downloadUrl = BuildDownloadUrl(version);

            if (string.IsNullOrEmpty(downloadUrl))
            {
                MessageBox.Show(MEmarket.Resources.AppResources.DownloadUrlError, "╮ (. ❛ ᴗ ❛.) ╭", MessageBoxButton.OK);
                return;
            }

            try
            {
                // Сохранения прилоения в installer_apps.json перед скачиванием
                try
                {
                    string versionToSave = version != null ? version.Version : (_currentApp.LatestVersion ?? "1.0.0.0");

                    string repoUrl = "";
                    if (!string.IsNullOrEmpty(_currentApp.AppUrl))
                    {
                        var appUri = new Uri(_currentApp.AppUrl);
                        repoUrl = appUri.Scheme + "://" + appUri.Host;
                        if (appUri.Port != 80 && appUri.Port != 443)
                        {
                            repoUrl += ":" + appUri.Port;
                        }
                    }

                    LocalAppsManager.RegisterInstalledApp(
                        _currentApp.Id,
                        _currentApp.Name,
                        _currentApp.Icon,
                        versionToSave,
                        repoUrl,
                        _currentApp.AppUrl
                    );

                    System.Diagnostics.Debug.WriteLine("Приложение " + _currentApp.Name + " успешно добавлено в журнал.");
                }
                catch (Exception dbEx)
                {
                    System.Diagnostics.Debug.WriteLine("Ошибка записи в базу приложений: " + dbEx.Message);
                }

                var savePicker = new FileSavePicker();
                savePicker.SuggestedStartLocation = PickerLocationId.Downloads;

                var fileName = Path.GetFileName(downloadUrl.Split('?')[0]);
                var extension = Path.GetExtension(fileName).ToLower();

                if (extension == ".xap")
                    savePicker.FileTypeChoices.Add("XAP-файлы", new List<string>() { ".xap" });
                else if (extension == ".appx")
                    savePicker.FileTypeChoices.Add("APPX-файлы", new List<string>() { ".appx" });
                else if (extension == ".appxbundle")
                    savePicker.FileTypeChoices.Add("APPX Bundle-файлы", new List<string>() { ".appxbundle" });
                else
                    savePicker.FileTypeChoices.Add("Файлы приложений", new List<string>() { string.IsNullOrEmpty(extension) ? ".bin" : extension });

                savePicker.SuggestedFileName = fileName;
                savePicker.ContinuationData["DownloadUrl"] = downloadUrl;
                savePicker.ContinuationData["AppName"] = _currentApp?.Name ?? "app";

                savePicker.PickSaveFileAndContinue();
            }
            catch (Exception ex)
            {
                MessageBox.Show(MEmarket.Resources.AppResources.StartDownloadingError + ex.Message, "〣( ºΔº )〣", MessageBoxButton.OK);
            }
        }

        private void VersionComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
        }

        private void IconImage_ImageFailed(object sender, ExceptionRoutedEventArgs e)
        {
            var image = sender as Image;
            if (image == null) return;

            var bitmap = image.Source as BitmapImage;
            if (bitmap == null || bitmap.UriSource == null) return;

            var currentUri = bitmap.UriSource.AbsoluteUri;
            if (image.Tag as string == currentUri) return;

            var fallbackUri = App.GetFallbackIconUri(bitmap.UriSource);
            if (fallbackUri != null)
            {
                image.Tag = currentUri;
                image.Source = new BitmapImage(fallbackUri);
            }
        }

        private void BuildApplicationBar()
        {
            ApplicationBar = new ApplicationBar();

            ApplicationBarIconButton buttonSave = new ApplicationBarIconButton();
            buttonSave.IconUri = new Uri("/Assets/AppBar/save.png", UriKind.Relative);
            buttonSave.Text = MEmarket.Resources.AppResources.AppBarSave;
            buttonSave.Click += SaveToDevice_Click;
            ApplicationBar.Buttons.Add(buttonSave);

            ApplicationBarIconButton buttonDownload = new ApplicationBarIconButton();
            buttonDownload.IconUri = new Uri("/Assets/AppBar/download.png", UriKind.Relative);
            buttonDownload.Text = MEmarket.Resources.AppResources.AppBarDownload;
            buttonDownload.Click += Install_Click;
            ApplicationBar.Buttons.Add(buttonDownload);
        }

        private void Screenshot_Tap(object sender, System.Windows.Input.GestureEventArgs e)
        {
            var clickedImage = sender as Image;
            if (clickedImage == null || clickedImage.Source == null) return;

            _currentScreenshotIndex = ScreenshotsPanel.Children.IndexOf(clickedImage);

            ImageViewerImage.Source = clickedImage.Source;

            ImageViewerGrid.Visibility = Visibility.Visible;

            SlideUpStoryboard.Begin();

            if (ApplicationBar != null)
            {
                ApplicationBar.IsVisible = false;
            }
        }

        private void ImageViewer_ManipulationCompleted(object sender, System.Windows.Input.ManipulationCompletedEventArgs e)
        {
            double swipeDistance = e.TotalManipulation.Translation.X;

            if (swipeDistance < -50)
            {
                if (_currentScreenshotIndex < ScreenshotsPanel.Children.Count - 1)
                {
                    _currentScreenshotIndex++;
                    ShowScreenshot(_currentScreenshotIndex);
                }
            }
            else if (swipeDistance > 50)
            {
                if (_currentScreenshotIndex > 0)
                {
                    _currentScreenshotIndex--;
                    ShowScreenshot(_currentScreenshotIndex);
                }
            }
        }

        private void ShowScreenshot(int index)
        {
            if (index >= 0 && index < ScreenshotsPanel.Children.Count)
            {
                var img = ScreenshotsPanel.Children[index] as Image;
                if (img != null)
                {
                    FadeInStoryboard.Begin();
                    ImageViewerImage.Source = img.Source;
                }
            }
        }

        private void CloseImageViewer()
        {
            EventHandler completedHandler = null;
            completedHandler = (s, args) =>
            {
                SlideDownStoryboard.Completed -= completedHandler;
                ImageViewerGrid.Visibility = Visibility.Collapsed;
                ImageViewerImage.Source = null;
            };

            SlideDownStoryboard.Completed += completedHandler;

            SlideDownStoryboard.Begin();

            if (ApplicationBar != null)
            {
                ApplicationBar.IsVisible = true;
            }
        }

        protected override void OnBackKeyPress(System.ComponentModel.CancelEventArgs e)
        {
            if (ImageViewerGrid != null && ImageViewerGrid.Visibility == Visibility.Visible)
            {
                CloseImageViewer();
                e.Cancel = true;
                return;
            }

            base.OnBackKeyPress(e);
        }
    }
}