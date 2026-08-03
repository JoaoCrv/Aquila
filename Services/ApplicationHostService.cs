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

                if (settingsService.Current.StartMinimized || settingsService.Current.DashboardMode)
                    ((System.Windows.Window)nav).Hide();
            }

            if (settingsService.Current.DesktopSurfaceEnabled)
            {
                // Never let a desktop-surface failure take down startup — the surface is optional, the app
                // is not. Win32 z-order work is inherently version-sensitive (see #24).
                try
                {
                    serviceProvider.GetRequiredService<Desktop.DesktopSurfaceService>().Show();
                    serviceProvider.GetRequiredService<DesktopWidgetService>().Populate();
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Desktop surface failed to start");
                }
            }
        }

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
