using Aquila.Models;
using Aquila.Services;
using Aquila.Services.LibreHardwareMonitor;
using Aquila.ViewModels.Pages;
using Aquila.ViewModels.Windows;
using Aquila.Views.Pages;
using Aquila.Views.Windows;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.IO;
using System.Windows.Media;
using System.Windows.Threading;
using Velopack;
using Wpf.Ui;
using Wpf.Ui.Appearance;
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
            .ConfigureAppConfiguration(c =>
            {
                var basePath = Path.GetDirectoryName(AppContext.BaseDirectory) ?? AppContext.BaseDirectory;
                c.SetBasePath(basePath);
                c.AddJsonFile("appsettings.json", optional: false, reloadOnChange: false);
                c.AddJsonFile("appsettings.local.json", optional: true,  reloadOnChange: false);
            })
            .ConfigureServices((context, services) =>
            {
                services.AddNavigationViewPageProvider();

                services.AddHostedService<ApplicationHostService>();

                // Models
                services.AddSingleton<AquilaState>();

                // Driver —
                services.AddSingleton<IHardwareDriver, LHMDriver>();
                //services.AddSingleton<IHardwareDriver, MockDriver>();

                //Services
                services.AddSingleton<UiService>();
                services.AddSingleton<SettingsService>();
                services.AddSingleton<UpdateService>();
                services.AddSingleton<AquilaService>();

                services.AddSingleton<ISnackbarService, SnackbarService>();

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
                services.AddSingleton<StorageViewModel>();
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

        private static readonly string[] _accentKeys =
        [
            "Aquila.Cpu", "Aquila.Gpu", "Aquila.Ram", "Aquila.Temp", "Aquila.GpuTemp",
            "Aquila.Power", "Aquila.Critical", "Aquila.Gauge.Background",
            "Aquila.Chart.Cpu", "Aquila.Chart.Ram", "Aquila.Chart.Gpu",
            "Aquila.Chart.NetDown", "Aquila.Chart.NetUp"
        ];

        private static void RefreshAccentBrushes()
        {
            var suffix = ApplicationThemeManager.GetAppTheme() == ApplicationTheme.Light ? ".Light" : ".Dark";
            var res = Current.Resources;
            foreach (var key in _accentKeys)
                if (res[key + suffix] is Brush brush) res[key] = brush;
        }

        /// <summary>
        /// Occurs when the application is loading.
        /// </summary>
        private async void OnStartup(object sender, StartupEventArgs e)
        {
            var settings = _host.Services.GetRequiredService<SettingsService>();
            settings.Load();

            ApplicationThemeManager.Changed += (_, _) => Dispatcher.Invoke(RefreshAccentBrushes);

            await _host.StartAsync();

            var theme = settings.Current.Theme == "Light" ? ApplicationTheme.Light : ApplicationTheme.Dark;
            ApplicationThemeManager.Apply(theme);
            RefreshAccentBrushes();

            _host.Services.GetRequiredService<AquilaService>().SetInterval(settings.Current.PollingIntervalMs);
            _ = Services.GetRequiredService<UpdateService>()
                .CheckForUpdatesSilentlyAndNotifyAsync(Services.GetService<ISnackbarService>(), TimeSpan.FromSeconds(2));
            _host.Services.GetRequiredService<AquilaService>();
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
    }
}
