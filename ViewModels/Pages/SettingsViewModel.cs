using System.Collections.Generic;
using System.Reflection;
using System.Windows;
using Aquila.Services;
using Aquila.Views.Windows;
using Microsoft.Win32;
using Serilog.Events;
using Wpf.Ui.Abstractions.Controls;
using Wpf.Ui.Appearance;

namespace Aquila.ViewModels.Pages
{
    public record PollingOption(string Label, int Ms);

    public partial class SettingsViewModel : ObservableObject, INavigationAware, IDisposable
    {
        private const string RunKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
        private const string AppName = "Aquila";

        private readonly UpdateService _updateService;
        private readonly SettingsService _settings;
        private readonly AquilaService _aquila;
        private bool _isInitialized = false;
        private bool _externalUpdate = false;

        public List<PollingOption> PollingIntervalOptions { get; } =
        [
            new("500 ms",  500),
            new("1 s",    1000),
            new("2 s",    2000),
            new("5 s",    5000),
        ];

        [ObservableProperty]
        private PollingOption _selectedPollingInterval = null!;

        public SettingsViewModel(UpdateService updateService, SettingsService settings, AquilaService aquila)
        {
            _updateService = updateService;
            _settings = settings;
            _aquila = aquila;
            _updateService.StatusChanged += OnUpdateStatusChanged;
            _settings.Changed += OnSettingsChangedExternally;
        }

        private void OnSettingsChangedExternally()
        {
            if (!_isInitialized) return;
            _externalUpdate = true;
            DashboardMode  = _settings.Current.DashboardMode;
            MinimizeToTray = _settings.Current.MinimizeToTray;
            _externalUpdate = false;
        }

        [ObservableProperty]
        private ApplicationTheme _currentTheme = ApplicationTheme.Unknown;

        [ObservableProperty]
        private string _appVersion = string.Empty;

        [ObservableProperty]
        private string _updateStatusMessage = "Check manually for new Aquila releases.";

        [ObservableProperty]
        private bool _isCheckingForUpdates;

        [ObservableProperty]
        private bool _minimizeToTray;

        [ObservableProperty]
        private bool _startMinimized;

