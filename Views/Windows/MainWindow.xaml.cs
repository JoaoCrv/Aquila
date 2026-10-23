using Aquila.Services;
using Aquila.ViewModels.Windows;
using Microsoft.Extensions.DependencyInjection;
using Wpf.Ui;
using Wpf.Ui.Abstractions;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

namespace Aquila.Views.Windows
{
    /// <summary>
    /// The main window. Built when it is opened, never at a start in the tray, destroyed when it is closed, and
    /// opened only through <see cref="MainWindowService"/>; the tray icon is not its own any more, see
    /// <see cref="TrayIcon"/>.
    /// </summary>
    public partial class MainWindow : INavigationWindow
    {
        public MainWindowViewModel ViewModel { get; }

        private readonly SettingsService _settings;
        private bool _exiting;

        // Tracks normal-state bounds so we always have a valid non-maximized size to persist
        private double _normalLeft = double.NaN, _normalTop = double.NaN, _normalWidth = double.NaN, _normalHeight = double.NaN;

        public MainWindow(
            MainWindowViewModel viewModel,
            INavigationViewPageProvider navigationViewPageProvider,
            INavigationService navigationService,
            ISnackbarService snackbarService,
            SettingsService settings)
        {
            ViewModel = viewModel;
            _settings = settings;
            DataContext = this;

            // No SystemThemeWatcher here: it applies plain Fluent light or dark the moment Windows
            // switches, which would overwrite whichever theme the follow-Windows pair names.
            // AppearanceService watches that switch instead and applies the chosen theme.

            InitializeComponent();
            SetPageService(new PagesOfThisWindow(navigationViewPageProvider));
            navigationService.SetNavigationControl(RootNavigation);
            snackbarService.SetSnackbarPresenter(SnackbarPresenter);

            RestoreWindowBounds();

            ShowInTaskbar = !_settings.Current.DashboardMode;

            Loaded += (_, _) => ApplyDashboardMode(_settings.Current.DashboardMode);

            SizeChanged     += (_, _) => TrackNormalBounds();
            LocationChanged += (_, _) => TrackNormalBounds();
        }

        #region INavigationWindow methods

        public INavigationView GetNavigation() => RootNavigation;
        public bool Navigate(Type pageType) => RootNavigation.Navigate(pageType);
        public void SetPageService(INavigationViewPageProvider navigationViewPageProvider) => RootNavigation.SetPageProviderService(navigationViewPageProvider);
        public void ShowWindow() => Show();
        public void CloseWindow() => Close();

        #endregion

        /// <summary>Closes for real, past minimize-to-tray: the tray's Exit, which ends everything — dashboard
        /// and desktop widgets included.</summary>
        public void CloseForExit()
        {
            _exiting = true;
            Close();
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            SaveWindowBounds();
            base.OnClosing(e);
        }

        private void TrackNormalBounds()
        {
            if (WindowState != WindowState.Normal) return;
            _normalLeft   = Left;
            _normalTop    = Top;
            _normalWidth  = Width;
            _normalHeight = Height;
        }

        private void RestoreWindowBounds()
        {
            var s = _settings.Current;
            if (double.IsNaN(s.WindowLeft)) return; // first run — keep CenterScreen

            // Validate the title-bar strip is reachable on at least one screen
            var titleRect = new System.Drawing.Rectangle(
                (int)s.WindowLeft, (int)s.WindowTop, (int)s.WindowWidth, 40);

            bool onScreen = System.Windows.Forms.Screen.AllScreens
                .Any(sc => sc.WorkingArea.IntersectsWith(titleRect));

            if (!onScreen) return; // monitor gone — fall back to default

            _normalLeft   = s.WindowLeft;
            _normalTop    = s.WindowTop;
            _normalWidth  = s.WindowWidth;
            _normalHeight = s.WindowHeight;

            WindowStartupLocation = WindowStartupLocation.Manual;
            Left   = s.WindowLeft;
            Top    = s.WindowTop;
            Width  = s.WindowWidth;
            Height = s.WindowHeight;

            if (s.WindowMaximized)
                WindowState = WindowState.Maximized;
        }

        private void SaveWindowBounds()
        {
            var s = _settings.Current;
            s.WindowMaximized = WindowState == WindowState.Maximized && !_settings.Current.DashboardMode;
            s.WindowLeft      = !double.IsNaN(_normalLeft)   ? _normalLeft   : Left;
            s.WindowTop       = !double.IsNaN(_normalTop)    ? _normalTop    : Top;
            s.WindowWidth     = !double.IsNaN(_normalWidth)  && _normalWidth  > 0 ? _normalWidth  : Width;
            s.WindowHeight    = !double.IsNaN(_normalHeight) && _normalHeight > 0 ? _normalHeight : Height;
            _settings.Save();
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);

            // Keyed on one setting only. Dashboard mode used to force this too, which meant a window could refuse
            // to close because of a choice made about a different window. With minimize-to-tray on, closing ends
            // this window and nothing else — destroyed, not hidden (see MainWindowService), and the tray icon goes
            // with the application, not with it (see TrayIcon). Off, closing is what closing means. Exiting for real
            // is the tray's Exit, and that ends everything — dashboard and desktop widgets included.
            if (_exiting || !_settings.Current.MinimizeToTray)
                Application.Current.Shutdown();
        }

        /// <summary>
        /// One instance of each page for the life of this window, as the singletons they were gave before. The kit
        /// asks for the page on every navigation, and a page belongs to the window that shows it — so they cannot
        /// outlive it as singletons, or the next window would be handed a page still parented to the closed one.
        /// Their view models stay singletons: what a page shows survives the window.
        /// </summary>
        private sealed class PagesOfThisWindow(INavigationViewPageProvider pages) : INavigationViewPageProvider
        {
            private readonly Dictionary<Type, object?> _built = [];

            public object? GetPage(Type pageType) =>
                _built.TryGetValue(pageType, out var page) ? page : _built[pageType] = pages.GetPage(pageType);
        }

        public void ApplyDashboardMode(bool on)
        {
            // Same rule: never take the taskbar button away from a window that is on screen. Switching
            // into dashboard mode from Settings would otherwise strand the very window being used.
            ShowInTaskbar = !on || IsVisible;
            if (on)
            {
                var dw = App.Services.GetRequiredService<DashboardWindow>();
                if (!dw.IsVisible) dw.Show();
            }
            else
            {
                // Only interact with DashboardWindow if it was already created
                Application.Current.Windows.OfType<DashboardWindow>().FirstOrDefault()?.Hide();

                if (!IsVisible)
                {
                    // Transitioning from dashboard mode back to normal
                    ShowInTaskbar = true;
                    Show();
                    Activate();
                    WindowState = WindowState.Normal;
                    RootNavigation.Navigate(typeof(Views.Pages.DashboardPage));
                    if (!double.IsNaN(_normalLeft))
                    {
                        Left   = _normalLeft;
                        Top    = _normalTop;
                        Width  = _normalWidth;
                        Height = _normalHeight;
                    }
                }
            }
        }

        // Required by INavigationWindow but not used — DI is managed by App.xaml.cs
        void INavigationWindow.SetServiceProvider(IServiceProvider serviceProvider) { }
    }
}
