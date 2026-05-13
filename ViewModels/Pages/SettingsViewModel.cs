using System.Collections.Generic;
using System.Reflection;
using Aquila.Services;
using Wpf.Ui.Abstractions.Controls;
using Wpf.Ui.Appearance;

namespace Aquila.ViewModels.Pages
{
    public record PollingOption(string Label, int Ms);

    public partial class SettingsViewModel : ObservableObject, INavigationAware, IDisposable
    {
        private readonly UpdateService _updateService;
        private readonly SettingsService _settings;
        private readonly AquilaService _aquila;
        private bool _isInitialized = false;

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
        }

        [ObservableProperty]
        private ApplicationTheme _currentTheme = ApplicationTheme.Unknown;

        [ObservableProperty]
        private string _appVersion = string.Empty;

        [ObservableProperty]
        private string _updateStatusMessage = "Check manually for new Aquila releases.";

        [ObservableProperty]
        private bool _isCheckingForUpdates;

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
        }

        private void InitializeViewModel()
        {
            CurrentTheme = ApplicationThemeManager.GetAppTheme();
            AppVersion = $"Current version: {GetAssemblyVersion()}";
            UpdateStatusMessage = _updateService.StatusMessage;
            SelectedPollingInterval =
                PollingIntervalOptions.FirstOrDefault(o => o.Ms == _settings.Current.PollingIntervalMs)
                ?? PollingIntervalOptions[1];

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

        partial void OnSelectedPollingIntervalChanged(PollingOption? value)
        {
            if (!_isInitialized || value is null) return;
            _aquila.SetInterval(value.Ms);
            _settings.Current.PollingIntervalMs = value.Ms;
            _settings.Save();
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
