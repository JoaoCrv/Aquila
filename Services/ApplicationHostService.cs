using Aquila.Views.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Wpf.Ui;

namespace Aquila.Services
{
    public class ApplicationHostService(IServiceProvider serviceProvider, AquilaService aquilaService, SettingsService settingsService) : IHostedService
    {
        public async Task StartAsync(CancellationToken cancellationToken)
        {
            aquilaService.Start();
            await serviceProvider.GetRequiredService<ViewModels.Pages.ExplorerViewModel>().InitializeAsync();

            if (!Application.Current.Windows.OfType<MainWindow>().Any())
            {
                var nav = (serviceProvider.GetRequiredService<INavigationWindow>());
                nav.ShowWindow();
                nav.Navigate(typeof(Views.Pages.DashboardPage));

                if (settingsService.Current.StartMinimized || settingsService.Current.DashboardMode)
                    ((System.Windows.Window)nav).Hide();
            }
        }

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
