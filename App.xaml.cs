using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.IO;
using System.Reflection;
using System.Windows.Threading;
using Aquila.Services;
using Aquila.ViewModels.Pages;
using Aquila.ViewModels.Windows;
using Aquila.Views.Pages;
using Aquila.Views.Windows;
using Velopack;
using Velopack.Sources;
using Wpf.Ui;
using Wpf.Ui.DependencyInjection;

namespace Aquila
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App
    {
        // The.NET Generic Host provides dependency injection, configuration, logging, and other services.
        // https://docs.microsoft.com/dotnet/core/extensions/generic-host
        // https://docs.microsoft.com/dotnet/core/extensions/dependency-injection
        // https://docs.microsoft.com/dotnet/core/extensions/configuration
        // https://docs.microsoft.com/dotnet/core/extensions/logging
        private static readonly IHost _host = Host
            .CreateDefaultBuilder()
            .ConfigureAppConfiguration(c => { c.SetBasePath(Path.GetDirectoryName(AppContext.BaseDirectory)); })
            .ConfigureServices((context, services) =>
            {
                services.AddNavigationViewPageProvider();

                services.AddHostedService<ApplicationHostService>();

                // Hardware monitoring service
                services.AddSingleton<HardwareMonitorService>();

                // Theme manipulation
                services.AddSingleton<IThemeService, ThemeService>();

                // TaskBar manipulation
                services.AddSingleton<ITaskBarService, TaskBarService>();

                // Service containing navigation, same as INavigationWindow... but without window
                services.AddSingleton<INavigationService, NavigationService>();

                // Main window with navigation
                services.AddSingleton<INavigationWindow, MainWindow>();
                services.AddSingleton<MainWindowViewModel>();

                services.AddSingleton<DashboardPage>();
                services.AddSingleton<DashboardViewModel>();
                services.AddSingleton<ExplorerPage>();
                services.AddSingleton<ExplorerViewModel>();
                services.AddSingleton<StoragePage>();
                services.AddSingleton<AboutPage>();
                services.AddSingleton<AboutViewModel>();
                services.AddSingleton<SettingsPage>();
                services.AddSingleton<SettingsViewModel>();
            }).Build();

        public App()
        {
            try
            {
                // It's important to Run() the VelopackApp as early as possible in app startup.
                VelopackApp.Build().Run();

            }
            catch (Exception ex)
            {
                MessageBox.Show("Velopack Startup Error: " + ex.ToString());
            }
        }

        /// <summary>
        /// Gets services.
        /// </summary>
        public static IServiceProvider Services
        {
            get { return _host.Services; }
        }

        /// <summary>
        /// Occurs when the application is loading.
        /// </summary>
        private async void OnStartup(object sender, StartupEventArgs e)
        {
            await _host.StartAsync();

            StartUpdateCheckInBackground();

        }

        /// <summary>
        /// Occurs when the application is closing.
        /// </summary>
        private async void OnExit(object sender, ExitEventArgs e)
        {
            await _host.StopAsync();

            _host.Dispose();
        }

        /// <summary>
        /// Occurs when an exception is thrown by an application but not handled.
        /// </summary>
        private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            // For more info see https://docs.microsoft.com/en-us/dotnet/api/system.windows.application.dispatcherunhandledexception?view=windowsdesktop-6.0
        }

        private async void StartUpdateCheckInBackground()
        {
            try
            {
                // Executes the update method and WAITS for it INSIDE this thread.
                await UpdateMyApp();
            }
            catch (Exception ex)
            {
                // If UpdateMyApp fails, the exception is caught here
                // and the application DOES NOT CRASH.
                // In the future, you can replace this with a logging system.
                Console.WriteLine($"ERROR: Update check failed. {ex.Message}");
            }
        }
        private static async Task UpdateMyApp()
        {
            try
            {
                var mgr = new UpdateManager(new GithubSource("https://github.com/JoaoCrv/Aquila", null, false));
                var newVersion = await mgr.CheckForUpdatesAsync();

                if (newVersion == null)
                    return;

                await mgr.DownloadUpdatesAsync(newVersion);

                mgr.ApplyUpdatesAndRestart(newVersion);
            }
            catch (Exception ex)
            {
                // This catch handles expected errors from Velopack (e.g., no internet).
                // The catch in the method above handles unexpected errors.
                Console.WriteLine("Update check failed: " + ex.ToString());

                // Rethrows the exception so that the calling method (StartUpdateCheckInBackground) knows that something went wrong.
                throw;
            }
        }
    }
}
