using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Aquila.Controls;
using Aquila.Desktop;
using Aquila.Models;

namespace Aquila.Services;

/// <summary>
/// Puts live widgets on the desktop surface. This is the DOMAIN side of the boundary: it knows about
/// sensors and #23 controls and reaches IN to <see cref="DesktopSurfaceService"/>'s canvases —
/// <see cref="Aquila.Desktop"/> itself stays free of any Aquila type so it remains extractable.
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
        // Aquila.Desktop must not learn what a widget means.
        _surfaces.WidgetRightClicked += OnWidgetRightClicked;
    }

    /// <summary>Starter layout for a machine with no widgets.json yet — sensor lookups rather than fixed
    /// identifiers, since those differ per machine; whatever isn't present is simply skipped.</summary>
    private static readonly (DesktopWidgetKind Kind, string Title, Func<HardwareNode, SensorNode?> Sensor,
        string AccentKey, double X, double Y, double Width, double Height)[] _starter =
    [
        (DesktopWidgetKind.RadialGauge,   "CPU Load",  h => h.Cpus.Count > 0 ? h.Cpus[0].Load.Total : null,
            "Aquila.Cpu",   32, 150, 170, 190),
        (DesktopWidgetKind.MiniSparkline, "CPU Temp",  h => h.Cpus.Count > 0 ? h.Cpus[0].Temperature.Primary : null,
            "Aquila.Temp",  32, 360, 240, 100),
        (DesktopWidgetKind.SensorMeter,   "CPU Power", h => h.Cpus.Count > 0 ? h.Cpus[0].Power.Package : null,
            "Aquila.Power", 32, 480, 240,  80),
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
            // A sensor can vanish between runs (hardware changed, driver renamed it). Skip rather than
            // showing an empty widget; the definition is kept so it comes back if the sensor does.
            var sensor = SensorCatalog.FindByIdentifier(hardware, definition.SensorIdentifier);
            if (sensor is null) continue;

            var surface = ResolveSurface(surfaces, definition, ref migrated);
            var element = Build(definition, sensor);

            Canvas.SetLeft(element, definition.X);
            Canvas.SetTop(element, definition.Y);
            surface.Canvas.Children.Add(element);
            _byElement[element] = definition;
        }

        if (migrated) _layout.Save(_widgets);
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
        if (saved.Count > 0) return saved;

        var screenKey = PrimarySurface(_surfaces.Surfaces).Key;

        var seeded = new List<DesktopWidgetDefinition>();
        foreach (var (kind, title, lookup, accent, x, y, width, height) in _starter)
        {
            var identifier = lookup(hardware)?.Identifier;
            if (string.IsNullOrEmpty(identifier)) continue;

            seeded.Add(new DesktopWidgetDefinition
            {
                Kind = kind,
                SensorIdentifier = identifier,
                Title = title,
                AccentKey = accent,
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
        _layout.Save(_widgets);
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

    /// <summary>Opens the editor pre-filled from the stored definition — same dialog as adding.</summary>
    public void EditWidget(DesktopWidgetDefinition definition) => OpenEditor(definition, null);

    /// <summary>
    /// The one way in to the editor, for all three entry points.
    ///
    /// There is no separate "preview": the widget list IS the state, and the desktop is a projection of
    /// it, so editing mutates the real definition and the canvas re-renders. That removes the whole
    /// duality of a preview element standing beside the real one — which is what previously produced a
    /// duplicated widget and a broken binding. Cancelling restores a snapshot taken on entry; the file is
    /// only written on accept, so a crash mid-edit can't leave behind a state the user never confirmed.
    /// </summary>
    public void OpenEditor(DesktopWidgetDefinition? existing, string? presetSensorIdentifier)
    {
        // There has to be a surface to edit on. Adding a widget implies wanting to see it anyway.
        if (!_surfaces.IsShown)
        {
            _surfaces.Show();
            Populate();
        }

        _widgets ??= _layout.Load();

        // Adding and editing are the same operation on the same data: a new widget is simply a definition
        // that starts empty and is added to the list up front, so it renders live like any other.
        var isNew = existing is null;
        var target = existing ?? NewDefinition(presetSensorIdentifier);
        if (isNew) _widgets.Add(target);

        var snapshot = target.Clone();

        var main = Application.Current?.Windows.OfType<Views.Windows.MainWindow>().FirstOrDefault();
        var restore = main?.WindowState ?? WindowState.Normal;
        if (main is not null) main.WindowState = WindowState.Minimized;

        var dialog = new Views.Windows.WidgetEditorWindow(_aquila.State.Hardware, target, isNew, presetSensorIdentifier);
        dialog.ViewModel.Changed += () => RefreshWidget(target);

        var accepted = dialog.ShowDialog() == true;

        if (accepted)
        {
            _layout.Save(_widgets);
        }
        else if (isNew)
        {
            _widgets.Remove(target);
            RemoveElementFor(target);
        }
        else
        {
            target.CopyFrom(snapshot);
            RefreshWidget(target);
        }

        if (main is not null)
        {
            main.WindowState = restore;
            main.Activate();
        }
    }

    private DesktopWidgetDefinition NewDefinition(string? presetSensorIdentifier) => new()
    {
        SensorIdentifier = presetSensorIdentifier ?? string.Empty,
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
        // sensor picked yet, and that must not throw.
        var sensor = SensorCatalog.FindByIdentifier(_aquila.State.Hardware, definition.SensorIdentifier);
        if (sensor is null) return;

        var migrated = false;
        var surface = ResolveSurface(surfaces, definition, ref migrated);
        var element = Build(definition, sensor);

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

    /// <summary>Clears the Sensor on whichever piece is inside a built widget, so it unhooks itself.
    /// Mirrors the structure Build produces: Border → LabeledTile → piece.</summary>
    private static void DetachSensor(UIElement widget)
    {
        if (widget is not Border { Child: LabeledTile tile }) return;

        switch (tile.Tile)
        {
            case RadialGauge gauge: gauge.Sensor = null; break;
            case MiniSparkline sparkline: sparkline.Sensor = null; break;
            case SensorMeter meter: meter.Sensor = null; break;
        }
    }

    private void OnWidgetMoved(DesktopSurfaceService.Surface surface, UIElement element, double x, double y)
    {
        if (!_byElement.TryGetValue(element, out var definition)) return;

        definition.X = x;
        definition.Y = y;
        if (_widgets is not null) _layout.Save(_widgets);
    }

    private void OnWidgetResized(UIElement element, double width, double height)
    {
        if (!_byElement.TryGetValue(element, out var definition)) return;

        definition.Width = width;
        definition.Height = height;
        if (_widgets is not null) _layout.Save(_widgets);
    }

    private void OnWidgetScreenChanged(DesktopSurfaceService.Surface surface, UIElement element, double x, double y)
    {
        if (!_byElement.TryGetValue(element, out var definition)) return;

        // Sending a widget to a screen is an explicit choice, so this DOES rewrite the stored monitor —
        // unlike the fallback when a monitor is merely missing, which keeps the original.
        definition.ScreenKey = surface.Key;
        definition.X = x;
        definition.Y = y;
        if (_widgets is not null) _layout.Save(_widgets);
    }

    /// <summary>
    /// SensorMeter takes the displayed number as a pre-formatted string (the Sensor property only drives
    /// the bar) — same convention as StatBox, where the caller decides the format. The dashboard cards
    /// hardcode both the format and the unit because each knows its sensor; a generic widget can't, so it
    /// binds Value and bakes that sensor's own unit into the format string.
    /// </summary>
    private static SensorMeter BuildMeter(SensorNode sensor)
    {
        var meter = new SensorMeter
        {
            Sensor = sensor,
            LabelPlacement = MeterLabelPlacement.Inline,
            // The widget already carries a title, so the inline label column would just be an empty gap.
            LabelWidth = new GridLength(0),
            ValueWidth = new GridLength(64),
        };

        // Volts need decimals to mean anything; everything else reads better rounded.
        var decimals = sensor.Unit == "V" ? "F2" : "F0";
        var unit = string.IsNullOrWhiteSpace(sensor.Unit) ? "" : " " + sensor.Unit;

        meter.SetBinding(SensorMeter.ValueTextProperty, new System.Windows.Data.Binding(nameof(SensorNode.Value))
        {
            Source = sensor,
            StringFormat = $"{{0:{decimals}}}{unit}",
            FallbackValue = "--",
        });

        return meter;
    }

    /// <summary>Builds one #23 control bound to a live <see cref="SensorNode"/>. The controls watch the
    /// node's INPC, so assigning it here is all that's needed — they follow the existing AquilaService
    /// tick with no extra timer. Public so the editor previews exactly what will land on the desktop,
    /// rather than an approximation that can drift.</summary>
    public static UIElement Build(DesktopWidgetDefinition definition, SensorNode sensor)
    {
        FrameworkElement piece = definition.Kind switch
        {
            DesktopWidgetKind.RadialGauge => new RadialGauge { Sensor = sensor },
            DesktopWidgetKind.MiniSparkline => new MiniSparkline { Sensor = sensor },
            DesktopWidgetKind.SensorMeter => BuildMeter(sensor),
            _ => throw new NotSupportedException($"Unknown widget kind {definition.Kind}"),
        };

        // SetResourceReference is the code equivalent of DynamicResource — accent brushes are swapped on
        // theme change (App.RefreshAccentBrushes), so a static lookup would freeze the old theme's colour.
        piece.SetResourceReference(
            definition.Kind switch
            {
                DesktopWidgetKind.RadialGauge => RadialGauge.AccentProperty,
                DesktopWidgetKind.MiniSparkline => MiniSparkline.AccentProperty,
                _ => SensorMeter.AccentProperty,
            },
            definition.AccentKey);

        // A desktop widget sits on whatever wallpaper the user has, so it can't rely on the app's
        // background for contrast: it carries its own backing panel, which the user can restyle.
        return new Border
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
