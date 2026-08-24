using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using Aquila.Controls;
using Aquila.Models;

namespace Aquila.Services;

/// <summary>
/// One kind of desktop widget, described in one place: what it is called, how big it starts, how to build
/// it from the readings it was given, and where each reading's colour goes.
/// </summary>
/// <param name="Name">Shown in the editor's type picker.</param>
/// <param name="Description">One line telling the user what this is good for.</param>
/// <param name="Create">Builds the piece from the resolved sensors, in the order the user added them.
/// Never called with an empty list. The controls watch each node's INPC, so handing them over is all that
/// is needed — they follow the existing AquilaService tick with no extra timer.</param>
/// <param name="AccentProperties">One property per series this kind can draw, in the same order. Its
/// length IS the limit — a kind cannot accept a reading it has nowhere to colour, so the editor and the
/// builder can never disagree about how many are allowed.</param>
public sealed record WidgetKindInfo(
    DesktopWidgetKind Kind,
    string Name,
    string Description,
    double DefaultWidth,
    double DefaultHeight,
    Func<IReadOnlyList<SensorNode>, FrameworkElement> Create,
    IReadOnlyList<DependencyProperty> AccentProperties)
{
    public int MaxSeries => AccentProperties.Count;

    /// <summary>Whether this kind is drawn as a line, and so has line settings worth showing. Declared here
    /// with everything else about a kind rather than worked out by asking a freshly built piece what
    /// interfaces it implements — the editor needs the answer before it builds anything.</summary>
    public bool HasLine { get; init; }

    /// <summary>Whether this kind is drawn as a dial, and so has dial settings worth showing.</summary>
    public bool HasDial { get; init; }

    /// <summary>Whether this kind is drawn as a bar, and so has bar settings worth showing.</summary>
    public bool HasBar { get; init; }
}

public static class WidgetCatalog
{
    public static IReadOnlyList<WidgetKindInfo> All { get; } =
    [
        new(DesktopWidgetKind.RadialGauge,
            "Radial gauge",
            "A dial. Best for a percentage, like load.",
            170, 190,
            series => new RadialGauge { Sensor = series[0] },
            [RadialGauge.AccentProperty]) { HasDial = true },

        new(DesktopWidgetKind.MiniSparkline,
            "Sparkline",
            "A small line chart of the recent history.",
            240, 100,
            series => new MiniSparkline { Sensor = series[0] },
            [MiniSparkline.AccentProperty]) { HasLine = true },

        new(DesktopWidgetKind.SensorMeter,
            "Meter",
            "A bar with the value beside it.",
            240, 80,
            series => BuildMeter(series[0]),
            [SensorMeter.AccentProperty]) { HasBar = true },

        new(DesktopWidgetKind.StatBox,
            "Number",
            "Just the reading, large, with its unit.",
            150, 120,
            series => BuildStatBox(series[0]),
            [StatBox.AccentProperty]),

        new(DesktopWidgetKind.SparklineChart,
            "Chart",
            "The same trend as a sparkline, with room to read it. Takes two readings.",
            300, 160,
            BuildChart,
            [SparklineChart.SeriesColorProperty, SparklineChart.SecondColorProperty]) { HasLine = true },
    ];

    /// <summary>Falls back to the first kind rather than throwing: the kind comes from widgets.json, which
    /// a user can hand-edit and an older build may have written before a kind was renamed.</summary>
    public static WidgetKindInfo For(DesktopWidgetKind kind) =>
        All.FirstOrDefault(k => k.Kind == kind) ?? All[0];

    /// <summary>
    /// SensorMeter takes the displayed number as a pre-formatted string — the Sensor property only drives
    /// the bar — the same convention as StatBox, where the caller decides the format. The dashboard cards
    /// hardcode both format and unit because each knows its sensor; a generic widget cannot, so it binds
    /// Value and bakes that sensor's own unit into the format string.
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

        var unit = string.IsNullOrWhiteSpace(sensor.Unit) ? "" : " " + sensor.Unit;

        meter.SetBinding(SensorMeter.ValueTextProperty, new Binding(nameof(SensorNode.Value))
        {
            Source = sensor,
            StringFormat = $"{{0:{Decimals(sensor)}}}{unit}",
            FallbackValue = "--",
        });

        return meter;
    }

    /// <summary>
    /// StatBox takes its number pre-formatted too, and carries no sensor of its own — so it needs a
    /// binding rather than an assignment. No Label: the widget already has a title above it, and a second
    /// caption inside would only repeat it.
    /// </summary>
    private static StatBox BuildStatBox(SensorNode sensor)
    {
        var box = new StatBox { Unit = sensor.Unit ?? string.Empty };

        box.SetBinding(StatBox.ValueProperty, new Binding(nameof(SensorNode.Value))
        {
            Source = sensor,
            StringFormat = $"{{0:{Decimals(sensor)}}}",
            FallbackValue = "--",
        });

        return box;
    }

    /// <summary>
    /// The chart reads the node's rolling history directly — an ObservableCollection the sensor already
    /// maintains, so nothing here has to poll or copy. It holds no SensorNode, which is why it is not an
    /// ISensorPiece: its subscription is to a collection, and it tears that down on Unloaded by itself.
    ///
    /// A percentage gets a fixed 0–100 ceiling. Anything else auto-scales, because there is no honest
    /// upper bound to assume for watts or bytes per second.
    /// </summary>
    private static SparklineChart BuildChart(IReadOnlyList<SensorNode> series) => new()
    {
        Values = series[0].History,
        SecondValues = series.Count > 1 ? series[1].History : null,
        MaxY = series[0].Unit == "%" ? 100 : double.NaN,
    };

    /// <summary>Volts need decimals to mean anything; everything else reads better rounded.</summary>
    private static string Decimals(SensorNode sensor) => sensor.Unit == "V" ? "F2" : "F0";
}
