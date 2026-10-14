using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Security.AccessControl;
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

    /// <summary>Added to <see cref="CreateTaskArg"/> when the first-run question offered PawnIO and was answered yes.</summary>
    public const string InstallPawnIoArg = "--install-pawnio";

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

        Register(ts, td);
    }

    /// <summary>
    /// Every registration of the task goes through here, so none can leave out the right to delete it.
    /// </summary>
    private static void Register(TaskService ts, TaskDefinition def)
    {
        ts.RootFolder.RegisterTaskDefinition(TaskName, def);
        LetUserDeleteTask();
    }

    /// <summary>
    /// Lets the user the task belongs to DELETE it unelevated — delete only, not change it. Registered by an
    /// elevated process, the task lets its user read (and so run) it and nothing more, and Velopack's
    /// uninstall hook runs unelevated: measured, its delete was refused ("Access denied"), and an uninstall
    /// left behind a task that runs elevated at every logon whatever file sits at a path in LocalAppData —
    /// a path any program of the user's can write to. The cost of the right is that another unelevated
    /// program could delete the task as well, which brings back one UAC prompt and nothing else.
    ///
    /// MUST run elevated. Also called at every elevated start, for tasks registered by a version that did
    /// not grant it — not a one-time migration, because the instance that would run one is often the
    /// unelevated one, which cannot. Adding a right the task already gives changes nothing.
    /// </summary>
    public static void LetUserDeleteTask()
    {
        try
        {
            using var ts = new TaskService();
            var task = ts.GetTask(TaskName);
            if (task is null) return;

            using var me = WindowsIdentity.GetCurrent();
            var security = task.GetAccessControl();
            security.AddAccessRule(new TaskAccessRule(me.User!, TaskRights.Delete, AccessControlType.Allow));
            task.SetAccessControl(security);
        }
        catch { /* elevation still works without it; only the uninstall's clean-up does not */ }
    }

    /// <summary>
    /// Deletes the task, and with it "start with Windows", which is a trigger on it. Velopack's uninstall
    /// hook: it runs unelevated, which is why <see cref="LetUserDeleteTask"/> exists.
    /// </summary>
    public static void DeleteTask()
    {
        try
        {
            using var ts = new TaskService();
            ts.RootFolder.DeleteTask(TaskName, exceptionOnNotExists: false);
        }
        catch { /* an uninstall must never fail on Aquila's account */ }
    }

    /// <summary>Whether the elevation task is set to also run at logon ("start with Windows").</summary>
    public static bool HasLogonTrigger()
    {
        try
        {
            using var ts = new TaskService();
            var task = ts.GetTask(TaskName);
            return task is not null && task.Definition.Triggers.Any(t => t.TriggerType == TaskTriggerType.Logon);
        }
        catch { return false; }
    }

    /// <summary>
    /// Adds or removes a logon trigger on the existing elevation task. Because the running app is
    /// already elevated (launched by the task), this modifies the task with no UAC prompt. Returns
    /// false if the task doesn't exist or the change failed.
    /// </summary>
    public static bool SetLogonTrigger(bool enable)
    {
        try
        {
            using var ts = new TaskService();
            var task = ts.GetTask(TaskName);
            if (task is null) return false;

            var def = task.Definition;
            foreach (var logon in def.Triggers.Where(t => t.TriggerType == TaskTriggerType.Logon).ToList())
                def.Triggers.Remove(logon);
            if (enable)
                def.Triggers.Add(new LogonTrigger { UserId = WindowsIdentity.GetCurrent().Name });

            Register(ts, def);
            return true;
        }
        catch { return false; }
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
    /// Relaunches this exe elevated to create the task (the single UAC prompt) — and to install PawnIO in the same
    /// elevated run when <paramref name="installPawnIo"/>, so one prompt covers both and the driver is in place before
    /// LibreHardwareMonitor first opens. Returns false if the user declined or the task could not be made; a PawnIO
    /// that failed does not fail it (see <see cref="PawnIoService.InstallQuietlyAsync"/>).
    /// </summary>
    public static async Task<bool> CreateTaskElevatedAsync(bool installPawnIo)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName        = AppExePath(),
                Arguments       = installPawnIo ? $"{CreateTaskArg} {InstallPawnIoArg}" : CreateTaskArg,
                UseShellExecute = true,
                Verb            = "runas",
            };
            using var p = Process.Start(psi);
            if (p is null) return false;
            await p.WaitForExitAsync();
            return p.ExitCode == 0;
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
    ///
    /// Before that prompt, one question covering everything the first run needs administrator rights for: the
    /// task, and PawnIO when it looks missing. Asked first so Windows' prompt is expected rather than sprung, and
    /// so a kernel driver is never installed unasked — a UAC prompt says only that Aquila wants to make changes.
    /// It replaces what were a prompt, then a question, then a restart: with PawnIO installed in the same elevated
    /// run, LibreHardwareMonitor finds it on its first open. "Not now" runs degraded, as a declined prompt always
    /// did, and the question comes back at the next start while the task is missing — as the prompt did.
    ///
    /// The caller must keep the application alive while it is asked: before the main window exists, the dialog
    /// is the only window, and WPF's default shutdown mode ends the application when it closes.
    /// </summary>
    public static async Task<bool> EnsureElevatedAsync()
    {
        if (!IsVelopackInstalled() || IsElevated())
            return true;

        bool taskReady = TaskExists();
        if (!taskReady)
        {
            // Unelevated, the driver cannot be asked, only its uninstall entry: a hint. The elevated helper asks the
            // driver itself before installing anything, and the elevated Aquila after it offers PawnIO again if the
            // hint was wrong.
            bool pawnIo = !PawnIoService.LooksInstalled();
            string message =
                "Aquila reads most of your hardware with administrator rights. It sets itself up once to start with "
                + "them, so Windows asks for permission now and not again.";
            if (pawnIo)
                message += $"\n\nIt also installs PawnIO, the signed driver that {PawnIoService.WhatItReads} are read "
                    + "through. It is downloaded from its official release and its signature is checked first.";

            if (!await Dialogs.Ask("Set up Aquila", message, yes: "Set up", no: "Not now"))
                return true; // degraded, as when the prompt was declined

            taskReady = await CreateTaskElevatedAsync(installPawnIo: pawnIo);
        }

        if (taskReady && RunTask())
            return false; // elevated instance launched — this one exits

        return true; // UAC declined or task failed — run degraded
    }
}
