using System.Globalization;
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
/// <param name="Accent">How many readings this kind draws and where each one's colour goes. A kind
/// cannot accept a reading it has nowhere to colour, so the editor and the builder can never disagree
/// about how many are allowed.</param>
public sealed record WidgetKindInfo(
    DesktopWidgetKind Kind,
    string Name,
    string Description,
    double DefaultWidth,
    double DefaultHeight,
    Func<IReadOnlyList<SensorNode>, FrameworkElement> Create,
    SeriesPaint Accent)
{
    public int MaxSeries => Accent.Max;

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

    /// <summary>Whether this kind can be turned on its side. The meter is a bar and cannot: its label and
    /// its value sit beside it in a layout that reads one way only.</summary>
    public bool HasDirection { get; init; }

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
            SeriesPaint.Properties(RadialGauge.AccentProperty)) { HasDial = true },

        new(DesktopWidgetKind.MiniSparkline,
            "Sparkline",
            "A small line chart of the recent history.",
            240, 100,
            series => new MiniSparkline { Sensor = series[0] },
            SeriesPaint.Properties(MiniSparkline.AccentProperty)) { HasLine = true },

        new(DesktopWidgetKind.SensorMeter,
            "Meter",
            "A bar with the value beside it.",
            240, 80,
            series => BuildMeter(series[0]),
            SeriesPaint.Properties(SensorMeter.AccentProperty)) { HasBar = true },

        new(DesktopWidgetKind.StatBox,
            "Number",
            "Just the reading, large, with its unit.",
            150, 120,
            series => BuildStatBox(series[0]),
            SeriesPaint.Properties(StatBox.AccentProperty)) { HasNumber = true },

        new(DesktopWidgetKind.SparklineChart,
            "Chart",
            "The same trend as a sparkline, with room to read it. Takes two readings.",
            300, 160,
            BuildChart,
            SeriesPaint.Properties(
                SparklineChart.SeriesColorProperty, SparklineChart.SecondColorProperty))
            { HasLine = true, HasValue = false, HasUnit = false },

        // Nowhere to put a colour, so MaxSeries is 0 — the first kind that reads nothing at all. Everything
        // it draws, the widget's own Border already draws: colour, opacity, corners, border. The piece is
        // empty on purpose rather than a rectangle of its own, which would be a second one behind the first.
        new(DesktopWidgetKind.Backdrop,
            "Backdrop",
            "A plate to sit behind other widgets. Reads nothing.",
            240, 160,
            _ => new Grid(),
            SeriesPaint.None) { NeedsReading = false, HasValue = false, HasUnit = false },

        // One accent property, so it MAY take a reading; NeedsReading false, so it does not have to. It is
        // the first kind where the ceiling and the requirement differ.
        new(DesktopWidgetKind.Text,
            "Text",
            "Words of your own, alone or beside a reading.",
            200, 60,
            series => new TextTile { Sensor = series.Count > 0 ? series[0] : null },
            SeriesPaint.Properties(TextBlock.ForegroundProperty))
            { NeedsReading = false, HasCaption = true },

        new(DesktopWidgetKind.Clock,
            "Clock",
            "The time or the date, in this machine's own format.",
            200, 70,
            _ => new ClockTile(),
            SeriesPaint.None) { NeedsReading = false, HasClock = true, HasUnit = false },

        // The first kind whose count the hardware decides. Sixteen is a ceiling, not a shape: this machine
        // has sixteen CPU cores and the editor stops offering more there, but four readings make four bars.
        // Each keeps its own ramp, so a core going amber says so on its own.
        new(DesktopWidgetKind.Bars,
            "Bars",
            "A row of vertical bars, one per reading. Good for per-core load.",
            260, 140,
            series => new SensorBars(series),
            SeriesPaint.Many(16, (piece, i, brush) => ((SensorBars)piece).PaintBar(i, brush)))
            { HasBar = true, HasDirection = true },
    ];

    /// <summary>Falls back to the first kind rather than throwing: the kind comes from widgets.json, which
    /// a user can hand-edit and an older build may have written before a kind was renamed.</summary>
    public static WidgetKindInfo For(DesktopWidgetKind kind) =>
        All.FirstOrDefault(k => k.Kind == kind) ?? All[0];

    /// <summary>
    /// SensorMeter takes the displayed number as a pre-formatted string — the Sensor property only drives
    /// the bar — the same convention as StatBox, where the caller decides the format. The dashboard cards
    /// hardcode both format and unit because each knows its sensor; a generic widget cannot, so it asks
    /// SensorFormat, through the converter below.
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

        // The unit is bound rather than assigned, because for a throughput sensor it is not fixed: the
        // same drive reads B/s when idle and MB/s when busy, and the unit has to follow the number. It is
        // still separate FROM the number, so a preset turning the unit off stays a restyle and never a
        // rebuild.
        Bind(meter, SensorMeter.ValueTextProperty, SensorMeter.UnitProperty, sensor);

        return meter;
    }

    /// <summary>
    /// StatBox takes its number pre-formatted too, and carries no sensor of its own — so it needs a
    /// binding rather than an assignment. No Label: the widget already has a title above it, and a second
    /// caption inside would only repeat it.
    /// </summary>
    private static StatBox BuildStatBox(SensorNode sensor)
    {
        var box = new StatBox();

        Bind(box, StatBox.ValueProperty, StatBox.UnitProperty, sensor);

        return box;
    }

    /// <summary>
    /// Points a piece's number and its unit at one sensor, both formatted by <see cref="SensorFormat"/>.
    ///
    /// Two bindings off the same value, because the unit is not a constant: a throughput sensor reads B/s
    /// idle and MB/s busy, and a unit assigned once would go on claiming bytes while the number had moved
    /// to megabytes. One converter instance serves both — it holds the sensor's own unit, which is what
    /// decides whether there is anything to scale at all.
    /// </summary>
    private static void Bind(
        DependencyObject target, DependencyProperty number, DependencyProperty unit, SensorNode sensor)
    {
        var format = new ReadingPart(sensor.Unit);

        BindingOperations.SetBinding(target, number, new Binding(nameof(SensorNode.Value))
        {
            Source = sensor,
            Converter = format,
            FallbackValue = "--",
        });

        BindingOperations.SetBinding(target, unit, new Binding(nameof(SensorNode.Value))
        {
            Source = sensor,
            Converter = format,
            ConverterParameter = "unit",
            FallbackValue = string.Empty,
        });
    }

    /// <summary>One half of a formatted reading, chosen by the parameter. Private because it exists for
    /// the two pieces that take their number as a pre-formatted string; everything else asks SensorFormat
    /// directly.</summary>
    private sealed class ReadingPart(string? unit) : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var (text, scaled) = SensorFormat.Parts(value as float?, unit);
            return string.Equals(parameter as string, "unit", StringComparison.Ordinal) ? scaled : text;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
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
