using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Aquila.Controls;
using Aquila.DesktopSurface;
using Aquila.Models;

namespace Aquila.Services;

/// <summary>
/// Puts live widgets on the desktop surface. This is the DOMAIN side of the boundary: it knows about
/// sensors and #23 controls and reaches IN to <see cref="DesktopSurfaceService"/>'s canvases —
/// <see cref="Aquila.DesktopSurface"/> itself stays free of any Aquila type so it remains extractable.
///
/// Widgets come from persisted <see cref="DesktopWidgetDefinition"/>s, so the layout survives restarts.
/// On a machine with no layout yet, a small starter set is seeded from whatever sensors that hardware
/// actually exposes. The pin-from-Explorer flow (#24) will add definitions to the same list — it changes
/// where definitions come from, not how widgets are built.
/// </summary>
public sealed class DesktopWidgetService
{
    private readonly AquilaService _aquila;
    private readonly DesktopSurfaceService _surfaces;
    private readonly DesktopLayoutService _layout;

    private List<DesktopWidgetDefinition>? _widgets;
    private readonly Dictionary<UIElement, DesktopWidgetDefinition> _byElement = [];

    /// <summary>The properties panel, alive only while edit mode is on. One for the whole session, not one
    /// per widget — that is what lets the selection move between widgets without dismissing anything.</summary>
    private Views.Windows.WidgetEditorPanel? _panel;

    /// <summary>
    /// The layout as it was when edit mode began, and the fact that a session is open at all — non-null IS
    /// "we are editing", so there is no second flag that could disagree with this one.
    ///
    /// Held in memory rather than re-read from widgets.json on Discard. The file is not a safe undo point:
    /// Populate writes to it when a widget's monitor has to be migrated, so hot-plugging a display mid-edit
    /// would bake in the very changes Discard exists to throw away — and a read failure would lose the lot.
    /// </summary>
    private List<DesktopWidgetDefinition>? _snapshot;

    public DesktopWidgetService(AquilaService aquila, DesktopSurfaceService surfaces, DesktopLayoutService layout)
    {
        _aquila = aquila;
        _surfaces = surfaces;
        _layout = layout;
        _surfaces.WidgetMoved += OnWidgetMoved;
        _surfaces.WidgetResized += OnWidgetResized;
        _surfaces.WidgetScreenChanged += OnWidgetScreenChanged;

        // A display change rebuilds the canvases, so the widgets have to be placed again — and this is
        // where a widget whose monitor just disappeared gets resolved onto the primary screen instead.
        _surfaces.SurfacesRebuilt += Populate;

        // The menu is built here, not in the surface service: edit/remove are domain concepts, and
        // Aquila.DesktopSurface must not learn what a widget means.
        _surfaces.WidgetRightClicked += OnWidgetRightClicked;

        // Selecting on the desktop is what fills the panel. The surface reports that a widget was reached
        // for; deciding that this means "edit this one" is a domain concept and belongs here.
        _surfaces.WidgetSelected += OnWidgetSelected;
    }

    /// <summary>Starter layout for a machine with no widgets.json yet — sensor lookups rather than fixed
    /// identifiers, since those differ per machine; whatever isn't present is simply skipped.</summary>
    private static readonly (DesktopWidgetKind Kind, string Title, Func<HardwareNode, SensorNode?> Sensor,
        string AccentKey, double X, double Y, double Width, double Height)[] _starter =
    [
        (DesktopWidgetKind.RadialGauge,   "CPU Load",  h => h.Cpus.Count > 0 ? h.Cpus[0].Load.Total : null,
            "Aquila.Scheme.Accent",  32, 150, 170, 190),
        (DesktopWidgetKind.MiniSparkline, "CPU Temp",  h => h.Cpus.Count > 0 ? h.Cpus[0].Temperature.Primary : null,
            "Aquila.Scheme.Series1", 32, 360, 240, 100),
        (DesktopWidgetKind.SensorMeter,   "CPU Power", h => h.Cpus.Count > 0 ? h.Cpus[0].Power.Package : null,
            "Aquila.Scheme.Normal",  32, 480, 240,  80),
    ];

