using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Aquila.Views.Pages;
using Aquila.Views.Windows;
using Wpf.Ui;

namespace Aquila.Services
{
    /// <summary>
    /// Managed host of the application.
    /// </summary>
    public class ApplicationHostService(IServiceProvider serviceProvider, HardwareMonitorService hardwareMonitor) : IHostedService
    {
        private readonly IServiceProvider _serviceProvider = serviceProvider;
        private readonly HardwareMonitorService _hardwareMonitor = hardwareMonitor;

        private INavigationWindow? _navigationWindow;

        /// <summary>
        /// Triggered when the application host is ready to start the service.
        /// </summary>
        /// <param name="cancellationToken">Indicates that the start process has been aborted.</param>
        public async Task StartAsync(CancellationToken cancellationToken)
        {
            // Start monitoring first so data is ready when the UI loads
            _hardwareMonitor.StartMonitoring();
            await HandleActivationAsync();
        }

        /// <summary>
        /// Triggered when the application host is performing a graceful shutdown.
        /// </summary>
        /// <param name="cancellationToken">Indicates that the shutdown process should no longer be graceful.</param>
        public async Task StopAsync(CancellationToken cancellationToken)
        {
            _hardwareMonitor.Dispose();
            await Task.CompletedTask;
        }

        /// <summary>
        /// Creates main window during activation.
        /// </summary>
        private async Task HandleActivationAsync()
        {
            if (!Application.Current.Windows.OfType<MainWindow>().Any())
            {
                _navigationWindow = (
                    _serviceProvider.GetService(typeof(INavigationWindow)) as INavigationWindow
                )!;
                _navigationWindow!.ShowWindow();

                _navigationWindow.Navigate(typeof(Views.Pages.DashboardPage));
            }

            await Task.CompletedTask;
        }
    }
}
