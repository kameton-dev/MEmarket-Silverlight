using System;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;
using System.Resources;
using System.Windows;
using System.Windows.Markup;
using System.Windows.Navigation;
using Microsoft.Phone.Controls;
using Microsoft.Phone.Shell;
using MEmarket.Resources;
using MEmarket.ViewModels;
using MEmarket.Models;
using System.IO;
using System.Net.Http;
using System.Runtime.Serialization.Json;
using System.Threading.Tasks;
using System.IO.IsolatedStorage;


namespace MEmarket
{
    public partial class App : Application
    {
        public static List<AppItem> Apps { get; set; }
        public static List<Repository> Repositories { get; set; }
        public static AppItem SelectedApp { get; set; }
        public static CategoryGroup SelectedCategory { get; set; }
        public static Repository SelectedRepository { get; set; }
        public static bool ShowAllOs { get; set; }
        public static bool ShowAppTypeFilter { get; set; }

        private static MainViewModel viewModel = null;

        /// <summary>
        /// A static ViewModel used by the views to bind against.
        /// </summary>
        /// <returns>The MainViewModel object.</returns>
        public static MainViewModel ViewModel
        {
            get
            {
                // Delay creation of the view model until necessary
                if (viewModel == null)
                    viewModel = new MainViewModel();

                return viewModel;
            }
        }

        /// <summary>
        /// Provides easy access to the root frame of the Phone Application.
        /// </summary>
        /// <returns>The root frame of the Phone Application.</returns>
        public static PhoneApplicationFrame RootFrame { get; private set; }

        /// <summary>
        /// Constructor for the Application object.
        /// </summary>
        public App()
        {
            Apps = new List<AppItem>();
            LoadRepositories();

            // Global handler for uncaught exceptions.
            UnhandledException += Application_UnhandledException;

            // Standard XAML initialization
            InitializeComponent();

            // Phone-specific initialization
            InitializePhoneApplication();

            // Language display initialization
            InitializeLanguage();

            // Show graphics profiling information while debugging.
            if (Debugger.IsAttached)
            {
                // Display the current frame rate counters.
                Application.Current.Host.Settings.EnableFrameRateCounter = true;

                // Show the areas of the app that are being redrawn in each frame.
                //Application.Current.Host.Settings.EnableRedrawRegions = true;

                // Enable non-production analysis visualization mode,
                // which shows areas of a page that are handed off to GPU with a colored overlay.
                //Application.Current.Host.Settings.EnableCacheVisualization = true;

                // Prevent the screen from turning off while under the debugger by disabling
                // the application's idle detection.
                // Caution:- Use this under debug mode only. Application that disables user idle detection will continue to run
                // and consume battery power when the user is not using the phone.
                PhoneApplicationService.Current.UserIdleDetectionMode = IdleDetectionMode.Disabled;
            }

        }

        // Code to execute when a contract activation such as a file open or save picker returns 
        // with the picked file or other return values
        private async void Application_ContractActivated(object sender, Windows.ApplicationModel.Activation.IActivatedEventArgs e)
        {
            if (e.Kind == Windows.ApplicationModel.Activation.ActivationKind.PickSaveFileContinuation)
            {
                var fileSavePickerContinuationArgs = e as Windows.ApplicationModel.Activation.FileSavePickerContinuationEventArgs;
                if (fileSavePickerContinuationArgs != null)
                {
                    await ContinueFileSavePicker(fileSavePickerContinuationArgs);
                }
            }
        }

        // Code to execute when the application is launching (eg, from Start)
        // This code will not execute when the application is reactivated
        private void Application_Launching(object sender, LaunchingEventArgs e)
        {
        }

        // Code to execute when the application is activated (brought to foreground)
        // This code will not execute when the application is first launched
        private void Application_Activated(object sender, ActivatedEventArgs e)
        {
            // Ensure that application state is restored appropriately
            if (!App.ViewModel.IsDataLoaded)
            {
                App.ViewModel.LoadData();
            }
        }

        // Code to execute when the application is deactivated (sent to background)
        // This code will not execute when the application is closing
        private void Application_Deactivated(object sender, DeactivatedEventArgs e)
        {
            // Ensure that required application state is persisted here.
        }

        // Code to execute when the application is closing (eg, user hit Back)
        // This code will not execute when the application is deactivated
        private void Application_Closing(object sender, ClosingEventArgs e)
        {
        }

        // Code to execute if a navigation fails
        private void RootFrame_NavigationFailed(object sender, NavigationFailedEventArgs e)
        {
            if (Debugger.IsAttached)
            {
                // A navigation has failed; break into the debugger
                Debugger.Break();
            }
        }

