using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.IO;
using System.Reflection;
using System.Windows.Threading;
using UiDesktopApp1.Services;
using UiDesktopApp1.ViewModels.Pages;
using UiDesktopApp1.ViewModels.Windows;
using UiDesktopApp1.Views.Pages;
using UiDesktopApp1.Views.Windows;
using Velopack;
using Velopack.Sources;
using Wpf.Ui;
using Wpf.Ui.DependencyInjection;

namespace UiDesktopApp1
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
                services.AddSingleton<DataPage>();
                services.AddSingleton<DataViewModel>();
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
                // Executa o método de atualização e ESPERA por ele DENTRO desta thread.
                await UpdateMyApp();
            }
            catch (Exception ex)
            {
                // Se o UpdateMyApp falhar, a exceção é apanhada aqui
                // e a aplicação NÃO VAI ABAIXO.
                // No futuro, podes substituir isto por um sistema de logging.
                Console.WriteLine($"ERRO: A verificação de atualização falhou. {ex.Message}");
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
                // Este catch lida com erros esperados do Velopack (ex: sem internet).
                // O catch no método de cima lida com erros inesperados.
                Console.WriteLine("Update check failed: " + ex.ToString());

                // Re-lança a exceção para que o método chamador (StartUpdateCheckInBackground) saiba que algo correu mal.
                throw;
            }
        }
    }
}
