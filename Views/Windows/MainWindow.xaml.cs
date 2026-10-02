using Aquila.Services;
using Aquila.ViewModels.Windows;
using Microsoft.Extensions.DependencyInjection;
using Wpf.Ui;
using Wpf.Ui.Abstractions;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

namespace Aquila.Views.Windows
{
    public partial class MainWindow : INavigationWindow, ITrayNotifier
    {
        public MainWindowViewModel ViewModel { get; }

        private readonly SettingsService _settings;
        private readonly System.Windows.Forms.NotifyIcon _trayIcon;
        private readonly UpdateService _updateService;
        private bool _allowClose = false;
        private Action? _balloonClick;

        private System.Drawing.Icon _baseIcon = System.Drawing.SystemIcons.Application;
        private TrayMenu? _trayMenu;
        private TrayFlyout? _trayFlyout;
        private System.Drawing.Icon? _badgedIcon;

        // Tracks normal-state bounds so we always have a valid non-maximized size to persist
        private double _normalLeft = double.NaN, _normalTop = double.NaN, _normalWidth = double.NaN, _normalHeight = double.NaN;

        public MainWindow(
            MainWindowViewModel viewModel,
            INavigationViewPageProvider navigationViewPageProvider,
            INavigationService navigationService,
            ISnackbarService snackbarService,
            SettingsService settings,
            UpdateService updateService)
        {
            ViewModel = viewModel;
            _settings = settings;
            _updateService = updateService;
            DataContext = this;

            // No SystemThemeWatcher here: it applies plain Fluent light or dark the moment Windows
            // switches, which would overwrite whichever theme the follow-Windows pair names.
            // AppearanceService watches that switch instead and applies the chosen theme.

            InitializeComponent();
            SetPageService(navigationViewPageProvider);
            navigationService.SetNavigationControl(RootNavigation);
            snackbarService.SetSnackbarPresenter(SnackbarPresenter);

            _trayIcon = BuildTrayIcon();
            _updateService.StatusChanged += RefreshTrayUpdateBadge;
            RefreshTrayUpdateBadge();

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

            _baseIcon = icon; // kept so the badge can be added and removed without re-extracting

            // A WPF menu rather than the WinForms ContextMenuStrip it replaced, so it wears the theme — see
            // TrayMenu. No ContextMenuStrip is attached to the icon, so a right click does nothing until the
            // handler below opens ours.
            _trayMenu = new TrayMenu(TrayOpen, TrayEditWidgets, TrayInstallUpdate, TrayExit);

            var tray = new System.Windows.Forms.NotifyIcon
            {
                Icon    = icon,
                Text    = "Aquila",
                Visible = true,
            };

            // Left click opens the flyout and right click the menu — the Windows 11 arrangement, where the
            // left button gives an app's own panel and the right one the plain list every icon has.
            tray.MouseClick  += (_, e) => { if (e.Button == System.Windows.Forms.MouseButtons.Left) Flyout().Toggle(); };

            // On the button's RELEASE, which is when Windows opens every other context menu.
            tray.MouseUp     += (_, e) => { if (e.Button == System.Windows.Forms.MouseButtons.Right) _trayMenu.Open(); };
            tray.DoubleClick += (_, _) => TrayOpen();
            // Clicking a notification brings Aquila up — the least surprising outcome, and the only way
            // back for someone running it hidden in the tray.
            tray.BalloonTipClicked += (_, _) =>
            {
                var action = _balloonClick;
                _balloonClick = null;
                if (action is not null) action(); else TrayOpen();
            };

            return tray;
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            // Keyed on one setting only. Dashboard mode used to force this too, which meant a window
            // could refuse to close because of a choice made about a different window; and with
            // minimize-to-tray off, closing is what closing means. Exiting for real is the tray's Exit,
            // and that ends everything — dashboard and desktop widgets included.
            if (!_allowClose && _settings.Current.MinimizeToTray)
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

        /// <summary>
        /// The tray flyout, built on first use and kept.
        ///
        /// A left click used to show or hide this window directly. It opens the flyout now, which carries
        /// "Open Aquila" among the rest — the window is one press further away, and in exchange the click
        /// answers the question someone running Aquila in the tray actually has, which is how the machine is
        /// doing, without a whole window to open and put away again. Double click still opens the window.
        /// </summary>
        private TrayFlyout Flyout()
        {
            if (_trayFlyout is null)
            {
                _trayFlyout = new TrayFlyout(ViewModel.TitleBar, ViewModel.Notices,
                    TrayOpen, TrayEditWidgets, TrayInstallUpdate, TrayExit);
                _trayFlyout.ShowUpdate(_updateService.IsUpdateAvailable);
            }

            return _trayFlyout;
        }

        /// <summary>
        /// Shows a notification from the tray icon. On Windows 10/11 this renders as a normal toast and
        /// lands in the Action Centre, so it survives the user being away from the machine — unlike an
        /// in-window snackbar, which is both invisible while the window is hidden and gone after a few
        /// seconds.
        /// </summary>
        public void Notify(string title, string message, Action? onClick = null)
        {
            // One handler at a time: the click handler captures this specific notification's action, so a
            // stale one would run the wrong thing.
            _balloonClick = onClick;
            _trayIcon.ShowBalloonTip(10_000, title, message, System.Windows.Forms.ToolTipIcon.Info);
        }

        public bool IsWindowVisible => IsVisible && WindowState != WindowState.Minimized;

        /// <summary>
        /// Marks the tray icon while an update is waiting, so the signal survives a missed notification and
        /// is visible even with the window closed — which is how Aquila normally runs.
        /// </summary>
        private void RefreshTrayUpdateBadge()
        {
            Dispatcher.Invoke(() =>
            {
                var available = _updateService.IsUpdateAvailable;

                _trayIcon.Text = available ? "Aquila — update available" : "Aquila";
                _trayIcon.Icon = available ? (_badgedIcon ??= BuildBadgedIcon(_baseIcon)) : _baseIcon;

                _trayMenu?.ShowUpdate(available);
                _trayFlyout?.ShowUpdate(available);
            });
        }

        /// <summary>
        /// Starts a desktop edit session from the tray.
        ///
        /// Resolved when clicked rather than injected: the view model is a singleton either way, and
        /// asking for it in this window's constructor would tie two objects together at startup that have
        /// no reason to know about each other until somebody opens a menu.
        /// </summary>
        private void TrayEditWidgets()
        {
            var widgets = App.Services.GetService(typeof(ViewModels.Pages.WidgetsViewModel))
                as ViewModels.Pages.WidgetsViewModel;

            widgets?.StartEditingCommand.Execute(null);
        }

        /// <summary>The same path the Settings page takes, so there is one update flow and not two.</summary>
        private async void TrayInstallUpdate()
        {
            try
            {
                await _updateService.RunUserInitiatedUpdateAsync();
            }
            catch (Exception ex)
            {
                await Aquila.Services.Dialogs.Tell("Update failed", ex.Message);
            }
        }

        /// <summary>Draws a dot on the app icon. Generated once and cached: Icon.FromHandle wraps a native
        /// HICON that the Icon does not own, so rebuilding it per call would leak a GDI handle each time.</summary>
        private static System.Drawing.Icon BuildBadgedIcon(System.Drawing.Icon baseIcon)
        {
            using var bitmap = new System.Drawing.Bitmap(baseIcon.Width, baseIcon.Height,
                System.Drawing.Imaging.PixelFormat.Format32bppArgb);

            using (var g = System.Drawing.Graphics.FromImage(bitmap))
            {
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                g.DrawIcon(baseIcon, new System.Drawing.Rectangle(0, 0, bitmap.Width, bitmap.Height));

                // Bottom-right, with a light ring so it stays legible against a dark taskbar or a dark icon.
                var size = Math.Max(6f, bitmap.Width / 2.4f);
                var dot = new System.Drawing.RectangleF(bitmap.Width - size, bitmap.Height - size, size, size);

                using var ring = new System.Drawing.SolidBrush(System.Drawing.Color.White);
                using var fill = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(0x2E, 0x9E, 0x4F));
                g.FillEllipse(ring, System.Drawing.RectangleF.Inflate(dot, 1.5f, 1.5f));
                g.FillEllipse(fill, dot);
            }

            var handle = bitmap.GetHicon();
            return System.Drawing.Icon.FromHandle(handle);
        }

        private void TrayOpen()
        {
            // Always in the taskbar once it has been opened on purpose — including in dashboard mode.
            // Without a taskbar button the first click on another window buries this one with no way
            // back except the tray, which looks exactly like the window having closed itself.
            // Suppressing the button is only correct while the window is hidden.
            ShowInTaskbar = true;
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