        // Code to execute on Unhandled Exceptions
        private void Application_UnhandledException(object sender, ApplicationUnhandledExceptionEventArgs e)
        {
            if (Debugger.IsAttached)
            {
                // An unhandled exception has occurred; break into the debugger
                Debugger.Break();
            }
        }

        #region Phone application initialization

        // Avoid double-initialization
        private bool phoneApplicationInitialized = false;

        // Do not add any additional code to this method
        private void InitializePhoneApplication()
        {
            if (phoneApplicationInitialized)
                return;

            // Create the frame but don't set it as RootVisual yet; this allows the splash
            // screen to remain active until the application is ready to render.
            RootFrame = new Microsoft.Phone.Controls.TransitionFrame();
            RootFrame.Navigated += CompleteInitializePhoneApplication;

            // Handle navigation failures
            RootFrame.NavigationFailed += RootFrame_NavigationFailed;

            // Handle reset requests for clearing the backstack
            RootFrame.Navigated += CheckForResetNavigation;

            // Handle contract activation such as a file open or save picker
            PhoneApplicationService.Current.ContractActivated += Application_ContractActivated;

            // Ensure we don't initialize again
            phoneApplicationInitialized = true;
        }

        // Do not add any additional code to this method
        private void CompleteInitializePhoneApplication(object sender, NavigationEventArgs e)
        {
            // Set the root visual to allow the application to render
            if (RootVisual != RootFrame)
                RootVisual = RootFrame;

            // Remove this handler since it is no longer needed
            RootFrame.Navigated -= CompleteInitializePhoneApplication;
        }

        private void CheckForResetNavigation(object sender, NavigationEventArgs e)
        {
            // If the app has received a 'reset' navigation, then we need to check
            // on the next navigation to see if the page stack should be reset
            if (e.NavigationMode == NavigationMode.Reset)
                RootFrame.Navigated += ClearBackStackAfterReset;
        }

        private void ClearBackStackAfterReset(object sender, NavigationEventArgs e)
        {
            // Unregister the event so it doesn't get called again
            RootFrame.Navigated -= ClearBackStackAfterReset;

            // Only clear the stack for 'new' (forward) and 'refresh' navigations
            if (e.NavigationMode != NavigationMode.New && e.NavigationMode != NavigationMode.Refresh)
                return;

            // For UI consistency, clear the entire page stack
            while (RootFrame.RemoveBackEntry() != null)
            {
                ; // do nothing
            }
        }

        #endregion

        // Initialize the app's font and flow direction as defined in its localized resource strings.
        //
        // To ensure that the font of your application is aligned with its supported languages and that the
        // FlowDirection for each of those languages follows its traditional direction, ResourceLanguage
        // and ResourceFlowDirection should be initialized in each resx file to match these values with that
        // file's culture. For example:
        //
        // AppResources.es-ES.resx
        //    ResourceLanguage's value should be "es-ES"
        //    ResourceFlowDirection's value should be "LeftToRight"
        //
        // AppResources.ar-SA.resx
        //     ResourceLanguage's value should be "ar-SA"
        //     ResourceFlowDirection's value should be "RightToLeft"
        //
        // For more info on localizing Windows Phone apps see http://go.microsoft.com/fwlink/?LinkId=262072.
        //
        private void InitializeLanguage()
        {
            try
            {
                // Set the font to match the display language defined by the
                // ResourceLanguage resource string for each supported language.
                //
                // Fall back to the font of the neutral language if the Display
                // language of the phone is not supported.
                //
                // If a compiler error is hit then ResourceLanguage is missing from
                // the resource file.
                RootFrame.Language = XmlLanguage.GetLanguage(AppResources.ResourceLanguage);

                // Set the FlowDirection of all elements under the root frame based
                // on the ResourceFlowDirection resource string for each
                // supported language.
                //
                // If a compiler error is hit then ResourceFlowDirection is missing from
                // the resource file.
                FlowDirection flow = (FlowDirection)Enum.Parse(typeof(FlowDirection), AppResources.ResourceFlowDirection);
                RootFrame.FlowDirection = flow;
            }
            catch
            {
                // If an exception is caught here it is most likely due to either
                // ResourceLangauge not being correctly set to a supported language
                // code or ResourceFlowDirection is set to a value other than LeftToRight
                // or RightToLeft.

                if (Debugger.IsAttached)
                {
                    Debugger.Break();
                }

                throw;
            }
        }

