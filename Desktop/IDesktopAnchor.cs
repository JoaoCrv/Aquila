using System.Windows;

namespace Aquila.Desktop;

/// <summary>
/// Strategy for how a screen-canvas window is anchored to the desktop. This is the extensibility seam:
/// today the only implementation is <see cref="ZOrderBottomAnchor"/> (the validated Rainmeter-style
/// "top-level window kept at the bottom of the z-order" model — the only one that renders WPF content
/// AND survives Win+D on current Windows builds). A future "place inside Progman" option would be a
/// second implementation of this interface (a WS_CHILD HwndSource parented to Progman), selected without
/// touching <see cref="DesktopSurfaceService"/> or the windows.
///
/// One anchor instance per window (each holds per-window native state — a message hook, a WinEvent
/// hook). Created through the injected factory in <see cref="DesktopSurfaceService"/>.
/// </summary>
public interface IDesktopAnchor : IDisposable
{
    /// <summary>Anchors an already-shown window (its HWND must exist — call after Window.Show()).</summary>
    void Attach(Window window);
}