        [ObservableProperty]
        private bool _startWithWindows;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsNotDashboardMode))]
        private bool _dashboardMode;

        [ObservableProperty]
        private bool _enableVerboseLogging;

        [ObservableProperty] private bool _showCpuCard;
        [ObservableProperty] private bool _showMemoryCard;
        [ObservableProperty] private bool _showNetworkCard;
        [ObservableProperty] private bool _showTemperaturesCard;
        [ObservableProperty] private bool _showPowerCard;
        [ObservableProperty] private bool _showFansCard;
        [ObservableProperty] private bool _showGpuCard;
        [ObservableProperty] private bool _showStorageCard;

        public bool IsNotDashboardMode => !DashboardMode;

        public Task OnNavigatedToAsync()
        {
            if (!_isInitialized)
                InitializeViewModel();

            return Task.CompletedTask;
        }

        public Task OnNavigatedFromAsync() => Task.CompletedTask;

        public void Dispose()
        {
            _updateService.StatusChanged -= OnUpdateStatusChanged;
            _settings.Changed -= OnSettingsChangedExternally;
        }

        private void InitializeViewModel()
        {
            CurrentTheme = ApplicationThemeManager.GetAppTheme();
            AppVersion = $"Current version: {GetAssemblyVersion()}";
            UpdateStatusMessage = _updateService.StatusMessage;
            SelectedPollingInterval =
                PollingIntervalOptions.FirstOrDefault(o => o.Ms == _settings.Current.PollingIntervalMs)
                ?? PollingIntervalOptions[1];

            MinimizeToTray   = _settings.Current.MinimizeToTray;
            StartMinimized   = _settings.Current.StartMinimized;
            StartWithWindows    = IsRegisteredAtStartup();
            DashboardMode       = _settings.Current.DashboardMode;
            EnableVerboseLogging  = _settings.Current.EnableVerboseLogging;

            ShowCpuCard          = _settings.Current.ShowCpuCard;
            ShowMemoryCard       = _settings.Current.ShowMemoryCard;
            ShowNetworkCard      = _settings.Current.ShowNetworkCard;
            ShowTemperaturesCard = _settings.Current.ShowTemperaturesCard;
            ShowPowerCard        = _settings.Current.ShowPowerCard;
            ShowFansCard         = _settings.Current.ShowFansCard;
            ShowGpuCard          = _settings.Current.ShowGpuCard;
            ShowStorageCard      = _settings.Current.ShowStorageCard;

            _isInitialized = true;
        }

        private static string GetAssemblyVersion() =>
            Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? string.Empty;

        private void OnUpdateStatusChanged()
        {
            UpdateStatusMessage = _updateService.StatusMessage;
        }

        [RelayCommand]
        private void OnChangeTheme(string parameter)
        {
            switch (parameter)
            {
                case "theme_light":
                    if (CurrentTheme == ApplicationTheme.Light) break;
                    ApplicationThemeManager.Apply(ApplicationTheme.Light);
                    CurrentTheme = ApplicationTheme.Light;
                    break;

                default:
                    if (CurrentTheme == ApplicationTheme.Dark) break;
                    ApplicationThemeManager.Apply(ApplicationTheme.Dark);
                    CurrentTheme = ApplicationTheme.Dark;
                    break;
            }

            _settings.Current.Theme = CurrentTheme == ApplicationTheme.Light ? "Light" : "Dark";
            _settings.Save();
        }

        partial void OnSelectedPollingIntervalChanged(PollingOption value)
        {
            if (!_isInitialized) return;
            _aquila.SetInterval(value.Ms);
            _settings.Current.PollingIntervalMs = value.Ms;
            _settings.Save();
        }

        partial void OnMinimizeToTrayChanged(bool value)
        {
            if (!_isInitialized || _externalUpdate) return;
            _settings.Current.MinimizeToTray = value;
            _settings.Save();
        }

        partial void OnStartMinimizedChanged(bool value)
        {
            if (!_isInitialized) return;
            _settings.Current.StartMinimized = value;
            _settings.Save();
        }

        partial void OnStartWithWindowsChanged(bool value)
        {
            if (!_isInitialized) return;
            SetRegistryStartup(value);
        }

        partial void OnEnableVerboseLoggingChanged(bool value)
        {
            if (!_isInitialized) return;
            App.LogLevel.MinimumLevel = value ? LogEventLevel.Debug : LogEventLevel.Warning;
            _settings.Current.EnableVerboseLogging = value;
            _settings.Save();
        }

        partial void OnShowGpuCardChanged(bool value)          { if (!_isInitialized) return; _settings.Current.ShowGpuCard          = value; _settings.Save(); }
        partial void OnShowStorageCardChanged(bool value)      { if (!_isInitialized) return; _settings.Current.ShowStorageCard      = value; _settings.Save(); }
        partial void OnShowCpuCardChanged(bool value)          { if (!_isInitialized) return; _settings.Current.ShowCpuCard          = value; _settings.Save(); }
        partial void OnShowMemoryCardChanged(bool value)       { if (!_isInitialized) return; _settings.Current.ShowMemoryCard       = value; _settings.Save(); }
        partial void OnShowNetworkCardChanged(bool value)      { if (!_isInitialized) return; _settings.Current.ShowNetworkCard      = value; _settings.Save(); }
        partial void OnShowTemperaturesCardChanged(bool value) { if (!_isInitialized) return; _settings.Current.ShowTemperaturesCard = value; _settings.Save(); }
        partial void OnShowPowerCardChanged(bool value)        { if (!_isInitialized) return; _settings.Current.ShowPowerCard        = value; _settings.Save(); }
        partial void OnShowFansCardChanged(bool value)         { if (!_isInitialized) return; _settings.Current.ShowFansCard         = value; _settings.Save(); }

        partial void OnDashboardModeChanged(bool value)
        {
            if (!_isInitialized || _externalUpdate) return;
            _settings.Current.DashboardMode = value;
            if (value)
            {
                MinimizeToTray   = true;
                StartWithWindows = true;
            }
            else
            {
                MinimizeToTray   = false;
                StartWithWindows = false;
            }
            _settings.Save();
            Application.Current.Dispatcher.BeginInvoke(() =>
            {
                var mw = Application.Current.Windows.OfType<MainWindow>().FirstOrDefault();
                mw?.ApplyDashboardMode(value);
            });
        }

        private static bool IsRegisteredAtStartup()
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: false);
            return key?.GetValue(AppName) is not null;
        }

        private static void SetRegistryStartup(bool enable)
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true);
            if (key is null) return;
            if (enable)
            {
                var exe = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName ?? string.Empty;
                key.SetValue(AppName, $"\"{exe}\"");
            }
            else
            {
                key.DeleteValue(AppName, throwOnMissingValue: false);
            }
        }

        [RelayCommand]
        private async Task CheckForUpdatesAsync()
        {
            if (IsCheckingForUpdates)
                return;

            IsCheckingForUpdates = true;

            try
            {
                await _updateService.RunUserInitiatedUpdateAsync(ConfirmUpdateAction, ShowUpdateNotification);
            }
            finally
            {
                IsCheckingForUpdates = false;
            }
        }

        private static bool ConfirmUpdateAction(UpdatePromptRequest request) =>
            MessageBox.Show(request.Message, request.Title, MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;

        private static void ShowUpdateNotification(UpdatePromptRequest request)
        {
            var image = request.Kind switch
            {
                UpdatePromptKind.Warning => MessageBoxImage.Warning,
                UpdatePromptKind.Error => MessageBoxImage.Error,
                _ => MessageBoxImage.Information
            };

            MessageBox.Show(request.Message, request.Title, MessageBoxButton.OK, image);
        }
    }
}
