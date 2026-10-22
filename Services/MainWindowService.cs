using Aquila.Views.Windows;
using Microsoft.Extensions.DependencyInjection;
using Wpf.Ui;

namespace Aquila.Services;

/// <summary>
/// The main window: built the first time it is opened, and opened only through here.
///
/// It used to be built, shown and navigated to the dashboard at every start, and then hidden again when Aquila
/// started in the tray — which is how it usually starts. Measured (#50): that hidden window held 111 MB of 362
/// private, 75 of them graphics-driver pools for a surface nobody was shown; a build that simply skipped it came
/// up at 251. So it waits until somebody asks for it, and every way of asking comes through <see cref="Open"/>:
/// the tray's double click, its menu and flyout, a notification clicked. Once built it stays, hidden to the tray
/// as before — letting it go on hide would be a separate decision.
///
/// The rest of the application gets by without it. The tray icon belongs to no window (<see cref="TrayIcon"/>),
/// and the notice bell, the update badge on Settings and the vitals are view models over services that run
/// regardless, each reading the state as it is when the window is finally built rather than replaying what it
/// missed.
/// </summary>
public sealed class MainWindowService(IServiceProvider services)
{
    private MainWindow? _window;

    /// <summary>The window once it has been opened, null until then. Never builds it: asking whether there is a
    /// window to put back must not be what makes one.</summary>
    public MainWindow? Window => _window;

    /// <summary>Whether it is on screen, not merely built. Hidden in the tray, minimised and never opened all say
    /// no — which is what decides between a balloon and a snackbar nobody would see.</summary>
    public bool IsOnScreen => _window is { IsVisible: true, WindowState: not WindowState.Minimized };

    /// <summary>
    /// Brings the window up: built and on the dashboard the first time, put back as it was every time after.
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

    private MainWindow Create()
    {
        _window = (MainWindow)services.GetRequiredService<INavigationWindow>();
        Claim();
        return _window;
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
