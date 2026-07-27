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
/// First widget slice of #24: a fixed set of widgets bound to real sensors, so live data on the desktop
/// is proven end to end. The <see cref="WidgetPlacement"/> list below is deliberately shaped like what
/// will later be persisted (kind + sensor + position) — the pin-from-Explorer flow and persistence
/// replace where the list comes from, not how widgets are built.
/// </summary>
public sealed class DesktopWidgetService(AquilaService aquila, DesktopSurfaceService surfaces)
{
    private enum WidgetKind { RadialGauge, MiniSparkline, SensorMeter }

    private readonly record struct WidgetPlacement(
        WidgetKind Kind, string Title, Func<HardwareNode, SensorNode?> Sensor,
        string AccentKey, double X, double Y, double Width, double Height);

    // Seed layout, top-left of the primary screen, below the screen-info label.
    private static readonly WidgetPlacement[] _seed =
    [
        new(WidgetKind.RadialGauge,  "CPU Load",  h => h.Cpus.Count > 0 ? h.Cpus[0].Load.Total : null,
            "Aquila.Cpu",   32, 150, 170, 190),
        new(WidgetKind.MiniSparkline, "CPU Temp", h => h.Cpus.Count > 0 ? h.Cpus[0].Temperature.Primary : null,
            "Aquila.Temp",  32, 360, 240, 100),
        new(WidgetKind.SensorMeter,  "CPU Power", h => h.Cpus.Count > 0 ? h.Cpus[0].Power.Package : null,
            "Aquila.Power", 32, 480, 240,  80),
    ];

    public void Populate()
    {
        // Never assume Screen.AllScreens order puts the primary first — it isn't guaranteed (the same
        // reason #24 won't trust that order for screen numbering). Match on the primary's bounds instead.
        var primaryBounds = System.Windows.Forms.Screen.PrimaryScreen?.Bounds;
        var live = surfaces.Surfaces;
        var surface = live.FirstOrDefault(s => s.Bounds == primaryBounds);
        if (surface.Canvas is null) surface = live.FirstOrDefault();
        if (surface.Canvas is null) return; // surface not shown (feature disabled)

        var hardware = aquila.State.Hardware;

        foreach (var placement in _seed)
        {
            // Sensors vary by machine — skip anything this hardware doesn't expose rather than showing an
            // empty widget.
            var sensor = placement.Sensor(hardware);
            if (sensor is null) continue;

            var widget = Build(placement, sensor);
            Canvas.SetLeft(widget, placement.X);
            Canvas.SetTop(widget, placement.Y);
            surface.Canvas.Children.Add(widget);
        }
    }

    /// <summary>Builds one #23 control bound to a live <see cref="SensorNode"/>. The controls watch the
    /// node's INPC, so assigning it here is all that's needed — they follow the existing AquilaService
    /// tick with no extra timer.</summary>
    private static UIElement Build(WidgetPlacement placement, SensorNode sensor)
    {
        FrameworkElement piece = placement.Kind switch
        {
            WidgetKind.RadialGauge => new RadialGauge { Sensor = sensor },
            WidgetKind.MiniSparkline => new MiniSparkline { Sensor = sensor },
            WidgetKind.SensorMeter => new SensorMeter { Sensor = sensor, LabelPlacement = MeterLabelPlacement.Inline },
            _ => throw new NotSupportedException($"Unknown widget kind {placement.Kind}"),
        };

        // SetResourceReference is the code equivalent of DynamicResource — accent brushes are swapped on
        // theme change (App.RefreshAccentBrushes), so a static lookup would freeze the old theme's colour.
        piece.SetResourceReference(
            placement.Kind switch
            {
                WidgetKind.RadialGauge => RadialGauge.AccentProperty,
                WidgetKind.MiniSparkline => MiniSparkline.AccentProperty,
                _ => SensorMeter.AccentProperty,
            },
            placement.AccentKey);

        // A desktop widget sits on whatever wallpaper the user has, so it can't rely on the app's
        // background for contrast: give it its own dim backing panel and light text.
        return new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(0x99, 0x00, 0x00, 0x00)),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(10),
            Width = placement.Width,
            Height = placement.Height,
            Child = new LabeledTile
            {
                Title = placement.Title,
                Tile = piece,
                Foreground = Brushes.White,
            },
        };
    }
}
