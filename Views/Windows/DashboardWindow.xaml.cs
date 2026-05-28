using Aquila.Services;
using Aquila.Views.Pages;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Animation;

namespace Aquila.Views.Windows
{
    public partial class DashboardWindow : Window
    {
        private readonly SettingsService _settings;

        public DashboardWindow(DashboardPage dashboardPage, SettingsService settings)
        {
            _settings = settings;
            InitializeComponent();
            RestoreWindowBounds();
            ContentFrame.Navigate(dashboardPage);

            IsVisibleChanged += (_, e) =>
            {
                if (!(bool)e.NewValue) return;
                Opacity = 0;
                BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(180))
                {
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                });
            };
        }

        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonDown(e);
            if (!e.Handled) DragMove();
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            SaveWindowBounds();
            base.OnClosing(e);
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            Application.Current.Shutdown();
        }

        private void RestoreWindowBounds()
        {
            var s = _settings.Current;

            // Always apply stored dimensions (defaults are 900×600 from AppSettings)
            Width  = s.DashboardWindowWidth;
            Height = s.DashboardWindowHeight;

            if (double.IsNaN(s.DashboardWindowLeft)) return; // first run — center on screen

            var rect = new System.Drawing.Rectangle(
                (int)s.DashboardWindowLeft, (int)s.DashboardWindowTop,
                (int)s.DashboardWindowWidth, 40);

            bool onScreen = System.Windows.Forms.Screen.AllScreens
                .Any(sc => sc.WorkingArea.IntersectsWith(rect));

            if (!onScreen) return;

            WindowStartupLocation = WindowStartupLocation.Manual;
            Left = s.DashboardWindowLeft;
            Top  = s.DashboardWindowTop;
        }

        private void SaveWindowBounds()
        {
            var s = _settings.Current;
            s.DashboardWindowLeft   = Left;
            s.DashboardWindowTop    = Top;
            s.DashboardWindowWidth  = Width;
            s.DashboardWindowHeight = Height;
            _settings.Save();
        }
    }
}