    public void Populate()
    {
        var surfaces = _surfaces.Surfaces;
        if (surfaces.Count == 0) return; // surface not shown (feature disabled)

        var hardware = _aquila.State.Hardware;
        _widgets ??= LoadOrSeed(hardware);

        // Idempotent on purpose: remove anything placed before, so calling this twice (a display rebuild
        // racing a settings toggle, say) can't leave duplicate widgets stacked on the canvas.
        foreach (var element in _byElement.Keys.ToList())
        {
            (VisualTreeHelper.GetParent(element) as Canvas)?.Children.Remove(element);
            DetachSensor(element);
        }
        _byElement.Clear();

        var migrated = false;

        foreach (var definition in _widgets)
        {
            // Sensors can vanish between runs (hardware changed, driver renamed one). Build returns null
            // when none of them resolve; the definition is kept so the widget comes back if they do.
            var element = Build(definition, hardware);
            if (element is null) continue;

            var surface = ResolveSurface(surfaces, definition, ref migrated);

            Canvas.SetLeft(element, definition.X);
            Canvas.SetTop(element, definition.Y);
            surface.Canvas.Children.Add(element);
            _byElement[element] = definition;
        }

        if (migrated) SaveUnlessEditing();
    }

    /// <summary>
    /// Finds the surface a widget belongs on, by stable monitor key. Falls back — in order — to the legacy
    /// screen index (migrating an older widgets.json), then to the primary screen, so a widget whose
    /// monitor is unplugged reappears somewhere visible instead of being lost off-screen.
    /// </summary>
    private DesktopSurfaceService.Surface ResolveSurface(
        IReadOnlyList<DesktopSurfaceService.Surface> surfaces, DesktopWidgetDefinition definition, ref bool migrated)
    {
        if (!string.IsNullOrEmpty(definition.ScreenKey))
        {
            var match = surfaces.FirstOrDefault(s => s.Key == definition.ScreenKey);
            if (match.Canvas is not null) return match;
        }
        else if (definition.ScreenIndex >= 0 && definition.ScreenIndex < surfaces.Count)
        {
            // Layout written before screen keys existed — adopt the key for that index once, then the
            // index is never consulted again.
            var byIndex = surfaces[definition.ScreenIndex];
            definition.ScreenKey = byIndex.Key;
            migrated = true;
            return byIndex;
        }

        var primary = PrimarySurface(surfaces);
        // Don't rewrite ScreenKey here: the monitor may just be temporarily unplugged, and forgetting its
        // real home would strand the widget on the primary screen for good.
        return primary;
    }

    private static DesktopSurfaceService.Surface PrimarySurface(IReadOnlyList<DesktopSurfaceService.Surface> surfaces)
    {
        var primaryBounds = System.Windows.Forms.Screen.PrimaryScreen?.Bounds;
        var primary = surfaces.FirstOrDefault(s => s.Bounds == primaryBounds);
        return primary.Canvas is not null ? primary : surfaces[0];
    }

    private List<DesktopWidgetDefinition> LoadOrSeed(HardwareNode hardware)
    {
        var saved = _layout.Load();
        if (saved.Count > 0)
        {
            // Layouts written before widgets had a series list. Folded once, on load, so nothing further
            // down ever has to know the old shape existed.
            foreach (var definition in saved) definition.MigrateSeries();
            return saved;
        }

        var screenKey = PrimarySurface(_surfaces.Surfaces).Key;

        var seeded = new List<DesktopWidgetDefinition>();
        foreach (var (kind, title, lookup, accent, x, y, width, height) in _starter)
        {
            var identifier = lookup(hardware)?.Identifier;
            if (string.IsNullOrEmpty(identifier)) continue;

            seeded.Add(new DesktopWidgetDefinition
            {
                Kind = kind,
                Title = title,
                Series = [new WidgetSeries { SensorIdentifier = identifier, AccentKey = accent }],
                ScreenKey = screenKey,
                X = x, Y = y, Width = width, Height = height,
            });
        }

        _layout.Save(seeded);
        return seeded;
    }

