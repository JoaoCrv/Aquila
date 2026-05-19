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
        private readonly SettingsService _settings;

        public bool IsLoading => _uiService.IsLoading;

        [ObservableProperty]
        private bool _isDashboardMode;

        public MainWindowViewModel(UiService uiService, SettingsService settings)
        {
            _uiService = uiService;
            _settings  = settings;

            _uiService.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(UiService.IsLoading))
                    OnPropertyChanged(nameof(IsLoading));
            };

            IsDashboardMode = _settings.Current.DashboardMode;
            _settings.Changed += () =>
                Application.Current.Dispatcher.Invoke(
                    () => IsDashboardMode = _settings.Current.DashboardMode);
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

        [ObservableProperty]
        private ObservableCollection<MenuItem> _trayMenuItems =
        [
            new MenuItem { Header = "Open Aquila", Tag = "tray_home" }
        ];
    }
}
