using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;
using Microsoft.Phone.Controls;
using Microsoft.Phone.Shell;
using MEmarket.Models;
using MEmarket.Services;
using Microsoft.Phone.Controls.Primitives;
using System.Threading.Tasks;
using System.Net.Http;
using Newtonsoft.Json.Linq;
using System.IO.IsolatedStorage;
using Windows.ApplicationModel;

namespace MEmarket
{
    public partial class SettingsPage : PhoneApplicationPage
    {

        private const string SettingKey = "ShowUpdatesOnTile";

        public SettingsPage()
        {
            InitializeComponent();
            BuildApplicationBar();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            
            RepositoriesList.ItemsSource = null;
            RepositoriesList.ItemsSource = App.Repositories;
            var settings = IsolatedStorageSettings.ApplicationSettings;
            bool isEnabled = false;
            if (settings.Contains(SettingKey))
            {
                isEnabled = (bool)settings[SettingKey];
            }
            
            LiveTileToggle.IsChecked = isEnabled;
            UpdateToggleText(isEnabled);

            AppsFilterToggle.IsChecked = App.ShowAppTypeFilter;
            UpdateAppsFilterToggleText(App.ShowAppTypeFilter);
            DisplayAppVersion();
        }

        private void ShowAllOsToggle_Checked(object sender, RoutedEventArgs e)
        {
            App.ShowAllOs = true;
            App.SaveSettings();
            App.ViewModel.ResetLoadState();
        }

        private void ShowAllOsToggle_Unchecked(object sender, RoutedEventArgs e)
        {
            App.ShowAllOs = false;
            App.SaveSettings();
            App.ViewModel.ResetLoadState();
        }

        private void AppsFilterToggle_Checked(object sender, RoutedEventArgs e)
        {
            App.ShowAppTypeFilter = true;
            App.SaveSettings();
            UpdateAppsFilterToggleText(true);
            App.ViewModel.ResetLoadState();
            App.ViewModel.LoadData();
        }

        private void AppsFilterToggle_Unchecked(object sender, RoutedEventArgs e)
        {
            App.ShowAppTypeFilter = false;
            App.SaveSettings();
            UpdateAppsFilterToggleText(false);
            App.ViewModel.ResetLoadState();
            App.ViewModel.LoadData();
        }

        private void RepositoriesList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var selector = sender as LongListSelector;
            if (selector == null) return;

            var repo = selector.SelectedItem as Repository;
            if (repo == null) return;

            App.SelectedRepository = repo;
            selector.SelectedItem = null; 

            NavigationService.Navigate(new Uri("/RepoPropsPage.xaml", UriKind.Relative));
        }

        private async Task<Tuple<bool, string>> ValidateRepository(string url)
        {
            try
            {
                var httpClient = new HttpClient();
                string cacheBuster = DateTime.Now.Ticks.ToString();

                var urlWithBuster = url;
                if (urlWithBuster.Contains("?"))
                {
                    urlWithBuster += "&v=" + cacheBuster;
                }
                else
                {
                    urlWithBuster += "?v=" + cacheBuster;
                }

                var response = await httpClient.GetStringAsync(urlWithBuster);
                var root = JObject.Parse(response);

                bool hasLastUpdated = root["last_updated"] != null;
                bool hasCreator = root["creator"] != null;
                bool hasRepoName = root["repo_name"] != null;

                if (hasLastUpdated && hasCreator && hasRepoName)
                {
                    string name = root["repo_name"].ToString();
                    return Tuple.Create(true, name);
                }
                return Tuple.Create(false, "");
            }
            catch (Exception)
            {
                return Tuple.Create(false, "");
            }
        }

