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
            PublisherText.Text = "";
            //AppSizeText.Text = "Размер: загрузка...";
            
            if (!string.IsNullOrEmpty(_currentApp.AppUrl))
            {
                await LoadEntryAsync(_currentApp.AppUrl);
            }
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

                if (root["title"] != null)
                {
                    AppPivot.Title = root["title"].ToString();
                }
                
                if (root["description"] != null)
                {
                    MarkdownRenderer.Render(DescriptionText, root["description"].ToString());
                }
                
                if (root["creator"] != null)
                {
                    PublisherText.Text = "Разработчик: " + root["creator"].ToString();
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
                    AppVersionText.Text = "Версия: " + _versions[0].Version;
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
                        VersionComboBox.Items.Add(new ComboBoxItem
                        {
                            Content = ver.Version,
                            Tag = ver
                        });
                    }
                    VersionComboBox.SelectedIndex = 0;
                }
                else
                {
                    AppVersionText.Text = "Версия: неизвестно";
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
                //AppSizeText.Text = "Размер: н/д";
                AppVersionText.Text = "Версия: " + (_currentApp?.LatestVersion ?? "неизвестно");
                CapabilitiesPanel.Children.Clear();
                CapabilitiesPanel.Children.Add(new TextBlock { Text = "Не удалось загрузить.", Opacity = 0.6 });
                SystemTray.SetProgressIndicator(this, null);
            }
        }

        private AppVersion GetSelectedVersion()
        {
            if (_versions == null || _versions.Count == 0) return null;

            if (VersionComboBox.Visibility == Visibility.Visible && VersionComboBox.SelectedItem != null)
            {
                var item = VersionComboBox.SelectedItem as ComboBoxItem;
                return item?.Tag as AppVersion;
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

            var progressIndicator = new ProgressIndicator
            {
                IsVisible = true,
                IsIndeterminate = false,
                Text = "Скачивание..."
            };
            SystemTray.SetProgressIndicator(this, progressIndicator);

            try
            {
                string fileName = "temp_install_package.appx";

                var httpClient = new HttpClient();
                var responseMsg = await httpClient.GetAsync(new Uri(downloadUrl));
                responseMsg.EnsureSuccessStatusCode();

                StorageFolder localFolder = ApplicationData.Current.LocalFolder;
                StorageFile tempFile = await localFolder.CreateFileAsync(fileName, CreationCollisionOption.ReplaceExisting);

                using (var inputStream = await responseMsg.Content.ReadAsStreamAsync())
                using (var outputStream = await tempFile.OpenStreamForWriteAsync())
                {
                    await inputStream.CopyToAsync(outputStream);
                }
                
                string physicalPath = tempFile.Path;

                progressIndicator.Text = "Подключение к CMD Injector...";
                progressIndicator.IsIndeterminate = true;
                
                var cts = new CancellationTokenSource();
                var tClient = new TelnetClient(TimeSpan.FromSeconds(1), cts.Token);
                tClient.Connect();

                long i = 0;
                while (tClient.IsConnected == false && i < 150)
                {
                    await Task.Delay(100);
                    i++;
                }

                if (!tClient.IsConnected)
                {
                    progressIndicator.IsVisible = false;
                    SystemTray.SetProgressIndicator(this, null);
                    MessageBox.Show("Не удалось подключиться к CMD Injector. Проверьте, активна ли инъекция на телефоне.", "Ошибка", MessageBoxButton.OK);
                    return;
                }

                progressIndicator.Text = "Установка приложения...";
                
                string command = string.Format("TH.exe \"{0}\" -di", physicalPath);
                
                await Task.Run(() =>
                {
                    tClient.Send(command);
                });
                
                await Task.Delay(6000);
                
                try
                {
                    await tempFile.DeleteAsync();
                }
                catch { }

                progressIndicator.IsVisible = false;
                SystemTray.SetProgressIndicator(this, null);
                MessageBox.Show("Установка успешно завершена! Приложение появится в списке меню.", "(o^ ^o)", MessageBoxButton.OK);
            }
            catch (Exception ex)
            {
                progressIndicator.IsVisible = false;
                SystemTray.SetProgressIndicator(this, null);
                MessageBox.Show("Ошибка установки: " + ex.Message, "〣( ºΔº )〣", MessageBoxButton.OK);
            }
        }*/

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