    public void Remove(DesktopWidgetDefinition definition)
    {
        if (_widgets is null || !_widgets.Remove(definition)) return;
        SaveUnlessEditing();
        Repopulate();
    }

    /// <summary>Rebuilds every widget. Simpler and far less error-prone than patching one element in
    /// place, and these are rare, user-initiated actions where a rebuild is imperceptible.</summary>
    private void Repopulate()
    {
        Populate(); // idempotent — clears what it placed before
        _surfaces.RefreshEditModeAdorners();
    }

    private void OnWidgetRightClicked(DesktopSurfaceService.Surface surface, UIElement element)
    {
        if (!_byElement.TryGetValue(element, out var definition)) return;

        var menu = new ContextMenu();

        var edit = new MenuItem { Header = "Edit widget…" };
        edit.Click += (_, _) => EditWidget(definition);
        menu.Items.Add(edit);

        var remove = new MenuItem { Header = "Remove widget" };
        remove.Click += (_, _) => Remove(definition);
        menu.Items.Add(remove);

        var others = _surfaces.Surfaces.Where(s => s.Canvas != surface.Canvas).ToList();
        if (others.Count > 0)
        {
            menu.Items.Add(new Separator());
            foreach (var target in others)
            {
                var item = new MenuItem { Header = $"Send to {target.Label}" };
                // Hovering lights up the actual monitor — a screen's name alone doesn't tell the user
                // which piece of hardware it is.
                item.MouseEnter += (_, _) => _surfaces.ShowScreenIdentify(target);
                item.MouseLeave += (_, _) => _surfaces.HideScreenIdentify();
                item.Click += (_, _) => _surfaces.SendWidgetToScreen(element, target);
                menu.Items.Add(item);
            }
        }

        menu.Closed += (_, _) => _surfaces.HideScreenIdentify();
        menu.PlacementTarget = element;
        menu.IsOpen = true;
    }

    /// <summary>Points the panel at a widget — what the desktop context menu's "Edit" does, and what
    /// selecting one does. If edit mode isn't on there is no panel, so this opens one.</summary>
    public void EditWidget(DesktopWidgetDefinition definition)
    {
        BeginEditSession();
        _panel?.Edit(definition);
    }

    /// <summary>
    /// Opens the properties panel for edit mode.
    ///
    /// It replaced a modal dialog, and the reason is the whole point of edit mode: widgets are edited on
    /// the real desktop, over the real wallpaper. A window that had to be dismissed before the widget under
    /// it could be seen turned "does this colour work here" into a guess, and moving to a second widget
    /// meant closing the editor for the first.
    ///
    /// It also drops accept/cancel. Dragging a widget already wrote to disk without asking anyone, so a
    /// colour change waiting for a confirmation would have been the odd one out.
    /// </summary>
    /// <summary>Private on purpose: a panel open outside a session could write straight to disk, and there
    /// would be nothing for Discard to go back to. <see cref="BeginEditSession"/> is the only way in.</summary>
    private void ShowEditorPanel()
    {
        // There has to be a surface to edit on. Opening the editor implies wanting to see the result.
        if (!_surfaces.IsShown)
        {
            _surfaces.Show();
            Populate();
        }

        if (_panel is not null)
        {
            _panel.Activate();
            return;
        }

        _widgets ??= _layout.Load();

        _panel = new Views.Windows.WidgetEditorPanel(_aquila.State.Hardware);
        _panel.ViewModel.Changed += OnPanelEdited;
        _panel.AddRequested += OnAddRequested;
        _panel.RemoveRequested += RemoveFromPanel;

        _panel.Show();

        // After Show, so the panel has a presentation source to convert physical screen bounds with.
        //
        // WorkingArea rather than Bounds: the panel is an ordinary window and belongs above the taskbar.
        // The widget canvases use the full bounds because covering the whole screen is the point of them.
        var surfaces = _surfaces.Surfaces;
        if (surfaces.Count > 0)
            _panel.DockRight(System.Windows.Forms.Screen.FromRectangle(PrimarySurface(surfaces).Bounds).WorkingArea);

        _panel.Edit(null);
    }

