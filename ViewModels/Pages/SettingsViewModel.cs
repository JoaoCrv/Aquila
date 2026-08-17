using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Reflection;
using System.Windows;
using Aquila.Models;
using Aquila.Services;
using Aquila.Views.Windows;
using Serilog.Events;
using Wpf.Ui;
using Wpf.Ui.Abstractions.Controls;

namespace Aquila.ViewModels.Pages
{
    public record PollingOption(string Label, int Ms);

    /// <summary>One entry in the theme list. <c>Id</c> is what goes into settings ("Light", "Dark",
    /// "System"); <c>Label</c> is what the user reads.</summary>
    public record ThemeOption(string Id, string Label);

    public partial class SettingsViewModel : ObservableObject, INavigationAware, IDisposable
    {
        private readonly UpdateService _updateService;
        private readonly SettingsService _settings;
        private readonly AquilaService _aquila;
        private readonly INavigationService _navigation;
        private readonly AppearanceService _theme;
        private readonly ColorProfileService _profiles;
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

        public SettingsViewModel(UpdateService updateService, SettingsService settings, AquilaService aquila,
            INavigationService navigation, AppearanceService theme, ColorProfileService profiles)
        {
            _updateService = updateService;
            _settings = settings;
            _aquila = aquila;
            _navigation = navigation;
            _theme = theme;
            _profiles = profiles;
            _updateService.StatusChanged += OnUpdateStatusChanged;
            _settings.Changed += OnSettingsChangedExternally;
        }

        [RelayCommand]
        private void OpenLhmExplorer() => _navigation.Navigate(typeof(Views.Pages.LhmExplorerPage));

        private void OnSettingsChangedExternally()
        {
            if (!_isInitialized) return;
            _externalUpdate = true;
            DashboardMode  = _settings.Current.DashboardMode;
            MinimizeToTray = _settings.Current.MinimizeToTray;
            // The title bar's toggle writes the same setting this combo shows. Without this the page
            // would keep displaying "Match Windows" long after the toggle had pinned a brightness.
            SelectedTheme  = ThemeOptions.FirstOrDefault(o => o.Id == _settings.Current.Theme)
                             ?? SelectedTheme;
            _externalUpdate = false;
        }

        // ── Appearance ─────────────────────────────────────────────────────────────────────────────
        // Two independent axes, and they stay independent: the THEME dresses the window and is ours, the
        // PROFILE dresses the data and is the user's. Mixing them freely — the new theme with the Sky
        // profile, say — is the point, not an edge case.

        /// <summary>Which look. Kept apart from brightness so the two never become a combinatorial list
        /// with an ambiguous "System" entry in it.</summary>
        public IReadOnlyList<ThemeOption> ThemeStyleOptions { get; } =
        [
            new("Aquila", "Aquila"),
            new("Fluent", "Windows Fluent"),
        ];

        [ObservableProperty] private ThemeOption? _selectedThemeStyle;

        /// <summary>How bright.</summary>
        public IReadOnlyList<ThemeOption> ThemeOptions { get; } =
        [
            new("System", "Match Windows"),
            new("Light", "Light"),
            new("Dark", "Dark"),
        ];

        [ObservableProperty] private ThemeOption? _selectedTheme;

