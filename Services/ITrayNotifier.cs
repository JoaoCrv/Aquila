namespace Aquila.Services;

/// <summary>
/// Shows a notification from the tray icon — the only channel that reaches the user regardless of window
/// state. Aquila is normally minimized to the tray (and hidden entirely in dashboard mode), so anything
/// shown inside the main window is invisible exactly when it matters most.
///
/// Implemented by the window that owns the tray icon; callers depend on this interface so they don't have
/// to know which window that is.
/// </summary>
public interface ITrayNotifier
{
    /// <param name="onClick">Invoked if the user clicks the notification. Typically brings the app up.</param>
    void Notify(string title, string message, Action? onClick = null);

    /// <summary>Whether the main window is currently on screen — lets a caller skip an in-window
    /// notification that nobody would see, or skip doubling up when they would.</summary>
    bool IsWindowVisible { get; }
}
