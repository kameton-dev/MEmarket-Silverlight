using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;
using Microsoft.Phone.Controls;
using Microsoft.Phone.Shell;
using Windows.Data.Json;
using MEmarket.Services;
using MEmarket.Models;
using Windows.Storage.Pickers;
using System.IO;

namespace MEmarket
{
    public partial class InstalledAppsPage : PhoneApplicationPage
    {
        public ObservableCollection<UpdateableApp> AvailableUpdates { get; set; }
        public ObservableCollection<TrackedApp> InstalledApps { get; set; }

        public InstalledAppsPage()
        {
            InitializeComponent();

            AvailableUpdates = new ObservableCollection<UpdateableApp>();
            InstalledApps = new ObservableCollection<TrackedApp>();
            
            UpdatesListBox.ItemsSource = AvailableUpdates;
            InstalledListBox.ItemsSource = InstalledApps;
        }
        
        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            SetLoading(false);
            await RefreshInstalledListAsync();
        }
        
        private async Task RefreshInstalledListAsync()
        {
            SetLoading(true, "Загрузка журнала...");
            InstalledApps.Clear();

            try
            {
                var localApps = await Task.Run(() => LocalAppsManager.LoadApps());
                foreach (var app in localApps)
                {
                    InstalledApps.Add(app);
                }
                
                //NoAppsTextBlock.Visibility = InstalledApps.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Ошибка обновления журнала: " + ex.Message);
            }
            finally
            {
                SetLoading(false);
            }
        }

        private async void CheckUpdatesButton_Click(object sender, RoutedEventArgs e)
        {
            CheckUpdatesButton.IsEnabled = false;
            SetLoading(true, "Проверка обновлений...");
            AvailableUpdates.Clear();

            try
            {
                var localApps = await Task.Run(() => LocalAppsManager.LoadApps());
                
                var updates = await UpdateChecker.CheckForUpdatesAsync(localApps);

                foreach (var update in updates)
                {
                    AvailableUpdates.Add(update);
                }

                if (AvailableUpdates.Count == 0)
                {
                    MessageBox.Show("Все ваши приложения обновлены до актуальной версии.", "＼(＾▽＾)／", MessageBoxButton.OK);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Не удалось проверить обновления: " + ex.Message, "(｡╯︵╰｡)", MessageBoxButton.OK);
            }
            finally
            {
                SetLoading(false);
                CheckUpdatesButton.IsEnabled = true;
            }
        }
        
        private void UpdatesListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var listBox = sender as ListBox;
            if (listBox == null || listBox.SelectedItem == null) return;

            var appToUpdate = listBox.SelectedItem as UpdateableApp;
            
            listBox.SelectedIndex = -1;
            
            MessageBoxResult result = MessageBox.Show(
                "Вы хотите обновить " + appToUpdate.Name + " до версии " + appToUpdate.NewVersion + "?",
                "(  •_•)",
                MessageBoxButton.OKCancel);

            if (result == MessageBoxResult.OK)
            {
                StartUpdateDownload(appToUpdate);
            }
        }

