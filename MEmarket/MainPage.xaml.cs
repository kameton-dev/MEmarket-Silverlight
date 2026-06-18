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
        public MainPage()
        {
            InitializeComponent();
            DataContext = App.ViewModel;
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
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
            var listbox = sender as ListBox;
            if (listbox == null) return;

            var app = listbox.SelectedItem as AppItem;
            if (app == null) return;

            App.SelectedApp = app;
            listbox.SelectedItem = null;

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