        private void AddRepoButton_Click(object sender, EventArgs e)
        {
            var textBox = new TextBox { Text = "http://" };
            var messageBox = new CustomMessageBox
            {
                Title = MEmarket.Resources.AppResources.AddRepoMessageTitle,
                Message = MEmarket.Resources.AppResources.AddRepoMessageContent,
                Content = textBox,
                LeftButtonContent = MEmarket.Resources.AppResources.OkButton,
                RightButtonContent = MEmarket.Resources.AppResources.CancelButton
            };

            messageBox.Dismissed += async (s, args) =>
            {
                if (args.Result == CustomMessageBoxResult.LeftButton)
                {
                    string url = textBox.Text.Trim();
                    if (string.IsNullOrEmpty(url)) return;

                    if (App.Repositories.Any(r => string.Equals(r.Url, url, StringComparison.OrdinalIgnoreCase)))
                    {
                        MessageBox.Show(MEmarket.Resources.AppResources.RepoAlrAddedMessage, "(・・ ) ? ", MessageBoxButton.OK);
                        return;
                    }

                    if (!Uri.IsWellFormedUriString(url, UriKind.Absolute) || (!url.StartsWith("http://") && !url.StartsWith("https://")))
                    {
                        MessageBox.Show(MEmarket.Resources.AppResources.RepoUrlError, "(・_・;)", MessageBoxButton.OK);
                        return;
                    }

                    var validation = await ValidateRepository(url);
                    bool isValid = validation.Item1;
                    string repoName = validation.Item2;

                    if (!isValid)
                    {
                        MessageBox.Show(MEmarket.Resources.AppResources.IndexLoadError, "╮(￣～￣)╭ ", MessageBoxButton.OK);
                        return;
                    }

                    var newRepo = new Repository { Url = url, Name = repoName };
                    App.Repositories.Add(newRepo);
                    App.SaveRepositories();
                    
                    RepositoriesList.ItemsSource = null;
                    RepositoriesList.ItemsSource = App.Repositories;
                    
                    App.ViewModel.ResetLoadState();
                }
            };

            messageBox.Show();
        }

        private void LiveTileToggle_Checked(object sender, RoutedEventArgs e)
        {
            SaveSetting(true);
            UpdateToggleText(true);
        }

        private void LiveTileToggle_Unchecked(object sender, RoutedEventArgs e)
        {
            SaveSetting(false);
            UpdateToggleText(false);
            TileManager.ClearTile();
        }

        private void SaveSetting(bool value)
        {
            var settings = IsolatedStorageSettings.ApplicationSettings;
            settings[SettingKey] = value;
            settings.Save();
        }

        private void UpdateToggleText(bool isEnabled)
        {
            if (LiveTileToggle != null)
            {
                LiveTileToggle.Content = isEnabled ? MEmarket.Resources.AppResources.OnText : MEmarket.Resources.AppResources.OffText;
            }
        }

        private void UpdateAppsFilterToggleText(bool isEnabled)
        {
            if (AppsFilterToggle != null)
            {
                AppsFilterToggle.Content = isEnabled ? MEmarket.Resources.AppResources.OnText : MEmarket.Resources.AppResources.OffText;
            }
        }

        private void SettingsPivot_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (this.ApplicationBar != null)
            {
                this.ApplicationBar.IsVisible = (SettingsPivot.SelectedIndex == 1);
            }
        }

        private void BuildApplicationBar()
        {
            ApplicationBar = new ApplicationBar();
            ApplicationBar.IsVisible = false;
            
            ApplicationBarIconButton buttonAdd = new ApplicationBarIconButton();
            buttonAdd.IconUri = new Uri("/Assets/AppBar/new.png", UriKind.Relative);
            buttonAdd.Text = MEmarket.Resources.AppResources.AppBarAdd;
            buttonAdd.Click += AddRepoButton_Click;
            ApplicationBar.Buttons.Add(buttonAdd);
        }

        private void DisplayAppVersion()
        {
            try
            {
                PackageVersion pv = Package.Current.Id.Version;

                string formattedVersion = string.Format("{0}.{1}.{2}.{3}",
                    pv.Major,
                    pv.Minor,
                    pv.Build,
                    pv.Revision);

                AppVersionTextBlock.Text = formattedVersion;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Error reading app version: " + ex.Message);
            }
        }
    }
}