using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;
using Microsoft.Phone.Controls;
using Microsoft.Phone.Shell;
using MEmarket.Services;
using MEmarket.Models;

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
            SetLoading(true, MEmarket.Resources.AppResources.CheckingUpdText);
            AvailableUpdates.Clear();

            try
            {
                var localApps = await Task.Run(() => LocalAppsManager.LoadApps());
                var updates = await UpdateChecker.CheckForUpdatesAsync(localApps);

                foreach (var update in updates)
                {
                    AvailableUpdates.Add(update);
                }

                TileManager.UpdateLiveTile(updates);

                if (AvailableUpdates.Count == 0)
                {
                    MessageBox.Show(MEmarket.Resources.AppResources.AllAppsUpdatedMessage, "＼(＾▽＾)／", MessageBoxButton.OK);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(MEmarket.Resources.AppResources.UpdCheckError + ex.Message, "(｡╯︵╰｡)", MessageBoxButton.OK);
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

            SetLoading(true, MEmarket.Resources.AppResources.LoadingText);

            try
            {
                var result = await AppUpdateService.UpdateAsync(
                    app,
                    status => SetLoading(true, status),
                    isInstalling => Dispatcher.BeginInvoke(() =>
                    {
                        if (SystemTray.ProgressIndicator != null)
                        {
                            SystemTray.ProgressIndicator.IsIndeterminate = isInstalling;
                        }
                    }));
                if (result.IsSuccess)
                {
                    AvailableUpdates.Remove(app);
                    MessageBox.Show(app.Name + " успешно обновлено.", "( ˶ˆᗜˆ˵ )", MessageBoxButton.OK);
                    await RefreshInstalledListAsync();
                }
                else
                {
                    string errorCode = result.ErrorCode ?? MEmarket.Resources.AppResources.IdkLol;
                    string errorDescription = result.ErrorDescription ?? MEmarket.Resources.AppResources.IdkLol;
                    string errorMessage = string.Format(
                        MEmarket.Resources.AppResources.DeployingErrorMessageFormat,
                        app.Name,
                        errorCode,
                        errorDescription);
                    MessageBox.Show(errorMessage, "(╥_╥)", MessageBoxButton.OK);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(MEmarket.Resources.AppResources.RetrievingLinkError + ex.Message, "(・・;)ゞ", MessageBoxButton.OK);
            }
            finally
            {
                SetLoading(false);
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
                    await Task.Run(() => LocalAppsManager.ForgetApp(appToForget.Id));
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