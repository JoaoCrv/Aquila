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
using System.Windows.Threading;
using Velopack;
using Wpf.Ui;
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
                // Appearance: the preset owns the data colours, the theme owns the window's, and
                // AppearanceService drives both so there is one order of application rather than two.
                services.AddSingleton<PresetService>();
                services.AddSingleton<ThemeCatalog>();
                services.AddSingleton<AppearanceService>();
                services.AddSingleton<NoticeService>();
                services.AddSingleton<UpdateService>();
                services.AddSingleton<VitalMonitor>();
                services.AddSingleton<AquilaService>();

                // Desktop widgets (#24) — domain-agnostic, isolated in Aquila.DesktopSurface. The anchor
                // factory is the single swap point for a future "place inside Progman" strategy.
                services.AddSingleton<Func<DesktopSurface.IDesktopAnchor>>(_ => () => new DesktopSurface.ZOrderBottomAnchor());
                services.AddSingleton<DesktopSurface.DesktopSurfaceService>();
                services.AddSingleton<DesktopLayoutService>();
                services.AddSingleton<DesktopWidgetService>();

                services.AddSingleton<ISnackbarService, SnackbarService>();

                // TaskBar manipulation
                services.AddSingleton<ITaskBarService, TaskBarService>();

                // Service containing navigation, same as INavigationWindow... but without window
                services.AddSingleton<INavigationService, NavigationService>();

                // Main window with navigation
                services.AddSingleton<INavigationWindow, MainWindow>();
                // Same instance under both roles — it's the window that owns the tray icon.
                services.AddSingleton<ITrayNotifier>(sp => (MainWindow)sp.GetRequiredService<INavigationWindow>());
                services.AddSingleton<MainWindowViewModel>();
                services.AddSingleton<TitleBarViewModel>();
                services.AddSingleton<NoticeCenterViewModel>();

                services.AddSingleton<DashboardWindow>();
                services.AddTransient<DashboardPage>(); // transient: DashboardWindow and MainWindow each get their own instance; ViewModel is the shared singleton
                services.AddSingleton<DashboardViewModel>();
                services.AddSingleton<ExplorerPage>();
                services.AddSingleton<ExplorerViewModel>();
                services.AddSingleton<WidgetsPage>();
                services.AddSingleton<WidgetsViewModel>();
                services.AddSingleton<LhmExplorerPage>();
                services.AddSingleton<LhmExplorerViewModel>();
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
                // THE ONE PLACE that still uses the system's message box, and deliberately. Everything
                // else goes through Aquila.Services.Dialogs, which puts up a FluentWindow — and a
                // FluentWindow needs the kit's resources, a dispatcher and a theme, none of which exist
                // yet inside the App constructor. This is the failure that happens before the application
                // does, so it wants the box that works when nothing else does.
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

            // Thirty rather than the library's sixty. This is a monitor: it draws for hours in the corner
            // of a screen, and every frame it spends is taken from the machine it is measuring. Half the
            // frames are not visible in a 300 ms sweep and are plainly visible in a task manager.
            LiveChartsCore.LiveCharts.RenderingSettings.LiveChartsRenderLoopFPS = 30;

            // A static for the same reason as VitalMonitor.Current below: the pieces that animate are
            // controls built by WidgetCatalog and by XAML, and neither can be handed anything by the
            // container.
            Controls.Motion.Apply(settings.Current);

            // Before StartAsync, which is what shows the main window: loads the themes and the presets,
            // applies both, and starts watching Windows' light/dark switch. Doing it after would render
            // the first frame undressed and then correct it in view of the user.
            _host.Services.GetRequiredService<AppearanceService>().Initialize();

            // Before StartAsync too, and for the same reason: the XAML converter that colours the cards
            // reads the limits from this static, having no way to be handed them by the container. Loaded
            // after it would paint the first frame on the built-in limits and correct itself in view.
            var vitals = _host.Services.GetRequiredService<VitalMonitor>();
            vitals.Load();
            VitalMonitor.Current = vitals;

            await _host.StartAsync();

            var aquila = _host.Services.GetRequiredService<AquilaService>();
            aquila.SetInterval(settings.Current.PollingIntervalMs);

            // A new preset or new limits change the colour a reading has earned without changing the reading,
            // so every reading is said again for whatever judges it. Wired here, in the composition root,
            // because neither the appearance nor the limits are the hardware service's business.
            _host.Services.GetRequiredService<AppearanceService>().Changed += aquila.Reannounce;
            vitals.Changed += aquila.Reannounce;

            // The first condition, and the one that made a notice panel worth building: without
            // administrator rights LibreHardwareMonitor cannot open its driver, so temperatures, fan
            // speeds and voltages simply are not there. The app already knew, and said nothing — half the
            // readings were missing and the only explanation available was "this program does not work".
            //
            // Raised once. Elevation cannot change inside a process, so re-checking it would be asking a
            // question whose answer is already written down.
            var notices = _host.Services.GetRequiredService<NoticeService>();
            if (!ElevationService.IsElevated())
                notices.Set(
                    "elevation",
                    "Running without administrator rights",
                    "Temperatures, fan speeds and voltages need them. Settings can set Aquila to start "
                    + "elevated at logon.",
                    Models.StatusKind.Caution);

            _ = Services.GetRequiredService<UpdateService>()
                .CheckForUpdatesSilentlyAndNotifyAsync(
                    Services.GetService<ISnackbarService>(),
                    Services.GetService<ITrayNotifier>(),
                    TimeSpan.FromSeconds(2),
                    notices);
        }

        /// <summary>
        /// Occurs when the application is closing.
        /// </summary>
        private async void OnExit(object sender, ExitEventArgs e)
        {
            // SystemEvents holds a static handler; leaving it hooked keeps the service alive past shutdown.
            _host.Services.GetRequiredService<AppearanceService>().Shutdown();

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