    /// <summary>
    /// Opens an edit session: the panel appears, and from here until Save or Discard nothing is written to
    /// disk. The snapshot is taken once — re-entering (adding a widget, or the context menu's Edit) must
    /// not move the point Discard returns to.
    /// </summary>
    public void BeginEditSession()
    {
        _widgets ??= _layout.Load();
        _snapshot ??= _widgets.Select(d => d.Clone()).ToList();

        ShowEditorPanel();
    }

    /// <summary>
    /// Closes the session, keeping the changes or throwing them away.
    ///
    /// Discard needs no per-widget merge: Populate already clears every element it placed and re-places the
    /// whole list, so putting the snapshot back and calling it is the entire rollback.
    /// </summary>
    public void EndEditSession(bool save)
    {
        HideEditorPanel();

        var restore = _snapshot;
        if (restore is null) return;

        var changed = HasUnsavedChanges;   // read while the snapshot is still there
        _snapshot = null;                  // cleared first, so the writes below are no longer gated

        if (save)
        {
            DropEmptyWidgets();
            SaveUnlessEditing();
        }
        else if (changed)
        {
            // Only when something actually differs. Rebuilding every widget to restore a layout identical
            // to the one on screen is work the user would see as a flicker and nothing else.
            _widgets = restore;
            Populate();
        }
    }

    /// <summary>Whether anything would actually be written. Compared as the serialized layout, using the
    /// same serializer that defines what Save writes — anything it ignores is, by definition, not a
    /// change.</summary>
    public bool HasUnsavedChanges =>
        _snapshot is not null && _widgets is not null &&
        DesktopLayoutService.Serialize(_widgets) != DesktopLayoutService.Serialize(_snapshot);

    private void HideEditorPanel()
    {
        if (_panel is null) return;

        _panel.ViewModel.Changed -= OnPanelEdited;
        _panel.AddRequested -= OnAddRequested;
        _panel.RemoveRequested -= RemoveFromPanel;

        // Cleared before closing: the panel is closed, not hidden, and a view model still pointing at a
        // definition would keep it alive along with every sensor subscription behind it.
        _panel.ViewModel.Clear();
        _panel.Close();
        _panel = null;
    }

    /// <summary>
    /// Forgets widgets that were added but never given a reading. Adding one writes it to the list straight
    /// away so it can be edited live, which means walking away from a half-made widget would otherwise leave
    /// it in widgets.json for good — invisible, since a widget with nothing to draw draws nothing.
    ///
    /// Swept when the session is SAVED rather than refused at Add, because "nothing chosen yet" is a normal
    /// state to be in for a few seconds; it is only when the layout is written that it stops being
    /// temporary. Discard drops them anyway, by restoring a snapshot that never had them.
    /// </summary>
    private void DropEmptyWidgets() => _widgets?.RemoveAll(w => w.Series.Count == 0);

    /// <summary>
    /// Writes the layout, unless an edit session is open. During one, nothing reaches the file until Save —
    /// which is the whole reason Discard has something to go back to.
    ///
    /// Every caller that persists an edit goes through here. The one write that does not is the first-run
    /// seed, which is not an edit: it is the file coming into existence.
    /// </summary>
    private void SaveUnlessEditing()
    {
        if (_snapshot is not null || _widgets is null) return;
        _layout.Save(_widgets);
    }

