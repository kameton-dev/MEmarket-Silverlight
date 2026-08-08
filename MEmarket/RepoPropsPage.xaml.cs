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
using System.Threading.Tasks;
using System.Net.Http;
using Newtonsoft.Json.Linq;

namespace MEmarket
{
    public partial class RepoPropsPage : PhoneApplicationPage
    {
        private Repository _currentRepo;

        public RepoPropsPage()
        {
            InitializeComponent();
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            _currentRepo = App.SelectedRepository;
            if (_currentRepo == null) return;

            RepoTitleText.Text = string.IsNullOrEmpty(_currentRepo.Name) ? "репозиторий" : _currentRepo.Name;
            RepoUrlText.Text = _currentRepo.Url ?? "";
            CreatorText.Text = _currentRepo.Creator ?? "...";
            LastUpdatedText.Text = _currentRepo.LastUpdated ?? "...";

            await LoadRepoDetailsAsync(_currentRepo.Url);
        }

        private async Task LoadRepoDetailsAsync(string url)
        {
            try
            {
                var httpClient = new HttpClient();
                string cacheBuster = DateTime.Now.Ticks.ToString();
                var urlWithBuster = url.Contains("?") ? url + "&v=" + cacheBuster : url + "?v=" + cacheBuster;

                var response = await httpClient.GetStringAsync(urlWithBuster);
                var root = JObject.Parse(response);

                if (root["repo_name"] != null)
                {
                    var repoName = root["repo_name"].ToString();
                    if (!string.IsNullOrEmpty(repoName))
                    {
                        RepoTitleText.Text = repoName;
                        if (_currentRepo != null) _currentRepo.Name = repoName;
                    }
                }

                CreatorText.Text = root["creator"] != null
                    ? root["creator"].ToString()
                    : "Не указано";

                LastUpdatedText.Text = root["last_updated"] != null
                    ? root["last_updated"].ToString()
                    : "Не указано";

                App.SaveRepositories();
            }
            catch (Exception)
            {
                CreatorText.Text = "Не удалось загрузить";
                LastUpdatedText.Text = "Не удалось загрузить";
            }
        }

        private void DeleteRepoMenuItem_Click(object sender, EventArgs e)
        {
            if (_currentRepo == null) return;
            
            if (_currentRepo.Url == "http://millenniummarket.ru/index.json")
            {
                MessageBox.Show(MEmarket.Resources.AppResources.BaseRepoMessageContent, MEmarket.Resources.AppResources.BaseRepoMessageTitle, MessageBoxButton.OK);
                return;
            }

            App.Repositories.Remove(_currentRepo);
            App.SaveRepositories();
            App.ViewModel.ResetLoadState();
            NavigationService.GoBack();
        }
    }
}