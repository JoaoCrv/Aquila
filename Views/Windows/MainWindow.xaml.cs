using Aquila.Services;
using Aquila.ViewModels.Windows;
using Microsoft.Extensions.DependencyInjection;
using Wpf.Ui;
using Wpf.Ui.Abstractions;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

namespace Aquila.Views.Windows
{
    public partial class MainWindow : INavigationWindow
    {
        public MainWindowViewModel ViewModel { get; }

        private readonly SettingsService _settings;
        private readonly System.Windows.Forms.NotifyIcon _trayIcon;
        private bool _allowClose = false;

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

            SystemThemeWatcher.Watch(this);

            InitializeComponent();
            SetPageService(navigationViewPageProvider);
            navigationService.SetNavigationControl(RootNavigation);
            snackbarService.SetSnackbarPresenter(SnackbarPresenter);

            _trayIcon = BuildTrayIcon();
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

        private System.Windows.Forms.NotifyIcon BuildTrayIcon()
        {
            // Extract the icon embedded in our own exe (the <ApplicationIcon>), which is
            // always present — copying a loose .ico into the output is unreliable under Velopack.
            System.Drawing.Icon icon;
            try
            {
                var exePath = Environment.ProcessPath ?? System.Reflection.Assembly.GetEntryAssembly()!.Location;
                icon = System.Drawing.Icon.ExtractAssociatedIcon(exePath) ?? System.Drawing.SystemIcons.Application;
            }
            catch
            {
                icon = System.Drawing.SystemIcons.Application;
            }

            var menu = new System.Windows.Forms.ContextMenuStrip();
            menu.Items.Add("Open Aquila", null, (_, _) => TrayOpen());
            menu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
            menu.Items.Add("Exit", null, (_, _) => TrayExit());

            var tray = new System.Windows.Forms.NotifyIcon
            {
                Icon    = icon,
                Text    = "Aquila",
                Visible = true,
                ContextMenuStrip = menu,
            };

            tray.MouseClick  += (_, e) => { if (e.Button == System.Windows.Forms.MouseButtons.Left) TrayClick(); };
            tray.DoubleClick += (_, _) => TrayOpen();

            return tray;
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            if (!_allowClose && (_settings.Current.MinimizeToTray || _settings.Current.DashboardMode))
            {
                e.Cancel = true;
                SaveWindowBounds();
                ShowInTaskbar = false;
                Hide();
                return;
            }

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
            _trayIcon.Visible = false;
            _trayIcon.Dispose();
            base.OnClosed(e);
            Application.Current.Shutdown();
        }

        public void ApplyDashboardMode(bool on)
        {
            ShowInTaskbar = !on;
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

        private void TrayClick()
        {
            if (!_settings.Current.DashboardMode) return;
            if (IsVisible) { SaveWindowBounds(); ShowInTaskbar = false; Hide(); }
            else TrayOpen();
        }

        private void TrayOpen()
        {
            ShowInTaskbar = !_settings.Current.DashboardMode;
            Show();
            Activate();
            WindowState = WindowState.Normal;
        }

        private void TrayExit()
        {
            _allowClose = true;
            Dispatcher.Invoke(Close);
        }

        // Required by INavigationWindow but not used — DI is managed by App.xaml.cs
        void INavigationWindow.SetServiceProvider(IServiceProvider serviceProvider) { }
    }
}
