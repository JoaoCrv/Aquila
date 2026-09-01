using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
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
    private readonly PresetService _presets;

    private List<DesktopWidgetDefinition>? _widgets;
    private readonly Dictionary<UIElement, DesktopWidgetDefinition> _byElement = [];

    /// <summary>The properties panel, alive only while edit mode is on. One for the whole session, not one
    /// per widget — that is what lets the selection move between widgets without dismissing anything.</summary>
    private Views.Windows.WidgetEditorPanel? _panel;

    /// <summary>
    /// The layout as it was when edit mode began, and the fact that a session is open at all — non-null IS
    /// "we are editing", so there is no second flag that could disagree with this one.
    ///
    /// Held in memory rather than re-read from widgets.json on Discard: the file is only as good as the
    /// last write, and a read that fails would lose everything the session was meant to be able to undo.
    /// </summary>
    private List<DesktopWidgetDefinition>? _snapshot;

    /// <summary>The widgets a preset was actually edited THROUGH during this session. A variant follows
    /// these and not everything wearing the preset, so "make the clock different" can be answered without
    /// dragging the other five widgets along with it.</summary>
    private readonly HashSet<DesktopWidgetDefinition> _dressed = [];

    public DesktopWidgetService(AquilaService aquila, DesktopSurfaceService surfaces,
        DesktopLayoutService layout, PresetService presets)
    {
        _aquila = aquila;
        _surfaces = surfaces;
        _layout = layout;
        _presets = presets;
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
        double X, double Y, double Width, double Height)[] _starter =
    [
        // Coloured by their own readings, like every other single-reading widget: a starter set that sits
        // in one flat colour teaches the wrong thing about what these are for.
        (DesktopWidgetKind.RadialGauge,   "CPU Load",  h => h.Cpus.Count > 0 ? h.Cpus[0].Load.Total : null,
            32, 150, 170, 190),
        (DesktopWidgetKind.MiniSparkline, "CPU Temp",  h => h.Cpus.Count > 0 ? h.Cpus[0].Temperature.Primary : null,
            32, 360, 240, 100),
        (DesktopWidgetKind.SensorMeter,   "CPU Power", h => h.Cpus.Count > 0 ? h.Cpus[0].Power.Package : null,
            32, 480, 240,  80),
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

        foreach (var definition in _widgets)
        {
            // Sensors can vanish between runs (hardware changed, driver renamed one). Build returns null
            // when none of them resolve; the definition is kept so the widget comes back if they do.
            var element = Build(definition, hardware);
            if (element is null) continue;

            var surface = ResolveSurface(surfaces, definition);

            Canvas.SetLeft(element, definition.X);
            Canvas.SetTop(element, definition.Y);
            surface.Canvas.Children.Add(element);
            _byElement[element] = definition;
        }

    }

    /// <summary>
    /// Finds the surface a widget belongs on, by stable monitor key, falling back to the primary screen so
    /// a widget whose monitor is unplugged reappears somewhere visible instead of being lost off-screen.
    /// </summary>
    private DesktopSurfaceService.Surface ResolveSurface(
        IReadOnlyList<DesktopSurfaceService.Surface> surfaces, DesktopWidgetDefinition definition)
    {
        if (WidgetSurface.ScreenKeyOf(definition.Surface) is { } key)
        {
            var match = surfaces.FirstOrDefault(s => s.Key == key);
            if (match.Canvas is not null) return match;

            // A named monitor that is not here may only be unplugged, so its name is kept: forgetting a
            // widget's real home would strand it on the primary screen for good.
            return PrimarySurface(surfaces);
        }

        // No home named at all — a widget from before surfaces were prefixed. There is nothing to forget,
        // so it adopts the screen it lands on and stops being homeless on the next save.
        var primary = PrimarySurface(surfaces);
        definition.Surface = WidgetSurface.Screen(primary.Key);
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
        if (saved.Count > 0) return saved;

        var screen = WidgetSurface.Screen(PrimarySurface(_surfaces.Surfaces).Key);

        var seeded = new List<DesktopWidgetDefinition>();
        foreach (var (kind, title, lookup, x, y, width, height) in _starter)
        {
            var identifier = lookup(hardware)?.Identifier;
            if (string.IsNullOrEmpty(identifier)) continue;

            seeded.Add(new DesktopWidgetDefinition
            {
                Kind = kind,
                Title = title,
                Series = [new WidgetSeries { SensorIdentifier = identifier }],
                Surface = screen,
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

        _panel = new Views.Windows.WidgetEditorPanel(_aquila.State.Hardware, _presets);
        _panel.ViewModel.Changed += OnPanelEdited;
        _panel.ViewModel.PresetRemoved += DropPreset;
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
        if (restore is null)
        {
            // No widget session, so any drafted preset is an orphan of one that ended without either
            // outcome running. Left alone it would be committed by the NEXT save, which is how a preset
            // edited on Monday could fork itself again on Tuesday.
            _presets.Revert();
            _dressed.Clear();
            return;
        }

        // The layout alone, not the wider question: a preset edit is undone by re-dressing, which keeps the
        // charts running, where a full rebuild would restart every line from an empty history.
        var changed = LayoutChanged;       // read while the snapshot is still there
        _snapshot = null;                  // cleared first, so the writes below are no longer gated

        if (save)
        {
            DropEmptyWidgets();

            // A locked preset whose changes the user chose not to keep is put back as it was, leaving the
            // widgets wearing it showing a look that no longer exists. Re-dressed rather than rebuilt:
            // they have the wrong colours, not the wrong thing, and a rebuild restarts every chart empty.
            if (_presets.Commit()) RestyleAll();

            SaveUnlessEditing();
            _dressed.Clear();
        }
        else
        {
            // Read before Revert clears it. A preset-only edit leaves the layout byte-for-byte identical, so
            // the widget comparison on its own would conclude nothing had happened and leave the drafted
            // look sitting on the desktop after the user asked for it to be thrown away.
            var dressed = _presets.Drafts.Count > 0;
            _presets.Revert();
            _dressed.Clear();

            // Only when something actually differs. Rebuilding every widget to restore a layout identical
            // to the one on screen is work the user would see as a flicker and nothing else.
            if (changed)
            {
                _widgets = restore;
                Populate();
            }
            else if (dressed) RestyleAll();
        }
    }

    /// <summary>
    /// Removes a preset and puts everything wearing it back on the default.
    ///
    /// Named rather than left dangling. For() already answers a missing id with the base, so the widgets
    /// would draw either way — but a layout naming a preset that does not exist is a file that lies, and
    /// the next person to read it cannot tell a deletion from a typo. Cleared to empty rather than to the
    /// base's id, because "whatever the default is" is exactly what these widgets are now wearing.
    /// </summary>
    public void DropPreset(Preset preset)
    {
        _presets.Delete(preset);

        if (_widgets is not null)
            foreach (var widget in _widgets)
                if (string.Equals(widget.Preset, preset.Id, StringComparison.OrdinalIgnoreCase))
                    widget.Preset = string.Empty;

        RestyleAll();
        SaveUnlessEditing();
    }

    /// <summary>The locked presets edited in this session — what the user has to be asked about before
    /// the session closes, because those changes have nowhere of their own to go.</summary>
    public IReadOnlyList<Preset> LockedEdits => _presets.LockedDrafts;

    /// <summary>
    /// Answers "keep my changes to a locked preset" by giving them a preset of their own.
    ///
    /// The widgets that move are the ones the edit was made THROUGH, not everything wearing the original.
    /// A locked preset is shared by definition — it is the one every widget starts on — so moving all of
    /// them is never what someone keeping one widget's changes meant.
    ///
    /// A widget naming no preset is wearing the base, so it moves too when the base is what was edited,
    /// and gains an explicit name doing it: it is no longer wearing whatever the default happens to be.
    /// </summary>
    public void KeepAsNew(Preset locked)
    {
        var variant = _presets.Duplicate(locked);

        foreach (var widget in _dressed)
            if (string.Equals(widget.Preset, locked.Id, StringComparison.OrdinalIgnoreCase) ||
                (string.IsNullOrWhiteSpace(widget.Preset) && locked.Id == PresetService.BaseId))
                widget.Preset = variant.Id;
    }

    /// <summary>Whether the layout itself would be written differently. Compared as the serialized layout,
    /// using the same serializer that defines what Save writes — anything it ignores is, by definition, not
    /// a change.</summary>
    private bool LayoutChanged =>
        _snapshot is not null && _widgets is not null &&
        DesktopLayoutService.Serialize(_widgets) != DesktopLayoutService.Serialize(_snapshot);

    /// <summary>
    /// Whether anything at all would be lost by discarding.
    ///
    /// Wider than the layout on purpose: an edit to a preset changes how every widget looks and leaves the
    /// layout byte-for-byte identical, so asking the layout alone would let a whole afternoon of colour work
    /// be thrown away without the confirmation ever appearing. Kept apart from
    /// <see cref="LayoutChanged"/> because they answer different questions — this one decides whether to
    /// ask, that one decides whether a rebuild is needed.
    /// </summary>
    public bool HasUnsavedChanges => LayoutChanged || _presets.Drafts.Count > 0;

    private void HideEditorPanel()
    {
        if (_panel is null) return;

        _panel.ViewModel.Changed -= OnPanelEdited;
        _panel.ViewModel.PresetRemoved -= DropPreset;
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
    ///
    /// Only kinds that NEED a reading. A backdrop has none by design, and sweeping by series count alone
    /// would have deleted every one of them the first time a session was saved.
    /// </summary>
    private void DropEmptyWidgets() =>
        _widgets?.RemoveAll(w => w.Series.Count == 0 && WidgetCatalog.For(w.Kind).NeedsReading);

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
        else if (change == ViewModels.Windows.WidgetChange.Dress)
        {
            // Recorded here rather than when the panel opens: looking at a widget is not editing through it.
            _dressed.Add(target);
            RestyleWearing(target);
        }
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

        var preset = _presets.For(definition.Preset);

        Frame(border, definition, preset);
        Canvas.SetLeft(border, definition.X);
        Canvas.SetTop(border, definition.Y);
        Panel.SetZIndex(border, definition.ZIndex);

        if (border.Child is LabeledTile tile)
        {
            Dress(tile.Tile, preset);
            Content(tile.Tile, definition);
            KeepEnoughHistory(definition, ResolveSensors(definition));
        }

        // The adorners are drawn around the widget's bounds, so a resize moves them.
        _surfaces.RefreshEditModeAdorners();
    }

    /// <summary>
    /// Re-dresses every widget wearing the same preset as the one being edited.
    ///
    /// Compared by reference on the RESOLVED preset rather than by id, so a widget that names it and a
    /// widget that names nothing at all count as the same when the base is what changed — the two spellings
    /// of "ember" would otherwise drift apart on screen.
    /// </summary>
    private void RestyleWearing(DesktopWidgetDefinition edited)
    {
        var preset = _presets.For(edited.Preset);

        // Copied, because re-dressing a widget with no element yet falls through to a rebuild, and that
        // writes to the dictionary being walked.
        foreach (var definition in _byElement.Values.ToList())
            if (ReferenceEquals(_presets.For(definition.Preset), preset)) RestyleWidget(definition);
    }

    private void RestyleAll()
    {
        foreach (var definition in _byElement.Values.ToList()) RestyleWidget(definition);
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
    /// Every widget that exists, and whether it can draw.
    ///
    /// The drawability rules live here rather than in the page, because they are the same rules
    /// <see cref="Build"/> follows — a list that worked them out separately would eventually disagree with
    /// what is actually on screen, which is the one thing it exists to report.
    /// </summary>
    public IReadOnlyList<WidgetSummary> Describe()
    {
        _widgets ??= _layout.Load();

        var hardware = _aquila.State.Hardware;
        return [.. _widgets.Select(w => Describe(w, hardware))];
    }

    private static WidgetSummary Describe(DesktopWidgetDefinition definition, HardwareNode hardware)
    {
        var kind = WidgetCatalog.For(definition.Kind);
        var title = string.IsNullOrWhiteSpace(definition.Title) ? kind.Name : definition.Title;
        var where = WidgetSurface.ScreenKeyOf(definition.Surface) is null ? "No screen" : "Desktop";

        // A kind that does not need a reading is never broken for want of one.
        if (!kind.NeedsReading && definition.Series.Count == 0)
            return new WidgetSummary(definition, kind.Name, title, where, null);

        if (definition.Series.Count == 0)
            return new WidgetSummary(definition, kind.Name, title, where,
                "No reading chosen — nothing to draw.");

        var found = definition.Series
            .Select(x => SensorCatalog.FindEntry(hardware, x.SensorIdentifier))
            .OfType<SensorEntry>()
            .ToList();

        if (found.Count == 0)
            return new WidgetSummary(definition, kind.Name, title, where,
                $"This machine does not report {string.Join(", ", definition.Series.Select(x => x.SensorIdentifier))}.");

        var reads = string.Join(", ", found.Select(e => e.Label));
        var missing = definition.Series.Count - found.Count;

        return new WidgetSummary(definition, kind.Name, title, $"{where} — {reads}",
            missing > 0 ? $"{missing} of its readings are not being reported." : null);
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
        var occupied = _widgets.Where(w => w.Surface == definition.Surface).ToList();
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
        Surface = _surfaces.Surfaces.Count > 0 ? WidgetSurface.Screen(PrimarySurface(_surfaces.Surfaces).Key) : string.Empty,
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

        var surface = ResolveSurface(surfaces, definition);

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

    /// <summary>
    /// Dresses a piece from the preset. Each section is matched structurally — a piece that is not a dial
    /// does not implement <see cref="IGaugeStyle"/> and is skipped — so nothing here keeps a list of which
    /// kinds get which settings, and a kind added later cannot be forgotten silently.
    /// </summary>
    /// <summary>
    /// Asks the sensors behind a chart to keep enough readings to fill its window.
    ///
    /// Sensors keep sixty by default whether anything draws them or not, so a longer trend is requested only
    /// on the ones actually being shown — a machine reporting three hundred sensors should not pay for a
    /// ten-minute buffer on all of them to give it to the two on the desktop.
    ///
    /// Raised, never lowered. Another widget may be showing the same sensor over a longer window, and
    /// shortening one chart must not quietly truncate the other's history.
    /// </summary>
    private static void KeepEnoughHistory(DesktopWidgetDefinition definition, IReadOnlyList<SensorNode> sensors)
    {
        if (!WidgetCatalog.For(definition.Kind).HasLine) return;

        foreach (var sensor in sensors)
            if (sensor.HistoryDepth < definition.PointCount)
                sensor.HistoryDepth = definition.PointCount;
    }

    /// <summary>The live sensors a definition names, in order, skipping any the machine no longer reports.</summary>
    private List<SensorNode> ResolveSensors(DesktopWidgetDefinition definition)
    {
        var hardware = _aquila.State.Hardware;
        var resolved = new List<SensorNode>();

        foreach (var series in definition.Series)
            if (SensorCatalog.FindByIdentifier(hardware, series.SensorIdentifier) is { } sensor)
                resolved.Add(sensor);

        return resolved;
    }

    /// <summary>
    /// The handful of things a piece shows that are NOT appearance: the words a caption says, what a clock
    /// is set to. They live on the widget because a preset carrying them would rename every widget wearing
    /// it — and the format's decisive test is that one preset dresses a CPU widget and a network one
    /// without editing.
    /// </summary>
    private static void Content(object? piece, DesktopWidgetDefinition definition)
    {
        if (piece is ICaptionStyle caption) caption.Caption = definition.Text;
        if (piece is IClockStyle clock) clock.Format = definition.ClockFormat;

        if (piece is IChartStyle chart)
        {
            chart.PointCount = definition.PointCount;
            chart.Scale = definition.Scale;
            chart.ScaleMin = definition.ScaleMin;
            chart.ScaleMax = definition.ScaleMax;
        }
    }

    /// <summary>
    /// The panel a widget sits in and the label above it — everything outside the piece itself.
    ///
    /// One method rather than the same assignments in Build and RestyleWidget. Those are the two places a
    /// widget's frame is decided, and a setting added to one and forgotten in the other shows up as a
    /// widget that looks right until the moment someone edits it.
    /// </summary>
    private static void Frame(Border border, DesktopWidgetDefinition definition, Preset preset)
    {
        border.Background = Tint(preset.Background.Color, preset.Background.Opacity);
        border.BorderBrush = Tint(preset.Border.Color, preset.Border.Opacity);
        border.BorderThickness = new Thickness(preset.Border.Thickness);
        border.CornerRadius = new CornerRadius(preset.Border.CornerRadius);
        border.Width = definition.Width;
        border.Height = definition.Height;

        // Set on the outermost element so the title, the reading and anything else made of words inherit
        // it from one assignment. Cleared rather than given a default when the preset names nothing: an
        // explicit family would override the theme's, and "nothing" is asking to keep the theme's.
        if (string.IsNullOrWhiteSpace(preset.FontFamily)) border.ClearValue(TextElement.FontFamilyProperty);
        else TextElement.SetFontFamily(border, new FontFamily(preset.FontFamily));

        if (border.Child is not LabeledTile tile) return;

        tile.Title = definition.Title;
        tile.TitleSize = preset.Title.Size;
        tile.TitleOpacity = preset.Title.Opacity;
        tile.TitlePlacement = preset.Title.Placement;
    }

    private static void Dress(object? piece, Preset preset)
    {
        // First, and for anything that draws a reading. Kept out of the per-kind blocks below because it
        // is the one setting they all share, and repeating it in each is how the sparkline came to be the
        // only kind it did not reach.
        if (piece is IValueStyle value) value.ValueSize = preset.Value.Size;

        if (piece is IChartStyle line)
        {
            line.LineThickness = preset.Line.Thickness;
            line.Fill = preset.Line.Fill;
            line.FillOpacity = preset.Line.FillOpacity;
            line.Smoothness = preset.Line.Smoothness;
            line.PointSize = preset.Line.PointSize;
        }

        if (piece is IGaugeStyle dial)
        {
            dial.ArcThickness = preset.Gauge.Thickness;
            dial.ArcCorner = preset.Gauge.Corner;
            dial.Sweep = preset.Gauge.Sweep;
            dial.ShowValue = preset.Value.Show;
            dial.TrackBrush = Tint(preset.Gauge.Track.Color, preset.Gauge.Track.Opacity);
        }

        if (piece is IMeterStyle bar)
        {
            bar.BarThickness = preset.Bar.Thickness;
            bar.BarCorner = preset.Bar.Corner;
            bar.Layout = preset.Bar.Layout;
            bar.ShowValue = preset.Value.Show;
        }

        if (piece is IStatStyle stat)
        {
            stat.UnitSize = preset.Number.UnitSize;
            stat.ShowUnit = preset.Number.UnitSize > 0;
            stat.ShowPanel = preset.Number.Panel;
        }

        if (piece is ITextStyle text) text.Align = preset.Value.Align;
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
        definition.Surface = WidgetSurface.Screen(surface.Key);
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
    public UIElement? Build(DesktopWidgetDefinition definition, HardwareNode hardware)
    {
        var kind = WidgetCatalog.For(definition.Kind);

        var preset = _presets.For(definition.Preset);

        // Resolved in order, dropping any sensor the machine no longer reports — a definition survives a
        // driver rename, it just draws one line fewer until the sensor comes back.
        var resolved = new List<SensorNode>();
        var ramps = new List<Ramp>();

        // Kept alongside, not indexed back into definition.Series: a sensor that fails to resolve is
        // skipped, so after the first gap the two lists no longer line up.
        var chosen = new List<WidgetSeries>();

        foreach (var series in definition.Series.Take(kind.MaxSeries))
        {
            var entry = SensorCatalog.FindEntry(hardware, series.SensorIdentifier);
            if (entry is null) continue;

            resolved.Add(entry.Sensor);

            // A layout written before this existed has no metric stored, and an unjudged reading cannot
            // follow its value at all. Filled in from the catalog, which is the only thing that still knows
            // what kind of part this came from — and written onto the definition, so the next save keeps it,
            // exactly as the screen-key migration does.
            if (string.IsNullOrEmpty(series.Metric)) series.Metric = entry.Key.ToString();

            chosen.Add(series);

            // The ramp a reading is drawn in. Empty falls to the preset's primary — and one named ramp
            // short of the number of lines does too, so a three-line chart on a two-ramp preset draws.
            ramps.Add(preset.RampFor(string.IsNullOrEmpty(series.Ramp)
                ? DefaultRamp(preset, ramps.Count)
                : series.Ramp));
        }

        // A kind that does not need a reading draws anyway — a backdrop has none to lose, and a text is a
        // caption until it is given one. Only the kinds that need a reading and have none are skipped, so a
        // widget whose sensor vanished keeps its definition and comes back if the machine reports it again.
        if (kind.NeedsReading && resolved.Count == 0) return null;

        var piece = kind.Create(resolved);

        // Every reading follows its value now: a fixed colour is a ramp whose four stops are the same, so
        // there is no second path for "do not follow" to take.
        //
        // A reading with no judgeable scale — watts, RPM — never leaves Normal, which is the ramp's resting
        // colour. That is the honest answer: nothing is being claimed about a number we cannot judge.
        for (var i = 0; i < resolved.Count; i++)
            Paint(piece, kind.AccentProperties[i], resolved[i], ramps[i], MetricKey.Parse(chosen[i].Metric));

        Dress(piece, preset);
        Content(piece, definition);
        KeepEnoughHistory(definition, resolved);

        // A desktop widget sits on whatever wallpaper the user has, so it can't rely on the app's
        // background for contrast: it carries its own backing panel, which the user can restyle.
        var widget = new Border
        {
            Padding = new Thickness(10),
            Child = new LabeledTile { Tile = piece, Foreground = Brushes.White },
        };

        Frame(widget, definition, preset);

        // Set here rather than where the widget is added to a canvas, because there are two such places
        // and this is the one both of them go through.
        Panel.SetZIndex(widget, definition.ZIndex);
        return widget;
    }

    /// <summary>Spread across ramps rather than all taking the primary: two lines in the same colour are
    /// one line. Past the ramps a preset declares, they share the primary — a chart that draws is better
    /// than one that refuses because its preset was written for two lines.</summary>
    private static string DefaultRamp(Preset preset, int index)
    {
        var names = preset.Ramps.Keys.ToList();
        return index < names.Count ? names[index] : Ramp.Primary;
    }

    /// <summary>
    /// Paints one reading from its ramp, and keeps painting it.
    ///
    /// The two halves meet here and nowhere else: <see cref="VitalMonitor"/> says WHICH state a reading is
    /// in, the ramp says what that state looks like. Neither knows the other's business, which is why a
    /// preset can be swapped without changing what any number means.
    ///
    /// Re-resolved on every reading rather than bound once, because two different things move it — the
    /// value crossing a limit, and the limits themselves being changed. Torn down on Unloaded rather than
    /// tracked in a list: the element leaves the canvas whenever the widget is rebuilt, so the subscription
    /// ends itself and there is no bookkeeping to fall out of step with what is on screen.
    /// </summary>
    private static void Paint(FrameworkElement piece, DependencyProperty property,
        SensorNode sensor, Ramp ramp, MetricKey? metric)
    {
        var monitor = VitalMonitor.Current;

        Repaint();
        sensor.PropertyChanged += OnSensorChanged;
        piece.Unloaded += OnUnloaded;

        if (monitor is not null) monitor.Changed += Repaint;

        void Repaint()
        {
            // No judgeable scale means no state to be in, so it rests on the ramp's Normal. A watt count
            // measured against the percentage steps would sit at critical and mean nothing.
            var role = sensor.Value is { } value && metric is { } key
                ? monitor?.RoleFor(value, key) ?? "Normal"
                : "Normal";

            piece.SetValue(property, Tint(ramp[role], 1));
        }

        void OnSensorChanged(object? _, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(SensorNode.Value)) Repaint();
        }

        void OnUnloaded(object? _, RoutedEventArgs __)
        {
            sensor.PropertyChanged -= OnSensorChanged;
            piece.Unloaded -= OnUnloaded;
            if (monitor is not null) monitor.Changed -= Repaint;
        }
    }

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
