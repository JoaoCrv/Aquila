using Velopack;
using Velopack.Sources;
using Wpf.Ui;
using Wpf.Ui.Controls;

namespace Aquila.Services
{
    public sealed class UpdateService
    {
        private const string DefaultStatusMessage = "Check manually for new Aquila releases.";
        private static readonly GithubSource Source = new("https://github.com/JoaoCrv/Aquila", null, false);
        private readonly SemaphoreSlim _checkLock = new(1, 1);

        public event Action? StatusChanged;

        public bool IsUpdateAvailable { get; private set; }
        public string StatusMessage { get; private set; } = DefaultStatusMessage;
        public UpdateInfo? PendingUpdateInfo { get; private set; }

        public async Task CheckForUpdatesSilentlyAndNotifyAsync(ISnackbarService? snackbarService, TimeSpan? delay = null)
        {
            try
            {
                if (delay is { } startupDelay && startupDelay > TimeSpan.Zero)
                    await Task.Delay(startupDelay);

                var checkResult = await CheckForUpdatesAsync(silent: true);

                if (!checkResult.IsSuccess || !checkResult.IsUpdateAvailable || snackbarService is null)
                    return;

                snackbarService.Show(
                    "Update available",
                    "A new Aquila version is ready. Open Settings to install it.",
                    ControlAppearance.Info,
                    new SymbolIcon { Symbol = SymbolRegular.Info24 },
                    TimeSpan.FromSeconds(8));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Silent update check failed: {ex.Message}");
            }
        }

        public async Task RunUserInitiatedUpdateAsync(
            Func<UpdatePromptRequest, bool> confirmAction,
            Action<UpdatePromptRequest>? notifyAction = null)
        {
            SetStatus("Checking for updates...");

            var checkResult = await CheckForUpdatesAsync();
            if (!checkResult.IsSuccess)
            {
                notifyAction?.Invoke(UpdatePromptRequest.Warning(checkResult.Message));
                return;
            }

            if (!checkResult.IsUpdateAvailable || checkResult.UpdateInfo is null)
                return;

            var installNow = confirmAction(
                UpdatePromptRequest.Confirmation("A new Aquila update is available. Download and restart now?"));

            if (!installNow)
            {
                SetStatus("Update available, but installation was cancelled.");
                return;
            }

            SetStatus("Downloading update...");

            var downloadResult = await DownloadUpdateAsync(checkResult.UpdateInfo);
            if (!downloadResult.IsSuccess || downloadResult.UpdateInfo is null)
            {
                notifyAction?.Invoke(UpdatePromptRequest.Error(downloadResult.Message));
                return;
            }

            var restartNow = confirmAction(
                UpdatePromptRequest.Confirmation("The update has been downloaded successfully. Restart Aquila now to apply it?"));

            if (!restartNow)
            {
                SetStatus("Update downloaded. Restart the app later to apply it.");
                return;
            }

            SetStatus("Restarting to apply the update...");
            ApplyUpdatesAndRestart(downloadResult.UpdateInfo);
        }

        public async Task<UpdateCheckResult> CheckForUpdatesAsync(bool silent = false)
        {
            await _checkLock.WaitAsync();

            try
            {
                var manager = new UpdateManager(Source);
                var updateInfo = await manager.CheckForUpdatesAsync();

                if (updateInfo is null)
                {
                    IsUpdateAvailable = false;
                    PendingUpdateInfo = null;
                    SetStatus("You're already on the latest version.");

                    return UpdateCheckResult.UpToDate(StatusMessage);
                }

                IsUpdateAvailable = true;
                PendingUpdateInfo = updateInfo;
                SetStatus("Update available. Open Settings to download and install it.");

                return UpdateCheckResult.Available(StatusMessage, updateInfo);
            }
            catch (Exception ex)
            {
                IsUpdateAvailable = false;
                PendingUpdateInfo = null;
                SetStatus(silent
                    ? "Automatic update check unavailable. You can still check manually in Settings."
                    : $"Unable to check for updates right now. {ex.Message}");

                return UpdateCheckResult.Failed(StatusMessage);
            }
            finally
            {
                _checkLock.Release();
            }
        }

        public async Task<UpdateDownloadResult> DownloadUpdateAsync(UpdateInfo? updateInfo = null)
        {
            var targetUpdate = updateInfo ?? PendingUpdateInfo;

            if (targetUpdate is null)
                return UpdateDownloadResult.Failed("No pending update is available to download.");

            try
            {
                var manager = new UpdateManager(Source);
                await manager.DownloadUpdatesAsync(targetUpdate);

                SetStatus("Update downloaded successfully. Restart Aquila to apply it.");
                return UpdateDownloadResult.Success("Update downloaded successfully.", targetUpdate);
            }
            catch (Exception ex)
            {
                SetStatus($"The update download failed. {ex.Message}");
                return UpdateDownloadResult.Failed(StatusMessage);
            }
        }

        public void ApplyUpdatesAndRestart(UpdateInfo updateInfo)
        {
            var manager = new UpdateManager(Source);
            manager.ApplyUpdatesAndRestart(updateInfo);
        }

        private void SetStatus(string message)
        {
            StatusMessage = message;
            StatusChanged?.Invoke();
        }
    }

    public sealed record UpdatePromptRequest(string Title, string Message, UpdatePromptKind Kind)
    {
        public static UpdatePromptRequest Confirmation(string message) => new("Aquila Update", message, UpdatePromptKind.Confirmation);
        public static UpdatePromptRequest Warning(string message) => new("Aquila Update", message, UpdatePromptKind.Warning);
        public static UpdatePromptRequest Error(string message) => new("Aquila Update", message, UpdatePromptKind.Error);
    }

    public enum UpdatePromptKind
    {
        Confirmation,
        Warning,
        Error
    }

    public sealed record UpdateCheckResult(bool IsSuccess, bool IsUpdateAvailable, string Message, UpdateInfo? UpdateInfo = null)
    {
        public static UpdateCheckResult UpToDate(string message) => new(true, false, message);
        public static UpdateCheckResult Available(string message, UpdateInfo updateInfo) => new(true, true, message, updateInfo);
        public static UpdateCheckResult Failed(string message) => new(false, false, message);
    }

    public sealed record UpdateDownloadResult(bool IsSuccess, string Message, UpdateInfo? UpdateInfo = null)
    {
        public static UpdateDownloadResult Success(string message, UpdateInfo updateInfo) => new(true, message, updateInfo);
        public static UpdateDownloadResult Failed(string message) => new(false, message);
    }
}
