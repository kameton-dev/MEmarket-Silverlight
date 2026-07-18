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
//using WUT_WP8.UWP;
using System.Threading;
using CMDInjectorHelper;

namespace MEmarket
{
    public partial class AppPage : PhoneApplicationPage
    {
        private AppItem _currentApp;
        private List<AppVersion> _versions;

        public AppPage()
        {
            InitializeComponent();
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            _currentApp = App.SelectedApp;
            if (_currentApp == null) return;
            
            AppPivot.Title = _currentApp.Name ?? "ПРИЛОЖЕНИЕ";
            
            if (!string.IsNullOrEmpty(_currentApp.Icon) && _currentApp.Icon.StartsWith("http"))
            {
                try { AppIcon.Source = new BitmapImage(new Uri(_currentApp.Icon)); }
                catch { }
            }
            
            MarkdownRenderer.Render(DescriptionText, _currentApp.Summary ?? "");
            SummaryText.Text = _currentApp.Summary ?? "";
            PublisherText.Text = "";
            RepositoryText.Text = GetRepositoryName(_currentApp);
            //AppSizeText.Text = "Размер: загрузка...";
            
            if (!string.IsNullOrEmpty(_currentApp.AppUrl))
            {
                await LoadEntryAsync(_currentApp.AppUrl);
            }
        }

        private string GetRepositoryName(AppItem app)
        {
            if (app == null) return "неизвестно";
            
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

            return "неизвестно";
        }

        private async Task LoadEntryAsync(string appUrl)
        {
            try
            {
                var progressIndicator = new ProgressIndicator
                {
                    IsVisible = true,
                    IsIndeterminate = true,
                    Text = "Загрузка..."
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
                    AppPivot.Title = root["title"].ToString();
                }

                if (root["summary"] != null)
                {
                    SummaryText.Text = root["summary"].ToString();
                }

                if (root["description"] != null)
                {
                    MarkdownRenderer.Render(DescriptionText, root["description"].ToString());
                }
                
                if (root["creator"] != null)
                {
                    PublisherText.Text = root["creator"].ToString();
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
                        Text = "Не указано",
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
                    AppVersionText.Text = "неизвестно";
                    AppVersionText.Visibility = Visibility.Visible;
                    VersionComboBox.Visibility = Visibility.Collapsed;
                }

                progressIndicator.IsVisible = false;
                SystemTray.SetProgressIndicator(this, null);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("entry.json load error: " + ex.Message);
                MarkdownRenderer.Render(DescriptionText, _currentApp?.Summary ?? "Не удалось загрузить описание.");
                SummaryText.Text = _currentApp?.Summary ?? "";
                //AppSizeText.Text = "Размер: н/д";
                AppVersionText.Text =(_currentApp?.LatestVersion ?? "неизвестно");
                CapabilitiesPanel.Children.Clear();
                CapabilitiesPanel.Children.Add(new TextBlock { Text = "Не удалось загрузить.", Opacity = 0.6 });
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

        // --- ПОПЫТКИ РАЗВЕРТЫВАНИЯ ПРИЛОЖЕНИЙ СРАЗУ ПОСЛЕ СКАЧИВАНИЯ ---
        // ВНИМАНИЕ: ИСХОДНИКИ ВЗЯТЫ ИЗ ДЕКОМПИЛИРОВАННОЙ LIVESTORE
        //TODO: либо удалить либо перелопатить

        /*private async void Install_Click(object sender, EventArgs e)
        {
            var version = GetSelectedVersion();
            var downloadUrl = BuildDownloadUrl(version);

            if (string.IsNullOrEmpty(downloadUrl))
            {
                MessageBox.Show("Ссылка на скачивание недоступна.", "╮ (. ❛ ᴗ ❛.) ╭", MessageBoxButton.OK);
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
                Text = "Downloading " + (_currentApp?.Name ?? "app") + "..."
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
                    MessageBox.Show("Error saving file: " + ex.Message, "MEmarket", MessageBoxButton.OK);
                }

                if (downloadedFile != null)
                {
                    progressIndicator.IsVisible = false;
                    SystemTray.SetProgressIndicator(this, null);
                    
                    await InstallPackageAsync(downloadedFile);
                }
                else
                {
                    progressIndicator.IsVisible = false;
                    SystemTray.SetProgressIndicator(this, null);
                    MessageBox.Show("Download failed.", "MEmarket", MessageBoxButton.OK);

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
                MessageBox.Show("Error downloading file: " + ex.Message, "MEmarket", MessageBoxButton.OK);
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
                Text = "Deploying " + fileName + ".."
            };
            SystemTray.SetProgressIndicator(this, progressIndicator);

            try
            {
                var installer = new MEinstaller();
                
                installer.output = new Action<string>((message) =>
                {
                    System.Diagnostics.Debug.WriteLine("[MEinstaller Log] " + message);

                    this.Dispatcher.BeginInvoke(() =>
                    {
                        if (message.Contains("Connecting"))
                            progressIndicator.Text = "Connecting to telnet...";
                        else if (message.Contains("Executing"))
                            progressIndicator.Text = "Deploying " + fileName + "..";
                        else if (message.Contains("Cleaning"))
                            progressIndicator.Text = "Cleaning temp files...";
                    });
                });

                installer.progress = new Action<bool>((isInstalling) =>
                {
                    this.Dispatcher.BeginInvoke(() =>
                    {
                        progressIndicator.IsIndeterminate = isInstalling;
                    });
                });
                
                bool state = await installer.InstallApp(packageFile, InstallInSDCard: false);
                
                try
                {
                    await packageFile.DeleteAsync();
                }
                catch { }

                progressIndicator.IsVisible = false;
                SystemTray.SetProgressIndicator(this, null);

                if (state)
                {
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
                        System.Diagnostics.Debug.WriteLine("Ошибка записи в журнал: " + dbEx.Message);
                    }

                    MessageBox.Show(fileName + " has been installed!", "MEmarket", MessageBoxButton.OK);
                }
                else
                {
                    MessageBox.Show("Installation failed. Check CMD Injector status.", "MEmarket", MessageBoxButton.OK);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Error in InstallPackageAsync: " + ex.Message);
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
        } */

        // ---

        private void SaveToDevice_Click(object sender, EventArgs e)
        {
            var version = GetSelectedVersion();
            var downloadUrl = BuildDownloadUrl(version);

            if (string.IsNullOrEmpty(downloadUrl))
            {
                MessageBox.Show("Ссылка на скачивание недоступна.", "╮ (. ❛ ᴗ ❛.) ╭", MessageBoxButton.OK);
                return;
            }

            try
            {
                // Сохранения прилоения в installer_apps.json перед скачиванием
                try
                {
                    // Определяем версию, которую выбрал пользователь
                    string versionToSave = version != null ? version.Version : (_currentApp.LatestVersion ?? "1.0.0.0");

                    string repoUrl = "";
                    if (!string.IsNullOrEmpty(_currentApp.AppUrl))
                    {
                        var appUri = new Uri(_currentApp.AppUrl);
                        repoUrl = appUri.Scheme + "://" + appUri.Host;

                        // Если порт нестандартный (например, http://localhost:8080, что маловероятно), добавляем его
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
                MessageBox.Show("Ошибка при запуске скачивания: " + ex.Message, "〣( ºΔº )〣", MessageBoxButton.OK);
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
    }
}