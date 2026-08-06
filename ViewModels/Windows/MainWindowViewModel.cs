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

        public bool IsLoading => _uiService.IsLoading;

        public MainWindowViewModel(UiService uiService, UpdateService updateService)
        {
            _uiService = uiService;
            _updateService = updateService;

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

        [ObservableProperty]
        private ObservableCollection<object> _menuItems =
        [
            new NavigationViewItem()
            {
                Content = "Home",
                Icon = new SymbolIcon { Symbol = SymbolRegular.Home24 },
                TargetPageType = typeof(DashboardPage)
            },
            new NavigationViewItem()
            {
                Content = "Explorer",
                Icon = new SymbolIcon { Symbol = SymbolRegular.DataHistogram24 },
                TargetPageType = typeof(ExplorerPage)
            },
            new NavigationViewItem()
            {
                Content = "Widgets",
                Icon = new SymbolIcon { Symbol = SymbolRegular.Grid24 },
                TargetPageType = typeof(WidgetsPage)
            },
            new NavigationViewItem()
            {
                Content = "Storage",
                Icon = new SymbolIcon { Symbol = SymbolRegular.Storage24 },
                TargetPageType = typeof(StoragePage)
            }
        ];

        [ObservableProperty]
        private ObservableCollection<object> _footerMenuItems =
        [
            new NavigationViewItem()
            {
                Content = "About",
                Icon = new SymbolIcon { Symbol = SymbolRegular.Info24 },
                TargetPageType = typeof(AboutPage)
            },
            new NavigationViewItem()
            {
                Content = "Settings",
                Icon = new SymbolIcon { Symbol = SymbolRegular.Settings24 },
                TargetPageType = typeof(SettingsPage)
            }
            
        ];

    }
}
