using System.Windows.Controls;
using Microsoft.Extensions.Logging;

namespace Aquila.Desktop;

/// <summary>
/// Owns the desktop-surface windows: one <see cref="ScreenCanvasWindow"/> per physical screen, each
/// anchored to the desktop via an <see cref="IDesktopAnchor"/>. Deliberately knows nothing about the
/// Aquila domain (no AquilaState/SensorNode/widgets) — the widget slices of #24 will reach IN to
/// <see cref="ScreenCanvasWindow.Surface"/>, this service never reaches out. That one-way boundary is
/// what keeps the whole folder extractable and lets a future "place inside Progman" anchor drop in by
/// swapping the injected <see cref="IDesktopAnchor"/> factory alone.
///
/// This slice: create the canvases, anchor them, tear them down. Screen identity is plain
/// <see cref="System.Windows.Forms.Screen"/> enumeration for now; EDID-based identity and screen
/// disappear/recover handling are later slices (see #24).
/// </summary>
public sealed class DesktopSurfaceService(Func<IDesktopAnchor> anchorFactory,
    ILogger<DesktopSurfaceService> logger) : IDisposable
{
    private readonly List<(ScreenCanvasWindow Window, IDesktopAnchor Anchor)> _surfaces = [];

    public bool IsShown => _surfaces.Count > 0;

    /// <summary>What a caller needs to place content on one screen's surface, with no window type leaking
    /// out. <paramref name="Bounds"/> is in physical pixels; <paramref name="Canvas"/> coordinates are DIPs
    /// relative to that screen's top-left.</summary>
    public readonly record struct Surface(Canvas Canvas, System.Drawing.Rectangle Bounds, string Label);

    /// <summary>The live surfaces, one per screen. Domain code (widgets) reaches IN through this; this
    /// service never reaches out into the domain.</summary>
    public IReadOnlyList<Surface> Surfaces =>
        [.. _surfaces.Select(s => new Surface(s.Window.Surface, s.Window.ScreenBounds, s.Window.ScreenLabel))];

    public void Show(bool showScreenInfo = true, bool clickThrough = true)
    {
        if (IsShown) return;

        var index = 1;
        foreach (var screen in System.Windows.Forms.Screen.AllScreens)
        {
            var label = $"Screen {index}{(screen.Primary ? " (primary)" : "")}";
            var window = new ScreenCanvasWindow(screen.Bounds, label, showScreenInfo, clickThrough);
            window.Show();

            var anchor = anchorFactory();
            anchor.Attach(window); // after Show — the HWND now exists

            _surfaces.Add((window, anchor));
            index++;
        }

        logger.LogInformation("Desktop surface shown on {Count} screen(s)", _surfaces.Count);
    }

    /// <summary>
    /// Shows/hides the per-screen info label on every live surface. Kept as a callable feature rather than
    /// bring-up scaffolding: it's the basis for the "identify screen" overlay #24 needs, since the screen
    /// picker can't trust `Screen.AllScreens` ordering to match the numbers Windows shows the user.
    /// </summary>
    public void SetScreenInfoVisible(bool visible)
    {
        foreach (var (window, _) in _surfaces)
            window.SetScreenInfoVisible(visible);
    }

    public void Hide()
    {
        foreach (var (window, anchor) in _surfaces)
        {
            anchor.Dispose();
            window.Close();
        }
        _surfaces.Clear();
    }

    public void Dispose() => Hide();
}
