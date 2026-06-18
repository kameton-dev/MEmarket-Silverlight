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
using Microsoft.Phone.Controls.Primitives;
using System.Threading.Tasks;
using System.Net.Http;
using Newtonsoft.Json.Linq;

namespace MEmarket
{
    public partial class SettingsPage : PhoneApplicationPage
    {
        public SettingsPage()
        {
            InitializeComponent();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            
            RepositoriesList.ItemsSource = null;
            RepositoriesList.ItemsSource = App.Repositories;
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
                Title = "Добавить репозиторий",
                Message = "Введите URL-адрес index.json репозитория:",
                Content = textBox,
                LeftButtonContent = "ок",
                RightButtonContent = "отмена"
            };

            messageBox.Dismissed += async (s, args) =>
            {
                if (args.Result == CustomMessageBoxResult.LeftButton)
                {
                    string url = textBox.Text.Trim();
                    if (string.IsNullOrEmpty(url)) return;

                    if (App.Repositories.Any(r => string.Equals(r.Url, url, StringComparison.OrdinalIgnoreCase)))
                    {
                        MessageBox.Show("Этот репозиторий уже добавлен", "(・・ ) ? ", MessageBoxButton.OK);
                        return;
                    }

                    if (!Uri.IsWellFormedUriString(url, UriKind.Absolute) || (!url.StartsWith("http://") && !url.StartsWith("https://")))
                    {
                        MessageBox.Show("Некорректный URL. Должен начинаться с http:// или https://", "(￢‿￢ ) ", MessageBoxButton.OK);
                        return;
                    }

                    var validation = await ValidateRepository(url);
                    bool isValid = validation.Item1;
                    string repoName = validation.Item2;

                    if (!isValid)
                    {
                        MessageBox.Show("Не удалось загрузить или распознать index.json с этого URL", "╮(￣～￣)╭ ", MessageBoxButton.OK);
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

        private void SettingsPivot_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (this.ApplicationBar != null)
            {
                this.ApplicationBar.IsVisible = (SettingsPivot.SelectedIndex == 1);
            }
        }
    }
}