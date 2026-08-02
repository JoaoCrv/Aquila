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
/// Surfaces are rebuilt when the display configuration changes (monitor plugged/unplugged/disabled,
/// resolution or arrangement changed). Note that powering a monitor OFF is not such a change: Windows
/// keeps the display connected and the desktop still extends onto it, which is why icons and widgets stay
/// put — nothing to react to, and nothing to recover.
/// </summary>
public sealed class DesktopSurfaceService(Func<IDesktopAnchor> anchorFactory,
    ILogger<DesktopSurfaceService> logger) : IDisposable
{
    private readonly List<(ScreenCanvasWindow Window, IDesktopAnchor Anchor)> _surfaces = [];

    private bool _clickThrough = true;
    private bool _editing;
    private bool _watchingDisplayChanges;
    private System.Windows.Threading.DispatcherTimer? _displayChangeDebounce;
    private EditModeToolbar? _toolbar;

    public bool IsShown => _surfaces.Count > 0;

    /// <summary>Raised after the surfaces are rebuilt for a display change — the canvases are new, so
    /// whoever put content on them has to put it back.</summary>
    public event Action? SurfacesRebuilt;

    /// <summary>What a caller needs to place content on one screen's surface, with no window type leaking
    /// out. <paramref name="Bounds"/> is in physical pixels; <paramref name="Canvas"/> coordinates are DIPs
    /// relative to that screen's top-left. <paramref name="Key"/> is the stable monitor identity to store
    /// in a saved layout (see <see cref="ScreenIdentity"/>) — never the index or DeviceName.</summary>
    public readonly record struct Surface(Canvas Canvas, System.Drawing.Rectangle Bounds, string Label, string Key);

    /// <summary>The live surfaces, one per screen. Domain code (widgets) reaches IN through this; this
    /// service never reaches out into the domain.</summary>
    public IReadOnlyList<Surface> Surfaces =>
        [.. _surfaces.Select(s => ToSurface(s.Window))];

    private static Surface ToSurface(ScreenCanvasWindow w) =>
        new(w.Surface, w.ScreenBounds, w.ScreenLabel, w.ScreenKey);

    public void Show(bool clickThrough = true)
    {
        if (IsShown) return;

        _clickThrough = clickThrough;
        StartWatchingDisplayChanges();

        var index = 1;
        foreach (var screen in System.Windows.Forms.Screen.AllScreens)
        {
            var label = $"Screen {index}{(screen.Primary ? " (primary)" : "")}";
            var window = new ScreenCanvasWindow(screen.Bounds, label, ScreenIdentity.KeyFor(screen),
                clickThrough);
            window.WidgetMoved += (element, x, y) => WidgetMoved?.Invoke(ToSurface(window), element, x, y);
            window.WidgetResized += (element, w, h) => WidgetResized?.Invoke(element, w, h);
            window.WidgetRightClicked += OnWidgetRightClicked;
            window.Show();

            var anchor = anchorFactory();
            anchor.Attach(window); // after Show — the HWND now exists

            _surfaces.Add((window, anchor));
            index++;
        }

        logger.LogInformation("Desktop surface shown on {Count} screen(s)", _surfaces.Count);
    }

    private void StartWatchingDisplayChanges()
    {
        if (_watchingDisplayChanges) return;
        _watchingDisplayChanges = true;
        Microsoft.Win32.SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
    }

    /// <summary>
    /// A real display change (monitor added/removed/disabled, resolution or arrangement changed) fires
    /// this several times in a burst, and the screen list is unreliable mid-burst — so debounce and only
    /// rebuild once things have settled. SystemEvents can raise on a non-UI thread, hence the marshal.
    /// </summary>
    private void OnDisplaySettingsChanged(object? sender, EventArgs e)
    {
        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher is null) return;

        dispatcher.BeginInvoke(() =>
        {
            _displayChangeDebounce ??= CreateDebounceTimer(dispatcher);
            _displayChangeDebounce.Stop();
            _displayChangeDebounce.Start();
        });
    }

    private System.Windows.Threading.DispatcherTimer CreateDebounceTimer(System.Windows.Threading.Dispatcher dispatcher)
    {
        var timer = new System.Windows.Threading.DispatcherTimer(System.Windows.Threading.DispatcherPriority.Normal, dispatcher)
        {
            Interval = TimeSpan.FromSeconds(1.5),
        };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            RebuildForDisplayChange();
        };
        return timer;
    }

    private void RebuildForDisplayChange()
    {
        if (!IsShown) return;

        logger.LogInformation("Display configuration changed, rebuilding desktop surfaces");

        var editing = _editing;
        Hide();
        Show(_clickThrough);
        if (editing) SetEditing(true);

        SurfacesRebuilt?.Invoke();
    }

    /// <summary>
    /// Flashes a big label on every screen at once, so the user can see which monitor is which — the same
    /// overlay shown when hovering a "send to screen" entry, rather than a second mechanism doing the same
    /// job. Replaced an always-on label drawn on the surface: identifying a screen is something you want
    /// for a moment while deciding, not a permanent fixture on your desktop.
    /// </summary>
    public void IdentifyScreens() =>
        ScreenIdentifyOverlay.ShowAll(_surfaces.Select(s => (s.Window.ScreenBounds, s.Window.ScreenLabel)));

    /// <summary>Raised when the user finishes editing from the floating toolbar, so the app can undo
    /// whatever it did to get out of the way (typically un-minimize itself).</summary>
    public event Action? EditingFinished;

    /// <summary>
    /// Enters/leaves edit mode (#30) on every surface: widgets become draggable and get a dashed outline.
    /// One switch for all screens — the mode is a state of the app, not of an individual widget or canvas.
    /// </summary>
    public void SetEditing(bool editing)
    {
        _editing = editing; // remembered so a display-change rebuild doesn't drop the user out of it
        foreach (var (window, _) in _surfaces)
            window.SetEditing(editing);

        if (editing) ShowToolbar(); else HideToolbar();
    }

    private void ShowToolbar()
    {
        if (_toolbar is not null) return;

        _toolbar = new EditModeToolbar();
        _toolbar.Done += () => EditingFinished?.Invoke();
        _toolbar.Show();
    }

    private void HideToolbar()
    {
        _toolbar?.Close();
        _toolbar = null;
    }

    /// <summary>Raised when a widget is dragged to a new position, with the surface it belongs to. The
    /// seam for persisting layout.</summary>
    public event Action<Surface, System.Windows.UIElement, double, double>? WidgetMoved;

    /// <summary>Raised after a widget is resized with the wheel, with its new size.</summary>
    public event Action<System.Windows.UIElement, double, double>? WidgetResized;

    /// <summary>Raised after a widget is sent to another screen, with its new surface and position.</summary>
    public event Action<Surface, System.Windows.UIElement, double, double>? WidgetScreenChanged;

    /// <summary>Raised when a widget is right-clicked in edit mode. The menu is built by the
    /// caller, not here: actions like edit/remove are domain concepts, and this service must not learn
    /// what a widget means.</summary>
    public event Action<Surface, System.Windows.UIElement>? WidgetRightClicked;

    private void OnWidgetRightClicked(ScreenCanvasWindow source, System.Windows.UIElement widget) =>
        WidgetRightClicked?.Invoke(ToSurface(source), widget);

    /// <summary>Moves a widget to another screen's canvas, keeping its position where it fits.</summary>
    public void SendWidgetToScreen(System.Windows.UIElement widget, Surface target)
    {
        HideScreenIdentify();

        var from = _surfaces.FirstOrDefault(s => s.Window.Surface.Children.Contains(widget)).Window;
        var to = _surfaces.FirstOrDefault(s => s.Window.Surface == target.Canvas).Window;
        if (from is null || to is null || from == to) return;

        var x = System.Windows.Controls.Canvas.GetLeft(widget);
        var y = System.Windows.Controls.Canvas.GetTop(widget);

        from.ReleaseWidget(widget);
        to.AdoptWidget(widget, double.IsNaN(x) ? 0 : x, double.IsNaN(y) ? 0 : y);

        WidgetScreenChanged?.Invoke(ToSurface(to), widget,
            System.Windows.Controls.Canvas.GetLeft(widget), System.Windows.Controls.Canvas.GetTop(widget));
    }

    /// <summary>Lights up a physical monitor with a big label, so a screen picker can show WHICH screen an
    /// entry means instead of relying on a name the user can't map to hardware.</summary>
    public void ShowScreenIdentify(Surface surface) => ScreenIdentifyOverlay.Show(surface.Bounds, surface.Label);

    public void HideScreenIdentify() => ScreenIdentifyOverlay.Hide();

    /// <summary>Re-syncs the edit-mode outlines after widgets are added or removed.</summary>
    public void RefreshEditModeAdorners()
    {
        foreach (var (window, _) in _surfaces)
            window.RefreshEditModeAdorners();
    }

    public void Hide()
    {
        HideToolbar();
        foreach (var (window, anchor) in _surfaces)
        {
            anchor.Dispose();
            window.Close();
        }
        _surfaces.Clear();
    }

    public void Dispose()
    {
        Hide();

        // SystemEvents keeps a static handler list, so not unsubscribing would leak this service for the
        // process's lifetime.
        if (_watchingDisplayChanges)
        {
            Microsoft.Win32.SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
            _watchingDisplayChanges = false;
        }

        _displayChangeDebounce?.Stop();
        _displayChangeDebounce = null;
    }
}