    /// <summary>Every edit in the panel lands here — rendered and saved on the spot, doing only as much as
    /// the edit actually disturbed.</summary>
    private void OnPanelEdited(ViewModels.Windows.WidgetChange change)
    {
        if (_panel?.Target is not { } target) return;

        if (change == ViewModels.Windows.WidgetChange.Structure) RefreshWidget(target);
        else RestyleWidget(target);

        SaveUnlessEditing();
    }

    /// <summary>
    /// Re-dresses the widget already on the canvas instead of building a new one: background, border,
    /// corners, size, layer and title. Nothing here changes WHAT is drawn, so the piece inside is left
    /// running.
    ///
    /// This is the difference between a slider being smooth and a chart being blank. Rebuilding threw away
    /// the LiveCharts series and its Skia paints and started the line from an empty history — on every
    /// notch of every slider, for a change as small as a corner radius.
    /// </summary>
    private void RestyleWidget(DesktopWidgetDefinition definition)
    {
        var element = _byElement.FirstOrDefault(pair => pair.Value == definition).Key;

        // No element yet — a widget that has just been given its first reading has nothing to re-dress.
        if (element is not Border border)
        {
            RefreshWidget(definition);
            return;
        }

        border.Background = Tint(definition.BackgroundColor, definition.BackgroundOpacity);
        border.BorderBrush = Tint(definition.BorderColor, definition.BorderOpacity);
        border.BorderThickness = new Thickness(definition.BorderThickness);
        border.CornerRadius = new CornerRadius(definition.CornerRadius);
        border.Width = definition.Width;
        border.Height = definition.Height;
        Canvas.SetLeft(border, definition.X);
        Canvas.SetTop(border, definition.Y);
        Panel.SetZIndex(border, definition.ZIndex);

        if (border.Child is LabeledTile tile)
        {
            tile.Title = definition.Title;
            ApplyLineStyle(tile.Tile, definition);
            ApplyDialStyle(tile.Tile, definition);
            ApplyBarStyle(tile.Tile, definition);
            ApplyNumberStyle(tile.Tile, definition);
        }

        // The adorners are drawn around the widget's bounds, so a resize moves them.
        _surfaces.RefreshEditModeAdorners();
    }

    /// <summary>Reaching for a widget on the desktop is what fills the panel. An element with no definition
    /// behind it is one a rebuild has already replaced; the panel is emptied rather than left pointing at
    /// something that is no longer on screen.</summary>
    private void OnWidgetSelected(UIElement element)
    {
        if (_panel is null) return;
        _panel.Edit(_byElement.TryGetValue(element, out var definition) ? definition : null);
    }

    /// <summary>
    /// Creates a widget and points the panel at it. It starts with no readings, so it draws nothing until
    /// one is added — which is honest, and better than seeding a sensor the user has to notice and undo.
    /// </summary>
    public void AddWidget(string? presetSensorIdentifier)
    {
        BeginEditSession();

        // BeginEditSession loads the list; the check is for the compiler, which cannot see that it did.
        if (_widgets is null) return;

        var definition = NewDefinition(presetSensorIdentifier);
        _widgets.Add(definition);
        SaveUnlessEditing();

        _panel?.Edit(definition);
    }

    private void OnAddRequested() => AddWidget(null);

    private void RemoveFromPanel(DesktopWidgetDefinition definition)
    {
        Remove(definition);
        _panel?.Edit(null);
    }

