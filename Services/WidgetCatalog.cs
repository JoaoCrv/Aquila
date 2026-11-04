using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using Aquila.Controls;
using Aquila.Models;
using Aquila.Models.Nodes;

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

    /// <summary>The kind's icon wherever widgets are listed by kind — the edit bar's list, and the type gallery.
    /// </summary>
    public Wpf.Ui.Controls.SymbolRegular Icon { get; init; } = Wpf.Ui.Controls.SymbolRegular.Square24;

    /// <summary>What a widget made this kind NOW starts with, beyond its size: a clock its rings, a list its bars,
    /// trends and total. Only when it is made — a widget written before these existed reads them off, so loading one
    /// never applies this. Asked by the editor when a kind is picked and by Add widget alike.</summary>
    public Action<DesktopWidgetDefinition>? MadeNow { get; init; }

    /// <summary>Whether a widget made this kind starts behind the others — a plate exists to back them. The backdrop
    /// alone: the rule was "reads nothing", which a clock does too, and a clock chosen went behind everything.</summary>
    public bool SitsBehind { get; init; }

    /// <summary>What the kind is, in a few words, under its name in the panel's type row. The
    /// <see cref="Description"/> says what it is good for, and the gallery shows that one.</summary>
    public string Summary { get; init; } = string.Empty;

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

    /// <summary>
    /// Builds the piece from a list, for a kind whose one reading is a list rather than a sensor — and what the
    /// desktop builds such a kind with. Null for every other kind.
    ///
    /// <see cref="Create"/> still answers for a list kind, drawing the sensors it is handed as fixed rows: that
    /// is the type picker's preview, which must show what the kind looks like without watching a list, since
    /// watching one is what starts its source reading.
    /// </summary>
    public Func<ListNode, FrameworkElement>? CreateList { get; init; }

    /// <summary>Whether this kind draws a list, and so is offered lists rather than sensors and has a number of
    /// rows worth asking about. Derived rather than declared, so the two cannot disagree — the argument
    /// <see cref="SeriesPaint"/> makes about its count and its painter.</summary>
    public bool HasList => CreateList is not null;

    /// <summary>
    /// The size a widget's preset sizes are meant at — see <see cref="WidgetScale"/>. The kind's default, unless the
    /// kind draws a different figure depending on what it shows. Null for every kind that does not.
    ///
    /// The clock is the one that does: a line of text is not a ring, and a clock without rings was 200×70 before
    /// rings existed. Measured against the ringed clock's 200×200, every clock written by 2.3.0 would have been
    /// drawn at a third of its size.
    /// </summary>
    public Func<DesktopWidgetDefinition, (double Width, double Height)>? Reference { get; init; }

    /// <summary>
    /// Whether a widget of this kind draws a figure with a shape of its own, which it is given — its reference's
    /// proportions, see <see cref="WidgetScale.Proportioned"/> — at the moment it becomes one: made a dial, or a
    /// clock given rings, so the figure is not drawn a third of its size in a box made for something else. Null for
    /// every kind that does not.
    ///
    /// Only then. Every resize after it is free, the same for every kind (João, 2026-11-02): a dial in a wide box is a
    /// dial with room beside it, and the editor's lock or Shift keeps whatever shape a widget has.
    ///
    /// A predicate on the widget rather than a flag, for the clock: with rings it is round, without them it is a
    /// line of text, as free as a caption.
    /// </summary>
    public Func<DesktopWidgetDefinition, bool>? HasShape { get; init; }
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
            SeriesPaint.Properties(RadialGauge.AccentProperty))
            { HasDial = true, HasShape = _ => true, Icon = Wpf.Ui.Controls.SymbolRegular.Gauge24, Summary = "One reading as a dial" },

        new(DesktopWidgetKind.MiniSparkline,
            "Sparkline",
            "A small line chart of the recent history.",
            240, 100,
            series => new MiniSparkline { Sensor = series[0] },
            SeriesPaint.Properties(MiniSparkline.AccentProperty))
            { HasLine = true, Icon = Wpf.Ui.Controls.SymbolRegular.DataLine24, Summary = "One reading and its recent history" },

        new(DesktopWidgetKind.SensorMeter,
            "Meter",
            "A bar with the value beside it.",
            240, 80,
            series => BuildMeter(series[0]),
            SeriesPaint.Properties(SensorMeter.AccentProperty))
            { HasBar = true, Icon = Wpf.Ui.Controls.SymbolRegular.DataBarHorizontal24, Summary = "One reading as a bar" },

        new(DesktopWidgetKind.StatBox,
            "Number",
            "Just the reading, large, with its unit.",
            150, 120,
            series => BuildStatBox(series[0]),
            SeriesPaint.Properties(StatBox.AccentProperty))
            { HasNumber = true, Icon = Wpf.Ui.Controls.SymbolRegular.NumberSymbol24, Summary = "One reading, large" },

        new(DesktopWidgetKind.SparklineChart,
            "Chart",
            "The same trend as a sparkline, with room to read it. Takes two readings.",
            300, 160,
            BuildChart,
            SeriesPaint.Properties(
                SparklineChart.SeriesColorProperty, SparklineChart.SecondColorProperty))
            { HasLine = true, HasValue = false, HasUnit = false, Icon = Wpf.Ui.Controls.SymbolRegular.DataTrending24, Summary = "Two readings and their history" },

        // Nowhere to put a colour, so MaxSeries is 0 — the first kind that reads nothing at all. Everything
        // it draws, the widget's own Border already draws: colour, opacity, corners, border. The piece is
        // empty on purpose rather than a rectangle of its own, which would be a second one behind the first.
        new(DesktopWidgetKind.Backdrop,
            "Backdrop",
            "A plate to sit behind other widgets. Reads nothing.",
            240, 160,
            _ => new Grid(),
            SeriesPaint.None)
            { NeedsReading = false, HasValue = false, HasUnit = false, SitsBehind = true, Icon = Wpf.Ui.Controls.SymbolRegular.SelectObject24, Summary = "A plate behind other widgets" },

        // One accent property, so it MAY take a reading; NeedsReading false, so it does not have to. It is
        // the first kind where the ceiling and the requirement differ.
        new(DesktopWidgetKind.Text,
            "Text",
            "Words of your own, alone or beside a reading.",
            200, 60,
            series => new TextTile { Sensor = series.Count > 0 ? series[0] : null },
            SeriesPaint.Properties(TextBlock.ForegroundProperty))
            { NeedsReading = false, HasCaption = true, Icon = Wpf.Ui.Controls.SymbolRegular.TextT24, Summary = "Your own words, with a reading if you like" },

        // Square since the rings, which a clock made today starts with, and large enough for "10:09 AM" to sit
        // inside three of them at the preset's usual size. A clock from before them keeps the size it was given —
        // a layout stores its own.
        new(DesktopWidgetKind.Clock,
            "Clock",
            "The time in rings of seconds, minutes and hours — or the time or the date alone, in this machine's own format.",
            200, 200,
            _ => new ClockTile(),
            SeriesPaint.None)
            {
                NeedsReading = false, HasClock = true, HasUnit = false, Icon = Wpf.Ui.Controls.SymbolRegular.Clock24, Summary = "The time, in rings or as text",
                Reference = clock => clock.Rings == ClockRings.None ? (200, 70) : (200, 200),
                HasShape = clock => clock.Rings != ClockRings.None,
                MadeNow = clock => clock.Rings = ClockRings.All,
            },

        // The first kind whose count the hardware decides. Sixteen is a ceiling, not a shape: this machine
        // has sixteen CPU cores and the editor stops offering more there, but four readings make four bars.
        // Each keeps its own ramp, so a core going amber says so on its own.
        new(DesktopWidgetKind.Bars,
            "Bars",
            "A row of vertical bars, one per reading. Good for per-core load.",
            260, 140,
            series => new SensorBars(series),
            SeriesPaint.Many(16, (piece, i, brush) => ((SensorBars)piece).PaintBar(i, brush)))
            { HasBar = true, HasDirection = true, Icon = Wpf.Ui.Controls.SymbolRegular.DataBarVertical24, Summary = "Up to 16 readings side by side" },

        // The first kind whose reading is not a sensor. Its one series names a list, which the SOURCE orders
        // and sizes; how many of its rows are shown is the widget's. One ramp for the whole list, and each row
        // judged on its own figure, so the application at the top can go amber while the rest stay calm.
        //
        // Taller since the bars, which a list made today starts with: the design's card at Ember's sizes — five rows
        // with a bar under each, and the total on the title's line. A list from before them keeps the size it was given, and is
        // measured as the column of text it is — the clock's argument about its rings. The width is the same either
        // way, so at its starting size — or any shape at least as tall for its width — a list keeps the size of its
        // text when the bar is turned off or on. One made wider or shorter than that is scaled by its height, and the
        // two figures need different heights.
        new(DesktopWidgetKind.List,
            "List",
            "The applications using the most CPU, memory or I/O, largest first.",
            240, 230,
            series => new SensorList(series),
            SeriesPaint.PerRow((piece, judge) => ((SensorList)piece).PaintRows(judge)))
            {
                CreateList = list => new SensorList(list),
                MadeNow = list => (list.ShowBar, list.ShowSparkline, list.ShowTotal) = (true, true, true),
                Icon = Wpf.Ui.Controls.SymbolRegular.TextBulletListLtr24, Summary = "The busiest applications, largest first",
                Reference = list => list.ShowBar ? (240, 230) : (240, 170),
            },
    ];

    /// <summary>Falls back to the first kind rather than throwing: the kind comes from widgets.json, which
    /// a user can hand-edit and an older build may have written before a kind was renamed.</summary>
    public static WidgetKindInfo For(DesktopWidgetKind kind) =>
        All.FirstOrDefault(k => k.Kind == kind) ?? All[0];

    /// <summary>
    /// The kind a widget made from one sensor starts as — pinned from the Explorer, or added from the edit bar.
    /// A percentage is a share of a whole, which is what a dial draws; every other reading — temperature included,
    /// although it has a fixed scale too — starts as its trend, the same for all of them, which reads honestly
    /// whatever the units are (João, 2026-10-07: consistency across pins). A different shape is one click away.
    /// </summary>
    public static WidgetKindInfo SuggestFor(SensorNode sensor) =>
        For(sensor.Unit == "%" ? DesktopWidgetKind.RadialGauge : DesktopWidgetKind.MiniSparkline);

    /// <summary>
    /// The readings a widget keeps when it becomes <paramref name="to"/>, in their order: those of the sort it draws
    /// — a list, or sensors — and of those as many as it draws. The one statement of the rule, asked by the change
    /// itself and by the gallery beforehand, so the warning that names what would go and what then goes are the same.
    /// It was written inside the change, which cut in silence.
    /// </summary>
    public static IReadOnlyList<T> ReadingsKept<T>(WidgetKindInfo to, IEnumerable<T> readings, Func<T, bool> isList) =>
        [.. readings.Where(reading => isList(reading) == to.HasList).Take(to.MaxSeries)];

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
    /// A percentage or a temperature gets a fixed top (<see cref="SensorScale.Fixed"/>). Anything else
    /// auto-scales, because there is no honest upper bound to assume for watts or bytes per second.
    /// </summary>
    private static SparklineChart BuildChart(IReadOnlyList<SensorNode> series) => new()
    {
        Values = series[0].History,
        SecondValues = series.Count > 1 ? series[1].History : null,
        MaxY = SensorScale.Fixed(series[0]) ?? double.NaN,
    };

}
