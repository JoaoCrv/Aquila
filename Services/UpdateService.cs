using Velopack;
using Velopack.Sources;

namespace Aquila.Services
{
    public sealed class UpdateService
    {
        private static readonly GithubSource Source = new("https://github.com/JoaoCrv/Aquila", null, false);
        private readonly SemaphoreSlim _checkLock = new(1, 1);

        public bool IsUpdateAvailable { get; private set; }
        public string StatusMessage { get; private set; } = "Check manually for new Aquila releases.";
        public UpdateInfo? PendingUpdateInfo { get; private set; }

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
                    StatusMessage = "You're already on the latest version.";

                    return UpdateCheckResult.UpToDate(StatusMessage);
                }

                IsUpdateAvailable = true;
                PendingUpdateInfo = updateInfo;
                StatusMessage = "Update available. Open Settings to download and install it.";

                return UpdateCheckResult.Available(StatusMessage, updateInfo);
            }
            catch (Exception ex)
            {
                IsUpdateAvailable = false;
                PendingUpdateInfo = null;
                StatusMessage = silent
                    ? "Automatic update check unavailable. You can still check manually in Settings."
                    : $"Unable to check for updates right now. {ex.Message}";

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

                StatusMessage = "Update downloaded successfully. Restart Aquila to apply it.";
                return UpdateDownloadResult.Success("Update downloaded successfully.", targetUpdate);
            }
            catch (Exception ex)
            {
                StatusMessage = $"The update download failed. {ex.Message}";
                return UpdateDownloadResult.Failed(StatusMessage);
            }
        }

        public void ApplyUpdatesAndRestart(UpdateInfo updateInfo)
        {
            var manager = new UpdateManager(Source);
            manager.ApplyUpdatesAndRestart(updateInfo);
        }
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