    /// <summary>
    /// Puts a sensor on the desktop straight away — no editor, no edit mode.
    ///
    /// Pinning is "put this where I can see it", not "let me rearrange my desktop". Sweeping every window
    /// aside and opening a properties panel because someone clicked a pin would be startling, and it is the
    /// wrong trade for a one-click action: the widget lands with sensible defaults, and edit mode is one
    /// click away on the Widgets page for anyone who wants to move or dress it.
    ///
    /// Returns the widget's title so the caller can say what happened, or null if the sensor is unknown.
    /// </summary>
    public string? PinWidget(string sensorIdentifier)
    {
        if (string.IsNullOrEmpty(sensorIdentifier)) return null;

        var hardware = _aquila.State.Hardware;
        var sensor = SensorCatalog.FindByIdentifier(hardware, sensorIdentifier);
        if (sensor is null) return null;

        // It has to land somewhere visible, and the surface is where widgets live.
        if (!_surfaces.IsShown)
        {
            _surfaces.Show();
            Populate();
        }

        _widgets ??= _layout.Load();

        var definition = NewDefinition(sensorIdentifier);

        // A dial needs a bounded scale to mean anything, so only a percentage gets one; everything else
        // gets the trend, which reads honestly whatever the units are.
        definition.Kind = sensor.Unit == "%" ? DesktopWidgetKind.RadialGauge : DesktopWidgetKind.MiniSparkline;
        definition.Title = sensor.Name ?? string.Empty;

        // Nothing goes through the editor here, so the size has to be taken from the catalog rather than
        // being filled in when a kind is picked — a widget left at 0x0 would draw nothing at all.
        var kind = WidgetCatalog.For(definition.Kind);
        definition.Width = kind.DefaultWidth;
        definition.Height = kind.DefaultHeight;

        // Below whatever is already on that screen, so pinning three sensors in a row does not stack three
        // widgets in the same spot.
        var occupied = _widgets.Where(w => w.ScreenKey == definition.ScreenKey).ToList();
        if (occupied.Count > 0) definition.Y = occupied.Max(w => w.Y + w.Height) + 16;

        _widgets.Add(definition);
        SaveUnlessEditing();
        RefreshWidget(definition);

        return definition.Title;
    }

    private DesktopWidgetDefinition NewDefinition(string? presetSensorIdentifier) => new()
    {
        // Pinning from the Explorer arrives with a sensor already chosen; adding from the page does not.
        Series = string.IsNullOrEmpty(presetSensorIdentifier)
            ? []
            : [new WidgetSeries { SensorIdentifier = presetSensorIdentifier }],
        ScreenKey = _surfaces.Surfaces.Count > 0 ? PrimarySurface(_surfaces.Surfaces).Key : string.Empty,
        X = 32,
        Y = 150,
    };

    /// <summary>
    /// Re-renders one definition in place — the whole render path for live editing. Rebuilding just this
    /// widget rather than every one keeps it cheap enough to run on each keystroke.
    /// </summary>
    public void RefreshWidget(DesktopWidgetDefinition definition)
    {
        var surfaces = _surfaces.Surfaces;
        if (surfaces.Count == 0) return;

        RemoveElementFor(definition);

        // An incomplete or unresolvable definition simply renders nothing — the editor can be open with no
        // sensor added yet, and that must not throw.
        var element = Build(definition, _aquila.State.Hardware);
        if (element is null) return;

        var migrated = false;
        var surface = ResolveSurface(surfaces, definition, ref migrated);

        Canvas.SetLeft(element, definition.X);
        Canvas.SetTop(element, definition.Y);
        surface.Canvas.Children.Add(element);
        _byElement[element] = definition;

        _surfaces.RefreshEditModeAdorners();
    }

    private void RemoveElementFor(DesktopWidgetDefinition definition)
    {
        var element = _byElement.FirstOrDefault(pair => pair.Value == definition).Key;
        if (element is null) return;

        (VisualTreeHelper.GetParent(element) as Canvas)?.Children.Remove(element);
        DetachSensor(element);
        _byElement.Remove(element);
    }

    /// <summary>Hands the line settings to a piece that has a line. A dial or a number is not one and does
    /// not implement the interface, so it is skipped without anything having to know which kinds those
    /// are — the same structural test <see cref="DetachSensor"/> uses.</summary>
    private static void ApplyLineStyle(object? piece, DesktopWidgetDefinition definition)
    {
        if (piece is not IChartStyle line) return;

        line.LineThickness = definition.LineThickness;
        line.Fill = definition.Fill;
        line.Smoothness = definition.LineSmoothness;
        line.PointSize = definition.PointSize;
        line.Scale = definition.Scale;
        line.ScaleMin = definition.ScaleMin;
        line.ScaleMax = definition.ScaleMax;
    }

