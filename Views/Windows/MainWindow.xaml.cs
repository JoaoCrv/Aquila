using Aquila.Services;
using Aquila.ViewModels.Windows;
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
            var iconPath = System.IO.Path.Combine(AppContext.BaseDirectory, "Assets", "icon.ico");
            var icon = System.IO.File.Exists(iconPath)
                ? new System.Drawing.Icon(iconPath)
                : System.Drawing.SystemIcons.Application;

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

            tray.DoubleClick += (_, _) => TrayOpen();

            return tray;
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            if (!_allowClose && _settings.Current.MinimizeToTray)
            {
                e.Cancel = true;
                Hide();
                return;
            }
            base.OnClosing(e);
        }

        protected override void OnClosed(EventArgs e)
        {
            _trayIcon.Visible = false;
            _trayIcon.Dispose();
            base.OnClosed(e);
            Application.Current.Shutdown();
        }

        private void TrayOpen()
        {
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
