using System.Collections.ObjectModel;
using Wpf.Ui.Controls;
using Aquila.Views.Pages;
using Aquila.Services;

namespace Aquila.ViewModels.Windows
{
    public partial class MainWindowViewModel : ObservableObject
    {
        [ObservableProperty]
        private string _applicationTitle = "Aquila";

        private readonly UiService _uiService;
        private readonly UpdateService _updateService;
        private readonly AppearanceService _appearance;

        public bool IsLoading => _uiService.IsLoading;

        /// <summary>What the toggle offers, not what is showing: on a dark window it holds a sun,
        /// because the sun is where the click leads.</summary>
        public SymbolRegular ThemeToggleIcon =>
            _appearance.IsDark ? SymbolRegular.WeatherSunny24 : SymbolRegular.WeatherMoon24;

        /// <summary>Hidden for a theme that ships one side. The Settings picker greys out for the same
        /// reason; a toggle that cannot toggle would be the one way left to ask for the impossible.</summary>
        public bool CanToggleTheme => _appearance.CanToggleBrightness;

        public string ThemeToggleTooltip =>
            _appearance.IsDark ? "Switch to light" : "Switch to dark";

        [RelayCommand]
        private void ToggleTheme() => _appearance.ToggleBrightness();

        /// <summary>The title bar's own data. Kept apart so this view model stays about the window.</summary>
        public TitleBarViewModel TitleBar { get; }

        public MainWindowViewModel(UiService uiService, UpdateService updateService,
            AppearanceService appearance, TitleBarViewModel titleBar)
        {
            _uiService = uiService;
            _updateService = updateService;
            _appearance = appearance;
            TitleBar = titleBar;

            // Also fires when the theme changes from Settings, or when Windows switches while we are
            // following it — the button has to agree with the window whoever moved it.
            _appearance.Changed += () =>
            {
                OnPropertyChanged(nameof(ThemeToggleIcon));
                OnPropertyChanged(nameof(ThemeToggleTooltip));
                OnPropertyChanged(nameof(CanToggleTheme));
            };

            _uiService.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(UiService.IsLoading))
                    OnPropertyChanged(nameof(IsLoading));
            };

            _updateService.StatusChanged += RefreshUpdateBadge;
            RefreshUpdateBadge();
        }

        /// <summary>
        /// Marks the Settings item when an update is waiting. Notifications are transient — miss the toast
        /// and nothing tells you again; this persists until the update is installed, and points at the page
        /// where you actually install it.
        /// </summary>
        private void RefreshUpdateBadge()
        {
            var settings = FooterMenuItems.OfType<NavigationViewItem>()
                .FirstOrDefault(i => i.TargetPageType == typeof(SettingsPage));
            if (settings is null) return;

            System.Windows.Application.Current?.Dispatcher.Invoke(() =>
                settings.InfoBadge = _updateService.IsUpdateAvailable
                    ? new InfoBadge { Severity = InfoBadgeSeverity.Attention }
                    : null);
        }

        /// <summary>
        /// Rail glyph size. WPF-UI's default fills the item; the mockup runs 19px, and the smaller glyph
        /// is most of what makes that rail read as quiet rather than busy — the navigation is not the
        /// subject of this window, the data is.
        ///
        /// Set on the icon itself rather than through a style: these items are built in code, so the
        /// size lands as a local value and cannot be overridden by the control template.
        /// </summary>
        private const double RailIconSize = 16;

        private static SymbolIcon RailIcon(SymbolRegular symbol) =>
            new() { Symbol = symbol, FontSize = RailIconSize };

        [ObservableProperty]
        private ObservableCollection<object> _menuItems =
        [
            new NavigationViewItem()
            {
                Content = "Home",
                Icon = RailIcon(SymbolRegular.Home24),
                TargetPageType = typeof(DashboardPage)
            },
            new NavigationViewItem()
            {
                Content = "Explorer",
                // A magnifier, as in the mockup: the page is for finding a sensor among hundreds. The
                // histogram it used to carry described the data, not the act, and collided with the bar
                // chart meaning elsewhere in the app.
                Icon = RailIcon(SymbolRegular.Search24),
                TargetPageType = typeof(ExplorerPage)
            },
            new NavigationViewItem()
            {
                Content = "Widgets",
                Icon = RailIcon(SymbolRegular.Grid24),
                TargetPageType = typeof(WidgetsPage)
            },
            new NavigationViewItem()
            {
                Content = "Storage",
                Icon = RailIcon(SymbolRegular.Storage24),
                TargetPageType = typeof(StoragePage)
            }
        ];

        [ObservableProperty]
        private ObservableCollection<object> _footerMenuItems =
        [
            new NavigationViewItem()
            {
                Content = "About",
                Icon = RailIcon(SymbolRegular.Info24),
                TargetPageType = typeof(AboutPage)
            },
            new NavigationViewItem()
            {
                Content = "Settings",
                Icon = RailIcon(SymbolRegular.Settings24),
                TargetPageType = typeof(SettingsPage)
            }

        ];

    }
}
