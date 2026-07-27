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

    private void OnWidgetMoved(DesktopSurfaceService.Surface surface, UIElement element, double x, double y)
    {
        if (!_byElement.TryGetValue(element, out var definition)) return;

        definition.X = x;
        definition.Y = y;
        if (_widgets is not null) _layout.Save(_widgets);
    }

    /// <summary>Builds one #23 control bound to a live <see cref="SensorNode"/>. The controls watch the
    /// node's INPC, so assigning it here is all that's needed — they follow the existing AquilaService
    /// tick with no extra timer.</summary>
    private static UIElement Build(DesktopWidgetDefinition definition, SensorNode sensor)
    {
        FrameworkElement piece = definition.Kind switch
        {
            DesktopWidgetKind.RadialGauge => new RadialGauge { Sensor = sensor },
            DesktopWidgetKind.MiniSparkline => new MiniSparkline { Sensor = sensor },
            DesktopWidgetKind.SensorMeter => new SensorMeter { Sensor = sensor, LabelPlacement = MeterLabelPlacement.Inline },
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
        // background for contrast: give it its own dim backing panel and light text.
        return new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(0x99, 0x00, 0x00, 0x00)),
            CornerRadius = new CornerRadius(8),
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
}
