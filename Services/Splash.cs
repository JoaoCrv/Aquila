using System.Runtime.CompilerServices;
using System.Windows;

namespace Aquila.Services;

/// <summary>
/// Aquila's loader: a picture on screen from the moment Aquila knows a window is coming until that window has something
/// to show. On a clean VM (2.5.1) the first install had three waits with nothing on screen, read as the installer having
/// failed: 4 s from the installer closing to the first question, 5 s of the elevated helper after it, and 10 s of the
/// elevated Aquila opening LibreHardwareMonitor and building its window.
///
/// WPF's own <see cref="SplashScreen"/>: a picture in a layered window, up before the rest of WPF is loaded, and kept on
/// screen by Windows while the thread that showed it is busy — which is the whole of startup. Kept on top, because a start
/// takes seconds and the person may have gone back to another window.
///
/// Shown just installed, before anything else (<see cref="Program"/>); after the first-run question, while the helper
/// works (<see cref="ElevationService"/>); and at every start that opens the main window (<c>App.OnStartup</c>) — not at
/// a start in the tray, which opens none. Closed once the main window and its page have been drawn (<c>App.OnStartup</c>),
/// and by any dialog, which must never sit under it (<see cref="Dialogs"/>). A process that exits takes it with it — so
/// the unelevated Aquila, which starts the elevated one and exits, first waits for that one to take over
/// (<see cref="HandOverAsync"/>).
/// </summary>
public static class Splash
{
    private static SplashScreen? _shown;

    /// <summary>The named event two Aquilas hand the loader over by (<see cref="HandOverAsync"/>).</summary>
    private const string HandOverName = "Aquila.Loader.HandOver";

    /// <summary>Shows the loader, unless it is up already. Not inlined: <see cref="Program"/>'s Main calls it, and inlined
    /// there it would load WPF before Velopack's hooks had their turn.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void Show()
    {
        if (_shown is not null) return;
        try
        {
            _shown = new SplashScreen("Assets/Images/splash.png");
            _shown.Show(autoClose: false, topMost: true);
        }
        catch
        {
            _shown = null; // a loader that cannot be shown is no reason not to start
        }
    }

    /// <summary>Fades the loader out, if it is up.</summary>
    public static void Close()
    {
        _shown?.Close(TimeSpan.FromMilliseconds(300));
        _shown = null;
    }

    /// <summary>
    /// Starts the Aquila that carries on from this one (<paramref name="start"/>) and keeps this one's loader up until
    /// that one has taken over (<see cref="TakeOver"/>), or 15 s. On a clean VM the elevated Aquila, started through the
    /// scheduled task, spent 5 s starting the runtime, Velopack and the host before it could know whether a window was
    /// coming, and the screen was empty in between. With no loader up — a click on the shortcut starts the unelevated
    /// Aquila without one — there is nothing to keep, and it only starts.
    /// </summary>
    public static async Task<bool> HandOverAsync(Func<bool> start)
    {
        if (_shown is null) return start();

        // Made before the start, so the other Aquila cannot take over before anyone is listening.
        using var takenOver = new EventWaitHandle(false, EventResetMode.ManualReset, HandOverName);
        if (!start()) return false;
        await Task.Run(() => takenOver.WaitOne(TimeSpan.FromSeconds(15)));
        return true;
    }

    /// <summary>
    /// Tells an Aquila waiting in <see cref="HandOverAsync"/> that this one carries on from here: with its own loader up
    /// when a window is coming, with none when it starts in the tray. Nobody waiting is the usual case.
    /// </summary>
    public static void TakeOver()
    {
        try
        {
            if (EventWaitHandle.TryOpenExisting(HandOverName, out var waiting))
                using (waiting) waiting.Set();
        }
        catch { /* the waiting one gives up after its 15 s, and that is all a failure here costs */ }
    }
}
