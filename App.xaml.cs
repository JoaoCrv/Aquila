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
using Serilog;
using Serilog.Core;
using Serilog.Events;
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
        internal static LoggingLevelSwitch LogLevel { get; } = new(LogEventLevel.Warning);

        // Held for the primary instance's lifetime to prevent duplicates (the logon task, a manual
        // launch, and the elevation relaunch could otherwise overlap).
        private static System.Threading.Mutex? _instanceMutex;

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

                services.AddSingleton<DashboardWindow>();
                services.AddTransient<DashboardPage>(); // transient: DashboardWindow and MainWindow each get their own instance; ViewModel is the shared singleton
                services.AddSingleton<DashboardViewModel>();
                services.AddSingleton<ExplorerPage>();
                services.AddSingleton<ExplorerViewModel>();
                services.AddSingleton<StoragePage>();
                services.AddSingleton<StorageViewModel>();
                services.AddSingleton<AboutPage>();
                services.AddSingleton<AboutViewModel>();
                services.AddSingleton<SettingsPage>();
                services.AddSingleton<SettingsViewModel>();
            })
            .UseSerilog((_, _, cfg) => cfg
                .MinimumLevel.ControlledBy(App.LogLevel)
                .WriteTo.File(
                    Path.Combine(AquilaPaths.Logs, "aquila-.log"),
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: 7))
            .Build();

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
            // Elevated helper mode: launched only to create the elevation task, then exit.
            if (e.Args.Contains(ElevationService.CreateTaskArg))
            {
                try { ElevationService.CreateTask(); Shutdown(0); }
                catch { Shutdown(1); }
                return;
            }

            // Elevate only if the active hardware driver needs it (LHM does; future API-based
            // drivers may not). In a Velopack install this may relaunch us elevated via the
            // scheduled task and ask this instance to exit.
            var driver = _host.Services.GetRequiredService<IHardwareDriver>();
            if (driver.RequiresElevation && !ElevationService.EnsureElevated())
            {
                Shutdown(0);
                return;
            }

            // Single instance: only the primary holds the mutex. A second launch (manual click while
            // already running, or an overlapping logon/relaunch) exits immediately.
            _instanceMutex = new System.Threading.Mutex(initiallyOwned: true, "Aquila.SingleInstance", out bool isPrimary);
            if (!isPrimary)
            {
                Shutdown(0);
                return;
            }

            var settings = _host.Services.GetRequiredService<SettingsService>();
            settings.Load();

            if (settings.Current.EnableVerboseLogging)
                LogLevel.MinimumLevel = LogEventLevel.Debug;

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
            Log.CloseAndFlush();
        }

        private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            Log.Fatal(e.Exception, "Unhandled UI exception");
            Log.CloseAndFlush();
        }
    }
}
