using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Security.Principal;
using Microsoft.Win32.TaskScheduler;
using Velopack.Locators;

namespace Aquila.Services;

/// <summary>
/// Elevates Aquila for the LibreHardwareMonitor kernel driver (motherboard node: fans, temps, DIMM
/// details) without a UAC prompt on every launch.
///
/// The app manifest is <c>asInvoker</c>. Elevation comes from a Scheduled Task with
/// <c>RunLevel=Highest</c>: created once (one UAC prompt), then used to relaunch the app elevated
/// with no further prompts. A <c>requireAdministrator</c> manifest is avoided because it breaks
/// Velopack's per-user install and update relaunch (medium integrity cannot silently spawn elevated).
///
/// Scope is deliberately minimal — elevation only. Auto-start at logon is a separate concern.
/// </summary>
public static class ElevationService
{
    public const string TaskName = "Aquila-Elevation";

    /// <summary>Arg passed to the elevated helper instance that only creates the task and exits.</summary>
    public const string CreateTaskArg = "--create-elevation-task";

    public static bool IsElevated()
    {
        using var identity = WindowsIdentity.GetCurrent();
        return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
    }

    /// <summary>
    /// True only when running from a real Velopack install (…\current\Aquila.exe). False in dev
    /// (bin\Debug) — so we never register a task pointing at the dev output.
    /// </summary>
    public static bool IsVelopackInstalled()
    {
        try
        {
            // CurrentlyInstalledVersion is null when running outside a Velopack install (e.g. dev).
            return VelopackLocator.CreateDefaultForPlatform(null).CurrentlyInstalledVersion is not null;
        }
        catch { return false; }
    }

    public static bool TaskExists()
    {
        try
        {
            using var ts = new TaskService();
            return ts.GetTask(TaskName) is not null;
        }
        catch { return false; }
    }

    private static string AppExePath() =>
        Environment.ProcessPath
        ?? Process.GetCurrentProcess().MainModule?.FileName
        ?? throw new InvalidOperationException("Cannot resolve current executable path.");

    /// <summary>Creates the elevation task (RunLevel=Highest, no trigger). MUST run elevated.</summary>
    public static void CreateTask()
    {
        var exe = AppExePath();

        using var ts = new TaskService();
        var td = ts.NewTask();
        td.RegistrationInfo.Description = "Runs Aquila elevated (required by the hardware monitor driver).";
        td.Principal.LogonType = TaskLogonType.InteractiveToken;
        td.Principal.RunLevel  = TaskRunLevel.Highest;
        td.Actions.Add(new ExecAction(exe, null, Path.GetDirectoryName(exe)));
        td.Settings.DisallowStartIfOnBatteries = false;
        td.Settings.StopIfGoingOnBatteries     = false;
        td.Settings.ExecutionTimeLimit         = TimeSpan.Zero;

        ts.RootFolder.RegisterTaskDefinition(TaskName, td);
    }

    /// <summary>Runs the task now (elevates with no UAC prompt). Returns false on failure.</summary>
    public static bool RunTask()
    {
        try
        {
            var psi = new ProcessStartInfo("schtasks", $"/run /tn \"{TaskName}\"")
            {
                UseShellExecute = false,
                CreateNoWindow  = true,
            };
            using var p = Process.Start(psi);
            p?.WaitForExit(5000);
            return p?.ExitCode == 0;
        }
        catch { return false; }
    }

    /// <summary>
    /// Relaunches this exe elevated to create the task (the single UAC prompt).
    /// Returns false if the user declined or it otherwise failed.
    /// </summary>
    public static bool CreateTaskElevated()
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName        = AppExePath(),
                Arguments       = CreateTaskArg,
                UseShellExecute = true,
                Verb            = "runas",
            };
            using var p = Process.Start(psi);
            p?.WaitForExit();
            return p?.ExitCode == 0;
        }
        catch (Win32Exception)
        {
            return false; // ERROR_CANCELLED — user declined UAC.
        }
    }

    /// <summary>
    /// Ensures the app is running elevated. Call early in startup. Returns true if the caller should
    /// CONTINUE running this instance; false if it should exit (a new elevated instance is taking over).
    ///
    /// Logic: in dev or already-elevated, just continue. In a Velopack install running unelevated,
    /// create the task if needed (one UAC prompt) then run it to relaunch elevated and exit. If the
    /// user declines the prompt, continue in degraded mode (no motherboard sensors).
    /// </summary>
    public static bool EnsureElevated()
    {
        if (!IsVelopackInstalled() || IsElevated())
            return true;

        bool taskReady = TaskExists() || CreateTaskElevated();
        if (taskReady && RunTask())
            return false; // elevated instance launched — this one exits

        return true; // UAC declined or task failed — run degraded
    }
}
