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
using System.Windows.Media.Imaging;

namespace MEmarket
{
    public partial class MainPage : PhoneApplicationPage
    {
        private ApplicationBarMenuItem _downloadsMenu;

        public MainPage()
        {
            InitializeComponent();
            DataContext = App.ViewModel;
            BuildApplicationBar();
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            if (await App.CheckInjectionActiveAsync() && _downloadsMenu != null &&
                !ApplicationBar.MenuItems.Contains(_downloadsMenu))
            {
                ApplicationBar.MenuItems.Insert(1, _downloadsMenu);
            }

            if (!App.ViewModel.IsDataLoaded)
            {
                App.ViewModel.LoadData();
            }
        }

        private void CategoriesList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var selector = sender as LongListSelector;
            if (selector == null) return;

            var group = selector.SelectedItem as CategoryGroup;
            if (group == null) return;

            App.SelectedCategory = group;
            selector.SelectedItem = null;

            NavigationService.Navigate(new Uri("/CategoryPage.xaml", UriKind.Relative));
        }

        private void AllAppsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var selector = sender as LongListSelector;
            if (selector == null) return;

            var app = selector.SelectedItem as AppItem;
            if (app == null) return;

            App.SelectedApp = app;
            selector.SelectedItem = null;

            NavigationService.Navigate(new Uri("/AppPage.xaml", UriKind.Relative));
        }

        private void FeaturedList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var selector = sender as LongListSelector;
            if (selector == null) return;

            var app = selector.SelectedItem as AppItem;
            if (app == null) return;

            App.SelectedApp = app;
            selector.SelectedItem = null;

            NavigationService.Navigate(new Uri("/AppPage.xaml", UriKind.Relative));
        }

        private void Refresh_Click(object sender, EventArgs e)
        {
            App.ViewModel.ResetLoadState();
            App.ViewModel.LoadData();
        }

        private void Search_Click(object sender, EventArgs e)
        {
            NavigationService.Navigate(new Uri("/SearchPage.xaml", UriKind.Relative));
        }

        private void Settings_Click(object sender, EventArgs e)
        {
            NavigationService.Navigate(new Uri("/SettingsPage.xaml", UriKind.Relative));
        }

        private void InstalledApps_Click(object sender, EventArgs e)
        {
            NavigationService.Navigate(new Uri("/InstalledAppsPage.xaml", UriKind.Relative));
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
            ApplicationBar.Opacity = 0.8;
            
            ApplicationBarMenuItem menuRefresh = new ApplicationBarMenuItem();
            menuRefresh.Text = MEmarket.Resources.AppResources.AppBarRefresh;
            menuRefresh.Click += Refresh_Click;
            ApplicationBar.MenuItems.Add(menuRefresh);
            
            _downloadsMenu = new ApplicationBarMenuItem();
            _downloadsMenu.Text = MEmarket.Resources.AppResources.AppBarDownloads;
            _downloadsMenu.Click += InstalledApps_Click;
            
            ApplicationBarMenuItem menuSettings = new ApplicationBarMenuItem();
            menuSettings.Text = MEmarket.Resources.AppResources.AppBarSettings;
            menuSettings.Click += Settings_Click;
            ApplicationBar.MenuItems.Add(menuSettings);

            ApplicationBarIconButton buttonSearch = new ApplicationBarIconButton();
            buttonSearch.IconUri = new Uri("/Assets/AppBar/feature.search.png", UriKind.Relative);
            buttonSearch.Text = MEmarket.Resources.AppResources.AppBarSearch;
            buttonSearch.Click += Search_Click;
            ApplicationBar.Buttons.Add(buttonSearch);
        }
    }
}