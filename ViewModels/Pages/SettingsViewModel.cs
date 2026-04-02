using System.Reflection;
using Aquila.Services;
using Wpf.Ui.Abstractions.Controls;
using Wpf.Ui.Appearance;

namespace Aquila.ViewModels.Pages
{
    public partial class SettingsViewModel(UpdateService updateService) : ObservableObject, INavigationAware
    {
        private readonly UpdateService _updateService = updateService;
        private bool _isInitialized = false;

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

        private void InitializeViewModel()
        {
            CurrentTheme = ApplicationThemeManager.GetAppTheme();
            AppVersion = $"Current version: {GetAssemblyVersion()}";
            UpdateStatusMessage = _updateService.StatusMessage;

            _isInitialized = true;
        }

        private static string GetAssemblyVersion() =>
            Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? string.Empty;

        [RelayCommand]
        private void OnChangeTheme(string parameter)
        {
            switch (parameter)
            {
                case "theme_light":
                    if (CurrentTheme == ApplicationTheme.Light)
                        break;

                    ApplicationThemeManager.Apply(ApplicationTheme.Light);
                    CurrentTheme = ApplicationTheme.Light;

                    break;

                default:
                    if (CurrentTheme == ApplicationTheme.Dark)
                        break;

                    ApplicationThemeManager.Apply(ApplicationTheme.Dark);
                    CurrentTheme = ApplicationTheme.Dark;

                    break;
            }
        }

        [RelayCommand]
        private async Task CheckForUpdatesAsync()
        {
            if (IsCheckingForUpdates)
                return;

            IsCheckingForUpdates = true;
            UpdateStatusMessage = "Checking for updates...";

            try
            {
                var checkResult = await _updateService.CheckForUpdatesAsync();

                if (!checkResult.IsSuccess)
                {
                    UpdateStatusMessage = checkResult.Message;
                    MessageBox.Show(checkResult.Message, "Aquila Update", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (!checkResult.IsUpdateAvailable || checkResult.UpdateInfo is null)
                {
                    UpdateStatusMessage = checkResult.Message;
                    return;
                }

                var installNow = MessageBox.Show(
                    "A new Aquila update is available. Download and restart now?",
                    "Aquila Update",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (installNow != MessageBoxResult.Yes)
                {
                    UpdateStatusMessage = "Update available, but installation was cancelled.";
                    return;
                }

                UpdateStatusMessage = "Downloading update...";

                var downloadResult = await _updateService.DownloadUpdateAsync(checkResult.UpdateInfo);

                if (!downloadResult.IsSuccess || downloadResult.UpdateInfo is null)
                {
                    UpdateStatusMessage = downloadResult.Message;
                    MessageBox.Show(downloadResult.Message, "Aquila Update", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                var restartNow = MessageBox.Show(
                    "The update has been downloaded successfully. Restart Aquila now to apply it?",
                    "Aquila Update",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (restartNow != MessageBoxResult.Yes)
                {
                    UpdateStatusMessage = "Update downloaded. Restart the app later to apply it.";
                    return;
                }

                UpdateStatusMessage = "Restarting to apply the update...";
                _updateService.ApplyUpdatesAndRestart(downloadResult.UpdateInfo);
            }
            finally
            {
                IsCheckingForUpdates = false;
            }
        }
    }
}
