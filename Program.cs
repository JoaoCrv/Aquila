using System.Runtime.CompilerServices;
using Velopack;

namespace Aquila;

/// <summary>
/// The entry point, ahead of WPF's own (<c>StartupObject</c> in the csproj), so Velopack's <c>Run()</c> comes before
/// anything of the application exists.
///
/// The installer runs <c>Aquila.exe --veloapp-install</c> and waits for it — 30 s at most. With <c>Run()</c> in the
/// App's constructor, the process first built the App's static host — the Generic Host, the services, Serilog, the
/// configuration — loading most of what Aquila ships only to say it was a hook. On a freshly installed machine every
/// one of those files is new to the antivirus, and the hook took 28 s of the 30 while the installer's bar stood still
/// (velopack.log on a clean VM, 2.5.0). Here a hook loads Velopack and leaves.
/// </summary>
public static class Program
{
    [STAThread]
    public static void Main()
    {
        Exception? velopackFailure = null;

        try
        {
            // Uninstalling removes the folder, the shortcuts and the uninstall entry, and knows nothing of the
            // elevation task — which runs elevated at every logon, so it must not outlive the app.
            VelopackApp.Build()
                .OnBeforeUninstallFastCallback(_ => Services.ElevationService.DeleteTask())
                .Run();
        }
        catch (Exception ex)
        {
            velopackFailure = ex;
        }

        Start(velopackFailure);
    }

    /// <summary>
    /// Apart from <see cref="Main"/> because the JIT loads every type a method names when it compiles it: with the App
    /// named in Main, WPF itself would be loaded before a hook had its chance to leave.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void Start(Exception? velopackFailure)
    {
        var app = new App { VelopackFailure = velopackFailure };
        app.InitializeComponent();
        app.Run();
    }
}