        private void LoadRepositories()
        {
            var settings = IsolatedStorageSettings.ApplicationSettings;

            if (settings.Contains("ShowAllOs"))
            {
                ShowAllOs = (bool)settings["ShowAllOs"];
            }
            else
            {
                ShowAllOs = false;
            }

            if (settings.Contains("ShowAppTypeFilter"))
            {
                ShowAppTypeFilter = (bool)settings["ShowAppTypeFilter"];
            }
            else
            {
                ShowAppTypeFilter = true;
            }

            if (settings.Contains("Repositories"))
            {
                var json = settings["Repositories"] as string;
                using (var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(json)))
                {
                    var serializer = new DataContractJsonSerializer(typeof(List<Repository>));
                    Repositories = (List<Repository>)serializer.ReadObject(stream);
                }
            }
            else
            {
                Repositories = new List<Repository>
                {
                    new Repository { Url = "http://millenniummarket.ru/index.json", Name = "MEmarket-Basic" }
                };
            }
        }

        public static void SaveSettings()
        {
            var settings = IsolatedStorageSettings.ApplicationSettings;
            settings["ShowAllOs"] = ShowAllOs;
            settings["ShowAppTypeFilter"] = ShowAppTypeFilter;
            settings.Save();
        }

        public static void SaveRepositories()
        {
            var settings = IsolatedStorageSettings.ApplicationSettings;
            using (var stream = new MemoryStream())
            {
                var serializer = new DataContractJsonSerializer(typeof(List<Repository>));
                serializer.WriteObject(stream, Repositories);
                var bytes = stream.ToArray();
                var json = System.Text.Encoding.UTF8.GetString(bytes, 0, bytes.Length);
                settings["Repositories"] = json;
            }
            settings.Save();
        }

        public static string GetFullUrl(string baseUrl, string relativeOrAbsoluteUrl)
        {
            if (string.IsNullOrEmpty(relativeOrAbsoluteUrl))
            {
                return string.Empty;
            }

            baseUrl = baseUrl?.Trim() ?? string.Empty;
            relativeOrAbsoluteUrl = relativeOrAbsoluteUrl.Trim();

            if (relativeOrAbsoluteUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                relativeOrAbsoluteUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                return relativeOrAbsoluteUrl;
            }

            if (string.IsNullOrEmpty(baseUrl))
            {
                return relativeOrAbsoluteUrl;
            }

            return baseUrl.EndsWith("/") ? baseUrl + relativeOrAbsoluteUrl.TrimStart('/') : baseUrl + "/" + relativeOrAbsoluteUrl.TrimStart('/');
        }

        public static Uri GetFallbackIconUri(Uri sourceUri)
        {
            if (sourceUri == null)
            {
                return null;
            }

            var original = sourceUri.AbsoluteUri;
            if (original.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) || original.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase))
            {
                return new Uri(original.Substring(0, original.LastIndexOf('.')) + ".png");
            }
            if (original.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
            {
                return new Uri(original.Substring(0, original.LastIndexOf('.')) + ".jpg");
            }

            return null;
        }

        private async Task ContinueFileSavePicker(Windows.ApplicationModel.Activation.FileSavePickerContinuationEventArgs args)
        {
            var file = args.File;
            if (file == null) return;

            var downloadUrl = args.ContinuationData["DownloadUrl"] as string;
            var appName = args.ContinuationData["AppName"] as string;

            var currentPage = RootFrame?.Content as PhoneApplicationPage;
            ProgressIndicator progressIndicator = null;
            if (currentPage != null)
            {
                progressIndicator = new ProgressIndicator
                {
                    IsVisible = true,
                    IsIndeterminate = true,
                    Text = "Скачивание " + appName + "..."
                };
                SystemTray.SetProgressIndicator(currentPage, progressIndicator);
            }

            try
            {
                var httpClient = new System.Net.Http.HttpClient();
                using (var response = await httpClient.GetAsync(new Uri(downloadUrl)))
                {
                    response.EnsureSuccessStatusCode();
                    using (var inputStream = await response.Content.ReadAsStreamAsync())
                    using (var fileStream = await file.OpenStreamForWriteAsync())
                    {
                        await inputStream.CopyToAsync(fileStream);
                    }
                }

                //MessageBox.Show($"Приложение {appName} успешно сохранено на устройство.", "Готово", MessageBoxButton.OK);
            }
            catch (Exception ex)
            {
                MessageBox.Show(MEmarket.Resources.AppResources.DownloadingPkgError + ex.Message, "(´∩｀。)", MessageBoxButton.OK);
            }
            finally
            {
                if (currentPage != null && progressIndicator != null)
                {
                    progressIndicator.IsVisible = false;
                    SystemTray.SetProgressIndicator(currentPage, null);
                }
            }
        }
    }
}