    /// <summary>Hands the dial settings to a piece that is one. Same structural test as
    /// <see cref="ApplyLineStyle"/>: a sparkline is not a dial and is skipped by not implementing it.</summary>
    private static void ApplyDialStyle(object? piece, DesktopWidgetDefinition definition)
    {
        if (piece is not IGaugeStyle dial) return;

        dial.ArcThickness = definition.ArcThickness;
        dial.ArcCorner = definition.ArcCorner;
        dial.Sweep = definition.Sweep;
        dial.ValueSize = definition.ValueSize;
        dial.ShowValue = definition.ShowValue;
    }

    /// <summary>Hands the bar settings to a piece that is one. Same structural test as its two siblings.</summary>
    private static void ApplyBarStyle(object? piece, DesktopWidgetDefinition definition)
    {
        if (piece is not IMeterStyle bar) return;

        bar.BarThickness = definition.BarThickness;
        bar.BarCorner = definition.BarCorner;
        bar.ShowValue = definition.ShowValue;
        bar.ValueSize = definition.BarValueSize;
        bar.Layout = definition.Layout;
    }

    /// <summary>Hands the number settings to a piece that is one. Last of the four structural tests.</summary>
    private static void ApplyNumberStyle(object? piece, DesktopWidgetDefinition definition)
    {
        if (piece is not IStatStyle stat) return;

        stat.ValueSize = definition.StatValueSize;
        stat.ShowUnit = definition.ShowUnit;
        stat.UnitSize = definition.UnitSize;
        stat.ShowPanel = definition.ShowPanel;
    }

    /// <summary>
    /// Clears the Sensor on the piece inside a built widget, so it unhooks itself. Mirrors the structure
    /// Build produces: Border → LabeledTile → piece.
    ///
    /// Matched by interface rather than by a switch over kinds. That switch was the one place a new widget
    /// could break without a compiler error: an unlisted kind simply would not match, and its piece would
    /// keep updating after the widget was gone.
    /// </summary>
    private static void DetachSensor(UIElement widget)
    {
        if (widget is Border { Child: LabeledTile { Tile: ISensorPiece piece } })
            piece.Sensor = null;
    }

    private void OnWidgetMoved(DesktopSurfaceService.Surface surface, UIElement element, double x, double y)
    {
        if (!_byElement.TryGetValue(element, out var definition)) return;

        definition.X = x;
        definition.Y = y;
        SaveUnlessEditing();

        // The drag and the boxes set the same two numbers, so whichever was used last has to be what both
        // of them show.
        if (_panel?.Target == definition) _panel.ViewModel.SyncGeometry();
    }

    private void OnWidgetResized(UIElement element, double width, double height)
    {
        if (!_byElement.TryGetValue(element, out var definition)) return;

        definition.Width = width;
        definition.Height = height;
        SaveUnlessEditing();

        // The wheel and the boxes set the same two numbers, so whichever was used last has to be what both
        // of them show.
        if (_panel?.Target == definition) _panel.ViewModel.SyncGeometry();
    }

    private void OnWidgetScreenChanged(DesktopSurfaceService.Surface surface, UIElement element, double x, double y)
    {
        if (!_byElement.TryGetValue(element, out var definition)) return;

        // Sending a widget to a screen is an explicit choice, so this DOES rewrite the stored monitor —
        // unlike the fallback when a monitor is merely missing, which keeps the original.
        definition.ScreenKey = surface.Key;
        definition.X = x;
        definition.Y = y;
        SaveUnlessEditing();

        if (_panel?.Target == definition) _panel.ViewModel.SyncGeometry();
    }

