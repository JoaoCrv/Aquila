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

    private bool _showScreenInfo = true;
    private bool _clickThrough = true;
    private bool _organizing;
    private bool _watchingDisplayChanges;
    private System.Windows.Threading.DispatcherTimer? _displayChangeDebounce;
    private OrganizeToolbar? _toolbar;

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

    public void Show(bool showScreenInfo = true, bool clickThrough = true)
    {
        if (IsShown) return;

        _showScreenInfo = showScreenInfo;
        _clickThrough = clickThrough;
        StartWatchingDisplayChanges();

        var index = 1;
        foreach (var screen in System.Windows.Forms.Screen.AllScreens)
        {
            var label = $"Screen {index}{(screen.Primary ? " (primary)" : "")}";
            var window = new ScreenCanvasWindow(screen.Bounds, label, ScreenIdentity.KeyFor(screen),
                showScreenInfo, clickThrough);
            window.WidgetMoved += (element, x, y) => WidgetMoved?.Invoke(ToSurface(window), element, x, y);
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

        var organizing = _organizing;
        Hide();
        Show(_showScreenInfo, _clickThrough);
        if (organizing) SetOrganizing(true);

        SurfacesRebuilt?.Invoke();
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

    /// <summary>
    /// Enters/leaves organization mode (#30) on every surface: widgets become draggable and get a dashed
    /// outline. One switch for all screens — the mode is a state of the app, not of an individual widget
    /// or canvas.
    /// </summary>
    /// <summary>Raised when the user finishes organizing from the floating toolbar, so the app can undo
    /// whatever it did to get out of the way (typically un-minimize itself).</summary>
    public event Action? OrganizeFinished;

    public void SetOrganizing(bool organizing)
    {
        _organizing = organizing; // remembered so a display-change rebuild doesn't drop the user out of it
        foreach (var (window, _) in _surfaces)
            window.SetOrganizing(organizing);

        if (organizing) ShowToolbar(); else HideToolbar();
    }

    private void ShowToolbar()
    {
        if (_toolbar is not null) return;

        _toolbar = new OrganizeToolbar();
        _toolbar.Done += () => OrganizeFinished?.Invoke();
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

    /// <summary>Raised after a widget is sent to another screen, with its new surface and position.</summary>
    public event Action<Surface, System.Windows.UIElement, double, double>? WidgetScreenChanged;

    /// <summary>
    /// Offers "send to [screen]" for a right-clicked widget. Lives here rather than in the canvas because
    /// only this service knows the other screens. Hovering an entry lights up the physical monitor it
    /// refers to (see <see cref="ScreenIdentifyOverlay"/>) — the screen list can't be trusted to be
    /// self-explanatory, so the user gets to SEE which one they're choosing.
    /// </summary>
    private void OnWidgetRightClicked(ScreenCanvasWindow source, System.Windows.UIElement widget)
    {
        if (_surfaces.Count < 2) return; // nowhere else to send it

        var menu = new System.Windows.Controls.ContextMenu();

        foreach (var (window, _) in _surfaces)
        {
            var target = window;
            var item = new System.Windows.Controls.MenuItem
            {
                Header = $"Send to {target.ScreenLabel}",
                IsEnabled = target != source,
            };

            item.MouseEnter += (_, _) => ScreenIdentifyOverlay.Show(target.ScreenBounds, target.ScreenLabel);
            item.MouseLeave += (_, _) => ScreenIdentifyOverlay.Hide();
            item.Click += (_, _) => SendToScreen(widget, source, target);

            menu.Items.Add(item);
        }

        menu.Closed += (_, _) => ScreenIdentifyOverlay.Hide();
        menu.PlacementTarget = widget;
        menu.IsOpen = true;
    }

    private void SendToScreen(System.Windows.UIElement widget, ScreenCanvasWindow from, ScreenCanvasWindow to)
    {
        ScreenIdentifyOverlay.Hide();
        if (from == to) return;

        var x = System.Windows.Controls.Canvas.GetLeft(widget);
        var y = System.Windows.Controls.Canvas.GetTop(widget);

        from.ReleaseWidget(widget);
        to.AdoptWidget(widget, double.IsNaN(x) ? 0 : x, double.IsNaN(y) ? 0 : y);

        WidgetScreenChanged?.Invoke(ToSurface(to), widget,
            System.Windows.Controls.Canvas.GetLeft(widget), System.Windows.Controls.Canvas.GetTop(widget));
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
