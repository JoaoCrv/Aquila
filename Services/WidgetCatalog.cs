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

    /// <summary>
    /// Whether this kind is useless without a reading.
    ///
    /// Not the same question as <see cref="MaxSeries"/>, which is only a ceiling. They coincided for every
    /// kind until Text arrived: it takes one reading and needs none, being a caption on its own and a
    /// caption with a number beside it when given one.
    /// </summary>
    public bool NeedsReading { get; init; } = true;

    /// <summary>Whether this kind shows words the user writes.</summary>
    public bool HasCaption { get; init; }

    /// <summary>Whether this kind shows the time or the date.</summary>
    public bool HasClock { get; init; }

    /// <summary>Whether this kind is drawn as a line, and so has line settings worth showing. Declared here
    /// with everything else about a kind rather than worked out by asking a freshly built piece what
    /// interfaces it implements — the editor needs the answer before it builds anything.</summary>
    public bool HasLine { get; init; }

    /// <summary>Whether this kind is drawn as a dial, and so has dial settings worth showing.</summary>
    public bool HasDial { get; init; }

    /// <summary>Whether this kind is drawn as a bar, and so has bar settings worth showing.</summary>
    public bool HasBar { get; init; }

    /// <summary>Whether this kind is just a number, and so has number settings worth showing.</summary>
    public bool HasNumber { get; init; }

    /// <summary>Whether it draws a reading whose size the preset sets. False for the two kinds made of
    /// nothing but shape — a full chart and a backdrop — so the editor does not offer a size for text
    /// they never draw.</summary>
    public bool HasValue { get; init; } = true;

    /// <summary>Whether its reading comes with a unit, and so whether offering to hide one means anything.
    /// False for the clock — it draws a reading and there is no unit a time could carry — and for the two
    /// kinds that draw no reading at all.</summary>
    public bool HasUnit { get; init; } = true;
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
            [StatBox.AccentProperty]) { HasNumber = true },

        new(DesktopWidgetKind.SparklineChart,
            "Chart",
            "The same trend as a sparkline, with room to read it. Takes two readings.",
            300, 160,
            BuildChart,
            [SparklineChart.SeriesColorProperty, SparklineChart.SecondColorProperty])
            { HasLine = true, HasValue = false, HasUnit = false },

        // No accent properties, so MaxSeries is 0 — the first kind that reads nothing at all. Everything it
        // draws, the widget's own Border already draws: colour, opacity, corners, border. The piece is empty
        // on purpose rather than a rectangle of its own, which would be a second one behind the first.
        new(DesktopWidgetKind.Backdrop,
            "Backdrop",
            "A plate to sit behind other widgets. Reads nothing.",
            240, 160,
            _ => new Grid(),
            []) { NeedsReading = false, HasValue = false, HasUnit = false },

        // One accent property, so it MAY take a reading; NeedsReading false, so it does not have to. It is
        // the first kind where the ceiling and the requirement differ.
        new(DesktopWidgetKind.Text,
            "Text",
            "Words of your own, alone or beside a reading.",
            200, 60,
            series => new TextTile { Sensor = series.Count > 0 ? series[0] : null },
            [TextBlock.ForegroundProperty]) { NeedsReading = false, HasCaption = true },

        new(DesktopWidgetKind.Clock,
            "Clock",
            "The time or the date, in this machine's own format.",
            200, 70,
            _ => new ClockTile(),
            []) { NeedsReading = false, HasClock = true, HasUnit = false },
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

        // The unit is set beside the number rather than baked into its format string. A format string is
        // fixed when the binding is made, so a preset turning the unit off would have needed the widget
        // rebuilt — and a rebuild is for structure, never for style.
        meter.Unit = sensor.Unit ?? string.Empty;

        meter.SetBinding(SensorMeter.ValueTextProperty, new Binding(nameof(SensorNode.Value))
        {
            Source = sensor,
            StringFormat = $"{{0:{SensorFormat.Decimals(sensor.Unit)}}}",
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
            StringFormat = $"{{0:{SensorFormat.Decimals(sensor.Unit)}}}",
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

}