    /// <summary>
    /// Builds one #23 control from a definition's series, using the entry <see cref="WidgetCatalog"/>
    /// holds for that kind. Returns null when nothing can be drawn — a definition whose sensors have all
    /// vanished, or one the editor has not finished yet.
    ///
    /// Public so the editor shows exactly what will land on the desktop, rather than an approximation
    /// that can drift.
    /// </summary>
    public static UIElement? Build(DesktopWidgetDefinition definition, HardwareNode hardware)
    {
        var kind = WidgetCatalog.For(definition.Kind);

        // Resolved in order, dropping any sensor the machine no longer reports — a definition survives a
        // driver rename, it just draws one line fewer until the sensor comes back.
        var resolved = new List<SensorNode>();
        var roles = new List<string>();

        foreach (var series in definition.Series.Take(kind.MaxSeries))
        {
            var sensor = SensorCatalog.FindByIdentifier(hardware, series.SensorIdentifier);
            if (sensor is null) continue;

            resolved.Add(sensor);
            roles.Add(Role(series.AccentKey, DefaultRole(roles.Count)));
        }

        if (resolved.Count == 0) return null;

        var piece = kind.Create(resolved);

        // SetResourceReference is the code equivalent of DynamicResource — role brushes are swapped when
        // the theme or profile changes (ColorProfileService.Apply), so a static lookup would freeze the
        // colours of whichever profile happened to be active when the widget was built.
        for (var i = 0; i < resolved.Count; i++)
            piece.SetResourceReference(kind.AccentProperties[i], roles[i]);

        ApplyLineStyle(piece, definition);
        ApplyDialStyle(piece, definition);
        ApplyBarStyle(piece, definition);
        ApplyNumberStyle(piece, definition);

        // A desktop widget sits on whatever wallpaper the user has, so it can't rely on the app's
        // background for contrast: it carries its own backing panel, which the user can restyle.
        var widget = new Border
        {
            Background = Tint(definition.BackgroundColor, definition.BackgroundOpacity),
            BorderBrush = Tint(definition.BorderColor, definition.BorderOpacity),
            BorderThickness = new Thickness(definition.BorderThickness),
            CornerRadius = new CornerRadius(definition.CornerRadius),
            Padding = new Thickness(10),
            Width = definition.Width,
            Height = definition.Height,
            Child = new LabeledTile
            {
                Title = definition.Title,
                Tile = piece,
                Foreground = Brushes.White,
            },
        };

        // Set here rather than where the widget is added to a canvas, because there are two such places
        // and this is the one both of them go through.
        Panel.SetZIndex(widget, definition.ZIndex);
        return widget;
    }

    /// <summary>The colour a series gets when it has not chosen one. Spread across roles rather than all
    /// taking the accent, because two lines in the same colour are one line.</summary>
    private static string DefaultRole(int index) => index switch
    {
        0 => "Aquila.Scheme.Accent",
        1 => "Aquila.Scheme.Series2",
        _ => "Aquila.Scheme.Series3",
    };

    /// <summary>Keeps a stored key only if it still names a profile role, so a layout written before colour
    /// profiles existed — or hand-edited since — falls back rather than rendering colourless.</summary>
    private static string Role(string key, string fallback) =>
        key.StartsWith("Aquila.Scheme.", StringComparison.Ordinal) ? key : fallback;

    /// <summary>Combines a stored "#RRGGBB" with a separate 0..1 opacity. Falls back to transparent rather
    /// than throwing: a hand-edited widgets.json shouldn't be able to break the desktop.</summary>
    private static Brush Tint(string hex, double opacity)
    {
        try
        {
            var color = (Color)ColorConverter.ConvertFromString(hex);
            color.A = (byte)Math.Clamp(opacity * 255, 0, 255);
            return new SolidColorBrush(color);
        }
        catch
        {
            return Brushes.Transparent;
        }
    }
}
