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
    public partial class SearchPage : PhoneApplicationPage
    {
        private List<AppItem> _allApps;

        public SearchPage()
        {
            InitializeComponent();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            _allApps = App.Apps ?? new List<AppItem>();
            PerformSearch();
        }

        private void SearchBoxTextChanged(object sender, TextChangedEventArgs e)
        {
            PerformSearch();
        }

        private void PerformSearch()
        {
            if (_allApps == null) return;

            string query = SearchBox.Text?.Trim().ToLower();

            if (string.IsNullOrEmpty(query))
            {
                SearchResultsList.ItemsSource = _allApps;
                NoResultsText.Visibility = _allApps.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
                return;
            }

            var results = _allApps.Where(app => 
                (app.Name != null && app.Name.ToLower().Contains(query)) ||
                (app.Summary != null && app.Summary.ToLower().Contains(query)) ||
                (app.Publisher != null && app.Publisher.ToLower().Contains(query))
            ).ToList();

            SearchResultsList.ItemsSource = results;
            NoResultsText.Visibility = results.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        private void SearchResultsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var selector = sender as LongListSelector;
            if (selector == null) return;

            var app = selector.SelectedItem as AppItem;
            if (app == null) return;

            App.SelectedApp = app;
            selector.SelectedItem = null;

            NavigationService.Navigate(new Uri("/AppPage.xaml", UriKind.Relative));
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