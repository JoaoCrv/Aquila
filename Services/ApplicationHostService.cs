using Aquila.Views.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Wpf.Ui;

namespace Aquila.Services
{
    public class ApplicationHostService(IServiceProvider serviceProvider, AquilaService aquilaService, SettingsService settingsService, ILogger<ApplicationHostService> logger) : IHostedService
    {
        public async Task StartAsync(CancellationToken cancellationToken)
        {
            aquilaService.Start();
            await serviceProvider.GetRequiredService<ViewModels.Pages.LhmExplorerViewModel>().InitializeAsync();

            if (!Application.Current.Windows.OfType<MainWindow>().Any())
            {
                var nav = (serviceProvider.GetRequiredService<INavigationWindow>());
                nav.ShowWindow();
                nav.Navigate(typeof(Views.Pages.DashboardPage));

                // One setting, the one that says so. Dashboard mode used to force this as well, which is
                // the same hidden coupling that made closing behave oddly — the preset now sets
                // StartMinimized outright, where the user can see it and turn it off.
                if (settingsService.Current.StartMinimized)
                    ((System.Windows.Window)nav).Hide();
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