        private async void StartUpdateDownload(UpdateableApp app)
        {
            if (app == null || string.IsNullOrEmpty(app.AppUrl)) return;

            SetLoading(true, "Подготовка к загрузке...");

            try
            {
                // 1. Стучимся в entry.json приложения
                string entryUrl = app.AppUrl.TrimEnd('/') + "/entry.json";
                HttpClient client = new HttpClient();
                string entryJsonText = await client.GetStringAsync(new Uri(entryUrl));

                string appFileName = null;

                // 2. Парсим манифест через нативный Windows.Data.Json
                JsonObject entryRoot;
                if (JsonObject.TryParse(entryJsonText, out entryRoot))
                {
                    if (entryRoot.ContainsKey("versions"))
                    {
                        JsonArray versions = entryRoot.GetNamedArray("versions");

                        // Используем стандартный foreach вместо GetAt
                        foreach (var v in versions)
                        {
                            var versionObj = v.GetObject();
                            string verStr = versionObj.GetNamedString("version");

                            if (verStr == app.NewVersion)
                            {
                                appFileName = versionObj.GetNamedString("app_file");
                                break;
                            }
                        }
                    }

                    // 3. Если файл найден — сохраняем в БД и вызываем FileSavePicker
                    if (!string.IsNullOrEmpty(appFileName))
                    {
                        string downloadUrl = app.AppUrl.TrimEnd('/') + "/" + appFileName;

                        // Обновление installed_apps.json посел обновления
                        try
                        {
                            // Собираем чистый адрес репозитория из AppUrl
                            string repoUrl = "";
                            if (!string.IsNullOrEmpty(app.AppUrl))
                            {
                                var appUri = new Uri(app.AppUrl);
                                repoUrl = appUri.Scheme + "://" + appUri.Host;

                                // Если порт нестандартный (например, http://localhost:8080, что маловероятно), добавляем его
                                if (appUri.Port != 80 && appUri.Port != 443)
                                {
                                    repoUrl += ":" + appUri.Port;
                                }
                            }

                            // Регистрируем в json файле уже с НОВОЙ версией (app.NewVersion)
                            LocalAppsManager.RegisterInstalledApp(
                                app.Id,
                                app.Name,
                                app.Icon,
                                app.NewVersion,
                                repoUrl,
                                app.AppUrl
                            );

                            System.Diagnostics.Debug.WriteLine("Обновление приложения " + app.Name + " зафиксировано в БД.");
                        }
                        catch (Exception dbEx)
                        {
                            System.Diagnostics.Debug.WriteLine("Ошибка записи обновления в базу: " + dbEx.Message);
                        }
                        
                        SetLoading(false);

                        // ИНИЦИАЛИЗАЦИЯ FILE_SAVE_PICKER (Аналогично AppPage)
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
                        savePicker.ContinuationData["AppName"] = app.Name;
                        
                        savePicker.PickSaveFileAndContinue();
                    }
                    else
                    {
                        SetLoading(false);
                        MessageBox.Show("Не удалось найти имя файла для этой версии в манифесте репозитория.", "┐(︶▽︶)┌", MessageBoxButton.OK);
                    }
                }
            }
            catch (Exception ex)
            {
                SetLoading(false);
                MessageBox.Show("Ошибка получения ссылки на обновление: " + ex.Message, "(・・;)ゞ", MessageBoxButton.OK);
            }
        }
        
        private void InstalledListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var listBox = sender as ListBox;
            if (listBox == null || listBox.SelectedItem == null) return;

            var selectedApp = listBox.SelectedItem as TrackedApp;
            
            listBox.SelectedIndex = -1;

            // TODO: пересмотреть решение
            // NavigationService.Navigate(new Uri("/AppPage.xaml?id=" + selectedApp.Id, UriKind.Relative));
        }

        // Систем трей
        private void SetLoading(bool isLoading, string text = "")
        {
            if (SystemTray.ProgressIndicator == null)
            {
                SystemTray.ProgressIndicator = new ProgressIndicator();
            }

            SystemTray.ProgressIndicator.IsVisible = isLoading;
            SystemTray.ProgressIndicator.IsIndeterminate = isLoading;
            SystemTray.ProgressIndicator.Text = text;
        }
        
        private async void ForgetApp_Click(object sender, RoutedEventArgs e)
        {
            var menuItem = sender as MenuItem;
            if (menuItem == null) return;
            
            var appToForget = menuItem.DataContext as TrackedApp;
            if (appToForget == null) return;
            
            MessageBoxResult result = MessageBox.Show(
                "Вы действительно хотите удалить \"" + appToForget.Name + "\" из журнала загрузок?",
                "(¬ ¬ )",
                MessageBoxButton.OKCancel);

            if (result == MessageBoxResult.OK)
            {
                SetLoading(true, "Удаление из журнала...");

                try
                {
                    var apps = await Task.Run(() => LocalAppsManager.LoadApps());
                    apps.RemoveAll(a => a.Id == appToForget.Id);
                    await Task.Run(() => LocalAppsManager.SaveApps(apps));
                    await RefreshInstalledListAsync();
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Ошибка при удалении: " + ex.Message);
                }
                finally
                {
                    SetLoading(false);
                }
            }
        }
        
        private void ViewAppDetails_Click(object sender, RoutedEventArgs e)
        {
            var menuItem = sender as MenuItem;
            if (menuItem == null) return;
            var trackedApp = menuItem.DataContext as TrackedApp;
            if (trackedApp == null) return;
            var appItem = new AppItem
            {
                Id = trackedApp.Id,
                Name = trackedApp.Name,
                Icon = trackedApp.Icon,
                AppUrl = trackedApp.AppUrl
            };
            
            App.SelectedApp = appItem;
            
            NavigationService.Navigate(new Uri("/AppPage.xaml", UriKind.Relative));
        }
    }
}