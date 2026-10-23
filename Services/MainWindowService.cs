using Aquila.Views.Windows;
using Microsoft.Extensions.DependencyInjection;
using Wpf.Ui;

namespace Aquila.Services;

/// <summary>
/// The main window: built when it is opened, destroyed when it is closed, and opened only through here.
///
/// It used to be built, shown and navigated to the dashboard at every start, and then hidden again when Aquila
/// started in the tray — which is how it usually starts. Measured (#50): the hidden window cost ~15 MB and ~30% of
/// Aquila's processor time (18.2 → 12.6% of a core), its dashboard taking every reading for nobody. So it waits
/// until somebody asks for it, and every way of asking comes through <see cref="Open"/>: the tray's double click,
/// its menu and flyout, a notification clicked. Closed to the tray it is destroyed, not hidden (2026-10-23): hiding
/// would have brought that cost back after the first open, and most people set Aquila up once and then live in the
/// tray. It is built again, quickly, the next time.
///
/// The rest of the application gets by without it. The tray icon belongs to no window (<see cref="TrayIcon"/>),
/// and the notice bell, the update badge on Settings and the vitals are view models over services that run
/// regardless, each reading the state as it is when the window is finally built rather than replaying what it
/// missed.
/// </summary>
public sealed class MainWindowService(IServiceProvider services)
{
    private MainWindow? _window;

    /// <summary>The window while it is open, minimised included; null before it is opened and after it is closed.
    /// Never builds it: asking whether there is a window to put back must not be what makes one.</summary>
    public MainWindow? Window => _window;

    /// <summary>Whether it is on screen, not merely built. Minimised and not open both say no — which is what
    /// decides between a balloon and a snackbar nobody would see.</summary>
    public bool IsOnScreen => _window is { IsVisible: true, WindowState: not WindowState.Minimized };

    /// <summary>
    /// Brings the window up: built and on the dashboard when there is none, put back as it was when it is only
    /// minimised.
    /// </summary>
    public void Open()
    {
        var first = _window is null;
        var window = _window ?? Create();

        // Always in the taskbar once it has been opened on purpose — including in dashboard mode. Without a
        // taskbar button the first click on another window buries this one with no way back except the tray,
        // which looks exactly like the window having closed itself. Suppressing the button is only correct
        // while the window is hidden. Set before Show, so the button is not added to a window already up.
        window.ShowInTaskbar = true;
        window.Show();
        if (first) window.Navigate(typeof(Views.Pages.DashboardPage));
        window.Activate();
        window.WindowState = WindowState.Normal;
    }

    /// <summary>
    /// At startup, when Aquila is NOT started in the tray: shown and navigated exactly as it always was, in
    /// whatever state it was left — maximised included, which <see cref="Open"/>'s restore would undo.
    /// </summary>
    public void ShowAtStartup()
    {
        if (_window is not null) return;

        var window = Create();
        window.Show();
        window.Navigate(typeof(Views.Pages.DashboardPage));
    }

    /// <summary>A new window each time — registered transient, since WPF never shows a closed window again.</summary>
    private MainWindow Create()
    {
        var window = (MainWindow)services.GetRequiredService<INavigationWindow>();
        window.Closed += (_, _) =>
        {
            _window = null;
            Claim(); // lets go of the slot, which would otherwise keep the closed window and its pages alive
        };
        _window = window;
        Claim();
        return window;
    }

    /// <summary>
    /// Puts <c>Application.MainWindow</c> on this window — or on nothing, while there is none.
    ///
    /// WPF hands that slot to the FIRST window built, and the kit dresses whatever holds it at every theme
    /// change: <c>ApplicationThemeManager.Apply</c> calls <c>WindowBackgroundManager.UpdateBackground</c> on it,
    /// which removes its background, applies Mica and sets its frame dark or light (read in Wpf.Ui 4.3.0). While
    /// this window was built at every start it was always the first. Started in the tray it no longer is: the
    /// desktop surface comes first, or the dashboard window in dashboard mode, and either would come out of the
    /// next theme change — Windows switching at sunset, say — wearing Mica.
    ///
    /// So it is put right where it is READ: <see cref="AppearanceService"/> calls this before the kit does, and
    /// <see cref="Dialogs"/> asks <see cref="Window"/> rather than the slot. Setting it once would not hold —
    /// while it is empty WPF gives it to the next window built, a flyout or a dialog.
    /// </summary>
    public void Claim()
    {
        if (Application.Current is { } app && !ReferenceEquals(app.MainWindow, _window))
            app.MainWindow = _window;
    }
}
