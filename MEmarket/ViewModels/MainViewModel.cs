using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using Newtonsoft.Json.Linq;
using MEmarket.Models;
using MEmarket.Resources;
using System.Net.Http;

namespace MEmarket.ViewModels
{
    public class MainViewModel : INotifyPropertyChanged
    {
        public MainViewModel()
        {
            this.Categories = new ObservableCollection<CategoryGroup>();
            this.AllApps = new ObservableCollection<AppItem>();
            this.Featured = new ObservableCollection<AppItem>();
            this.Items = new ObservableCollection<ItemViewModel>();
        }

        public ObservableCollection<CategoryGroup> Categories { get; private set; }
        public ObservableCollection<AppItem> AllApps { get; private set; }
        public ObservableCollection<AppItem> Featured { get; private set; }
        public ObservableCollection<ItemViewModel> Items { get; private set; }

        private bool _isDataLoaded = false;
        public bool IsDataLoaded
        {
            get { return _isDataLoaded; }
            private set
            {
                if (_isDataLoaded != value)
                {
                    _isDataLoaded = value;
                    NotifyPropertyChanged("IsDataLoaded");
                }
            }
        }

        private bool _isDataLoading = false;
        public bool IsDataLoading
        {
            get { return _isDataLoading; }
            set
            {
                if (_isDataLoading != value)
                {
                    _isDataLoading = value;
                    NotifyPropertyChanged("IsDataLoading");
                }
            }
        }

        public void ResetLoadState()
        {
            IsDataLoaded = false;
        }

        public async void LoadData()
        {
            if (IsDataLoaded || IsDataLoading) return;
            IsDataLoading = true;

            try
            {
                var loadedApps = await Task.Run(() => LoadAllAppsFromRepositoriesAsync());
                
                Deployment.Current.Dispatcher.BeginInvoke(() =>
                {
                    AllApps.Clear();
                    foreach (var app in loadedApps)
                    {
                        AllApps.Add(app);
                    }

                    App.Apps = loadedApps;
                    
                    Categories.Clear();
                    var categoryGroups = CreateCategoryGroups(loadedApps);
                    foreach (var group in categoryGroups)
                    {
                        Categories.Add(group);
                    }
                    
                    Featured.Clear();
                    var random = new Random();
                    var featuredList = loadedApps.OrderBy(x => random.Next()).Take(Math.Min(8, loadedApps.Count)).ToList();
                    foreach (var app in featuredList)
                    {
                        Featured.Add(app);
                    }

                    IsDataLoaded = true;
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Error loading data: " + ex.Message);
            }
            finally
            {
                Deployment.Current.Dispatcher.BeginInvoke(() =>
                {
                    IsDataLoading = false;
                });
            }
        }

        private async Task<List<AppItem>> LoadAllAppsFromRepositoriesAsync()
        {
            var list = new List<AppItem>();
            var httpClient = new HttpClient();

            foreach (var repo in App.Repositories)
            {
                try
                {
                    string cacheBuster = DateTime.Now.Ticks.ToString();
                    var urlWithBuster = repo.Url;
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

                    var packagesObj = root["packages"] as JObject;
                    if (packagesObj != null)
                    {
                        foreach (var kvp in packagesObj)
                        {
                            string key = kvp.Key;
                            var packageObj = kvp.Value as JObject;
                            if (packageObj != null)
                            {
                                string appType = (packageObj["app_type"]?.ToString() ?? "").Trim().ToLower();
                                
                                if (appType != "silverlight" && appType != "winrt")
                                {
                                    continue;
                                }

                                var appItem = new AppItem
                                {
                                    Id = key,
                                    Name = packageObj["title"]?.ToString() ?? string.Empty,
                                    Summary = packageObj["summary"]?.ToString() ?? string.Empty,
                                    AppType = appType,
                                    AppUrl = (packageObj["app_url"]?.ToString() ?? "").Trim(),
                                    Category = (packageObj["category"]?.ToString() ?? "").Trim(),
                                    LatestVersion = packageObj["latest_version"]?.ToString() ?? string.Empty,
                                    RepositoryName = repo.Name
                                };

                                string iconUrl = (packageObj["icon"]?.ToString() ?? "").Trim();
                                appItem.Icon = App.GetFullUrl(appItem.AppUrl, iconUrl);
                                if (string.IsNullOrEmpty(appItem.Icon))
                                {
                                    appItem.Icon = "/Assets/StoreLogo.png";
                                }

                                list.Add(appItem);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine("Exception fetching repo: " + ex.Message);
                }
            }

            return list;
        }

        private List<CategoryGroup> CreateCategoryGroups(List<AppItem> allApps)
        {
            var groups = new List<CategoryGroup>();
            var categories = new[]
            {
                new { Key = "games", Title = "игры" },
                new { Key = "store", Title = "др. магазины приложений" },
                new { Key = "entertainment", Title = "развлечения" },
                new { Key = "music+video", Title = "музыка + видео" },
                new { Key = "tools", Title = "инструменты" },
                new { Key = "livestyle", Title = "лайвстайл" },
                new { Key = "news+weather", Title = "новости + погода" },
                new { Key = "health+fitness", Title = "здоровье + фитнес" },
                new { Key = "photo", Title = "фото" },
                new { Key = "social", Title = "социальные" },
                new { Key = "sports", Title = "спорт" },
                new { Key = "business", Title = "бизнес" },
                new { Key = "education", Title = "обучение" }
            };

            foreach (var cat in categories)
            {
                var items = allApps.Where(app => string.Equals(app.Category, cat.Key, StringComparison.OrdinalIgnoreCase)).ToList();
                if (items.Count > 0)
                {
                    groups.Add(new CategoryGroup { Title = cat.Title, Items = items });
                }
            }
            
            var knownKeys = categories.Select(c => c.Key).ToList();
            var otherItems = allApps.Where(app => !knownKeys.Any(k => string.Equals(app.Category, k, StringComparison.OrdinalIgnoreCase))).ToList();
            if (otherItems.Count > 0)
            {
                groups.Add(new CategoryGroup { Title = "прочее", Items = otherItems });
            }

            return groups;
        }

        public event PropertyChangedEventHandler PropertyChanged;
        private void NotifyPropertyChanged(String propertyName)
        {
            PropertyChangedEventHandler handler = PropertyChanged;
            if (null != handler)
            {
                handler(this, new PropertyChangedEventArgs(propertyName));
            }
        }
    }
}