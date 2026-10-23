using Aquila.Views.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Aquila.Services
{
    public class ApplicationHostService(IServiceProvider serviceProvider, AquilaService aquilaService, ClockService clockService, SettingsService settingsService, MainWindowService mainWindow, ILogger<ApplicationHostService> logger) : IHostedService
    {
        public async Task StartAsync(CancellationToken cancellationToken)
        {
            aquilaService.Start();

            // Beside the poll and before any widget is placed, so the first clock drawn already has the time to
            // follow.
            clockService.Start();
            await serviceProvider.GetRequiredService<ViewModels.Pages.LhmExplorerViewModel>().InitializeAsync();

            // Before any window, and belonging to none: started in the tray, the icon is the only way in.
            serviceProvider.GetRequiredService<TrayIcon>().Show();

            // One setting, the one that says so. Dashboard mode used to force this as well, which is
            // the same hidden coupling that made closing behave oddly — the preset now sets
            // StartMinimized outright, where the user can see it and turn it off.
            //
            // Started in the tray, the main window is not built at all: it used to be built, shown, navigated
            // and hidden again, and that hidden window cost ~30% of Aquila's processor time (#50). It is built
            // when it is opened — see MainWindowService.
            if (settingsService.Current.StartMinimized)
            {
                // Dashboard mode's own window, which the main window put up when it loaded (ApplyDashboardMode).
                // With no main window there is no Loaded, so it is put up here, as the appliance expects.
                if (settingsService.Current.DashboardMode)
                    serviceProvider.GetRequiredService<DashboardWindow>().Show();
            }
            else
            {
                mainWindow.ShowAtStartup();
            }

            if (settingsService.Current.DesktopSurfaceEnabled)
            {
                // Never let a desktop-surface failure take down startup — the surface is optional, the app
                // is not. Win32 z-order work is inherently version-sensitive (see #24).
                try
                {
                    serviceProvider.GetRequiredService<DesktopSurface.DesktopSurfaceService>().Show();
                    serviceProvider.GetRequiredService<DesktopWidgetService>().Populate();
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Desktop surface failed to start");
                }
            }
        }

        /// <summary>
        /// Commits an open widget edit session on the way out.
        ///
        /// Nothing is written to disk while edit mode is open, so closing the app mid-edit would otherwise
        /// lose everything done since it was entered — and edit mode minimizes the window, so it can sit
        /// open for a long time. Between losing deliberate work to a shutdown the user may not have chosen
        /// and keeping changes they were going to keep anyway, keeping them is the smaller mistake. Does
        /// nothing when no session is open.
        /// </summary>
        public Task StopAsync(CancellationToken cancellationToken)
        {
            serviceProvider.GetRequiredService<DesktopWidgetService>().EndEditSession(save: true);
            return Task.CompletedTask;
        }
    }
}