        public ObservableCollection<ColorProfile> ColorProfiles { get; } = [];

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(ColorProfileDescription))]
        private ColorProfile? _selectedColorProfile;

        public string ColorProfileDescription =>
            SelectedColorProfile?.Description ?? "Colours for widgets, cards and charts.";

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
            LoadAppearance();

            AppVersion = $"Current version: {GetAssemblyVersion()}";
            UpdateStatusMessage = _updateService.StatusMessage;
            SelectedPollingInterval =
                PollingIntervalOptions.FirstOrDefault(o => o.Ms == _settings.Current.PollingIntervalMs)
                ?? PollingIntervalOptions[1];

            MinimizeToTray   = _settings.Current.MinimizeToTray;
            StartMinimized   = _settings.Current.StartMinimized;
            StartWithWindows    = ElevationService.HasLogonTrigger();
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

        // ── Appearance ─────────────────────────────────────────────────────────────────────────────

        /// <summary>Fills the pickers from settings without letting them write back — the change handlers
        /// below would otherwise save and re-apply once per assignment during startup.</summary>
        private void LoadAppearance()
        {
            _externalUpdate = true;

            SelectedThemeStyle = ThemeStyleOptions.FirstOrDefault(o => o.Id == _settings.Current.ThemeStyle)
                                 ?? ThemeStyleOptions[0];
            SelectedTheme = ThemeOptions.FirstOrDefault(o => o.Id == _settings.Current.Theme)
                            ?? ThemeOptions[0];

            RefreshProfileList();

            _externalUpdate = false;
        }

        private void RefreshProfileList()
        {
            ColorProfiles.Clear();
            foreach (var profile in _profiles.Profiles) ColorProfiles.Add(profile);

            SelectedColorProfile =
                ColorProfiles.FirstOrDefault(p => p.Id == _settings.Current.ColorProfileId)
                ?? ColorProfiles.FirstOrDefault();
        }

        partial void OnSelectedThemeStyleChanged(ThemeOption? value)
        {
            if (!_isInitialized || _externalUpdate || value is null) return;
            _settings.Current.ThemeStyle = value.Id;
            _settings.Save();
            _theme.Apply();
        }

        partial void OnSelectedThemeChanged(ThemeOption? value)
        {
            if (!_isInitialized || _externalUpdate || value is null) return;
            _settings.Current.Theme = value.Id;
            _settings.Save();
            _theme.Apply();
        }

        partial void OnSelectedColorProfileChanged(ColorProfile? value)
        {
            if (!_isInitialized || _externalUpdate || value is null) return;
            _settings.Current.ColorProfileId = value.Id;
            _settings.Save();
            _theme.ApplyProfile();
        }

        /// <summary>Copies the active profile into the user folder and selects it. Built-ins are read-only,
        /// so this is how one gets customised — and starting from something that already works beats
        /// starting from an empty file and a format to guess at.</summary>
        [RelayCommand]
        private void DuplicateColorProfile()
        {
            if (SelectedColorProfile is null) return;

            try
            {
                var path = _profiles.Duplicate(SelectedColorProfile);
                var id = System.IO.Path.GetFileNameWithoutExtension(path);

                RefreshProfileList();
                SelectedColorProfile = ColorProfiles.FirstOrDefault(p => p.Id == id) ?? SelectedColorProfile;

                OpenProfilesFolder();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"The profile could not be copied.\n\n{ex.Message}",
                    "Colour profiles", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        /// <summary>Re-reads the folder, so an edit made in a text editor shows up without a restart.</summary>
        [RelayCommand]
        private void ReloadColorProfiles()
        {
            _profiles.Load();
            RefreshProfileList();
            _theme.ApplyProfile();
        }

        [RelayCommand]
        private void OpenProfilesFolder()
        {
            System.IO.Directory.CreateDirectory(AquilaPaths.Profiles);
            Process.Start(new ProcessStartInfo(AquilaPaths.Profiles) { UseShellExecute = true });
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
            if (!_isInitialized || _externalUpdate) return;

            // Auto-start rides on the elevation task's logon trigger. The app is already elevated
            // (launched by that task), so this needs no UAC prompt. If the task isn't there (e.g.
            // dev, or elevation was declined), revert and tell the user.
            if (!ElevationService.SetLogonTrigger(value))
            {
                _externalUpdate = true;
                StartWithWindows = !value;
                _externalUpdate = false;

                MessageBox.Show(
                    "Aquila could not change the startup setting. This requires the app to be running with administrator privileges.",
                    "Start with Windows", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
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

            // A preset seeds, it does not lock or undo. Turning it on sets up the appliance in one
            // decision — which is the whole point of it. Turning it off leaves the three settings where
            // they are: someone who wanted "start with Windows" before trying this mode should not lose
            // it by trying it, and the switches stay editable so what the preset did is visible and
            // reversible by hand.
            if (value)
            {
                MinimizeToTray   = true;
                StartMinimized   = true;
                StartWithWindows = true;
            }

            _settings.Save();
            Application.Current.Dispatcher.BeginInvoke(() =>
            {
                var mw = Application.Current.Windows.OfType<MainWindow>().FirstOrDefault();
                mw?.ApplyDashboardMode(value);
            });
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
