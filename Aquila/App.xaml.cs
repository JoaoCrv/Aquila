using System.Configuration;
using System.Data;
using System.IO;
using System.Windows;
using Velopack;


namespace Aquila
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        [STAThread]
        private static void Main(string[] args)
        {
            MessageBox.Show("Aquila is starting");
        
            try
            {
                // It's important to Run() the VelopackApp as early as possible in app startup.
                VelopackApp.Build().Run();

                Task.Run(async () => await UpdateMyApp());

                // We can now launch the WPF application as normal.
                var app = new App();
                app.InitializeComponent();
                app.Run();

            }
            catch (Exception ex)
            {
                MessageBox.Show("Unhandled exception: " + ex.ToString());
            }

        }

        private static async Task UpdateMyApp()
        {
            MessageBox.Show("Checking for updates...");
            var mgr = new UpdateManager("https://github.com/JoaoCrv/Aquila/releases");

            // check for new version
            var newVersion = await mgr.CheckForUpdatesAsync();
            MessageBox.Show("Update check complete. New version: " + newVersion);
    
            if (newVersion == null)
                return; // no update available

            // download new version
            await mgr.DownloadUpdatesAsync(newVersion);

            // install new version and restart app
            mgr.ApplyUpdatesAndRestart(newVersion);
        }
    }

}
