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
    public partial class CategoryPage : PhoneApplicationPage
    {
        public CategoryPage()
        {
            InitializeComponent();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            if (App.SelectedCategory != null)
            {
                CategoryTitleText.Text = App.SelectedCategory.Title;
                AppsList.ItemsSource = App.SelectedCategory.Items;
            }
        }

        private void AppsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
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