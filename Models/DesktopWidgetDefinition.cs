namespace Aquila.Models;

/// <summary>
/// The widget shapes the desktop can show. Names are persisted in widgets.json by name, not by number,
/// so entries may be added freely but not renamed without breaking existing layouts.
/// </summary>
public enum DesktopWidgetKind
{
    RadialGauge,
    MiniSparkline,
    SensorMeter,
    StatBox,
    SparklineChart,
}

/// <summary>
/// How the area under a chart line is filled. Persisted by name, like <see cref="DesktopWidgetKind"/>.
///
/// Three, not a checkbox: over a busy wallpaper no fill at all reads best, over a plain one a flat block is
/// punchier, and the gradient sits between them. Which one wins depends on the desktop underneath, which is
/// exactly the sort of thing the app cannot guess.
/// </summary>
public enum ChartFill
{
    None,
    Flat,
    Gradient,
}

/// <summary>
/// How far round the dial goes. A preset rather than two free angles, because the start and the sweep have
/// to agree — a 270° arc starting at the top leaves the gap in the wrong place, and letting the user
/// discover that by dragging two sliders against each other is not flexibility, it is homework.
/// </summary>
public enum GaugeSweep
{
    /// <summary>180°, flat side down. Reads as a speedometer.</summary>
    Half,

    /// <summary>270° with the gap at the bottom. The default, and what most dials look like.</summary>
    Dial,

    /// <summary>A closed 360° ring, starting at the top.</summary>
    Ring,
}

/// <summary>
/// How a chart decides the top and bottom of its vertical scale.
///
/// Three, because two of them answer different questions. FromZero says "how much of the whole", which is
/// what makes two widgets comparable to each other. FitToData says "how is it moving", which is what makes
/// a reading that barely varies readable at all.
/// </summary>
public enum ChartScale
{
    /// <summary>Floor at zero, ceiling at 100 for a percentage and at whatever the data reaches otherwise.</summary>
    FromZero,

    /// <summary>Both ends follow the readings on screen. The line uses the full height — at the cost of a
    /// scale that moves, so idle noise looks dramatic and two widgets stop being comparable.</summary>
    FitToData,

    /// <summary>Both ends fixed by the user.</summary>
    Manual,
}

/// <summary>Where the meter's reading sits relative to its bar.</summary>
public enum MeterLayout
{
    /// <summary>Value to the right of the bar, on the same line.</summary>
    Beside,

    /// <summary>Value above the bar, which leaves the bar the full width.</summary>
    Above,
}

/// <summary>
/// One persisted desktop widget (#24): which piece, which sensor, and where it sits. Stored as data
/// rather than code so the layout survives restarts and, later, so the pin-from-Explorer flow can create
/// widgets without touching the widget-building code.
///
/// The sensor is stored as its <see cref="SensorNode.Identifier"/> — resolved back to a live node at
/// startup via <see cref="SensorCatalog.FindByIdentifier"/>. Positions are DIPs within the target
/// screen's canvas.
/// </summary>
public class DesktopWidgetDefinition
{
    public DesktopWidgetKind Kind { get; set; }
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// What this widget draws, in order. One entry for a dial or a number; a chart may have more.
    ///
    /// A list rather than a first sensor with optional extras beside it. That was tried and the editor it
    /// produced was confusing: a "sensor" and a "second sensor" are the same thing wearing different
    /// names, and nothing on screen explained why one of them could be cleared and the other could not.
    /// A list is also how people describe a chart — these readings, in this order.
    /// </summary>
    public List<WidgetSeries> Series { get; set; } = [];

    /// <summary>Legacy: the single sensor written before <see cref="Series"/> existed. Read once to
    /// migrate an older widgets.json, then left empty.</summary>
    public string SensorIdentifier { get; set; } = string.Empty;

    /// <summary>Legacy, alongside <see cref="SensorIdentifier"/>.</summary>
    public string AccentKey { get; set; } = string.Empty;

    /// <summary>
    /// Folds a pre-list layout into <see cref="Series"/>. Keyed on Series being empty rather than on the
    /// old fields having values, so a widget the user has since emptied is not resurrected on every load.
    /// </summary>
    public void MigrateSeries()
    {
        if (Series.Count > 0 || string.IsNullOrEmpty(SensorIdentifier)) return;

        Series.Add(new WidgetSeries
        {
            SensorIdentifier = SensorIdentifier,
            AccentKey = AccentKey,
        });

        SensorIdentifier = string.Empty;
        AccentKey = string.Empty;
    }

    /// <summary>Which physical monitor the widget lives on — a stable identity derived from the monitor's
    /// EDID manufacturer/product code and connection, not its index or DeviceName (both shift when
    /// monitors are unplugged or rearranged). If the screen is gone, the widget falls back to the primary
    /// one rather than disappearing.</summary>
    public string ScreenKey { get; set; } = string.Empty;

    /// <summary>Legacy: the screen index written before <see cref="ScreenKey"/> existed. Only read to
    /// migrate an older widgets.json, then overwritten with the key on the next save.</summary>
    public int ScreenIndex { get; set; }

    public double X { get; set; }
    public double Y { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }

    /// <summary>Which widget draws on top where two overlap — higher is nearer the front. Without it the
    /// order is whatever the layout file happens to list, which the user has no way to change.</summary>
    public int ZIndex { get; set; }

    // --- Appearance ---
    // Colours are stored as "#RRGGBB" and combined with the separate opacity, rather than as "#AARRGGBB":
    // the user sets colour and transparency independently, and keeping them apart means changing one never
    // silently resets the other. A widget sits on an unknown wallpaper, so a backing panel is the default.

    public string BackgroundColor { get; set; } = "#000000";

    /// <summary>0 = invisible, 1 = solid.</summary>
    public double BackgroundOpacity { get; set; } = 0.6;

    public double CornerRadius { get; set; } = 8;

    // --- Line, for the kinds drawn as one (sparkline, chart) ---
    // Ignored by the others, rather than kept somewhere separate: a widget's appearance is one record, and
    // splitting it by which kind reads which field would mean a second thing to migrate.

    /// <summary>Line width in DIPs. 1.5 is LiveCharts-thin; on a large display it nearly disappears.</summary>
    public double LineThickness { get; set; } = 1.5;

    public ChartFill Fill { get; set; } = ChartFill.Gradient;

    /// <summary>0 draws straight segments between readings, 1 a fully rounded curve.</summary>
    public double LineSmoothness { get; set; } = 0.5;

    /// <summary>How many readings the chart shows. One per poll tick, so this is the trend's length in
    /// seconds — 60 is a minute, 600 is ten.</summary>
    public int PointCount { get; set; } = 60;

    public ChartScale Scale { get; set; } = ChartScale.FromZero;

    /// <summary>The ends of the scale in <see cref="ChartScale.Manual"/>, in the sensor's own unit.</summary>
    public double ScaleMin { get; set; }
    public double ScaleMax { get; set; } = 100;

    /// <summary>Diameter of the dot drawn at each reading. 0 hides them, which is the default — on a
    /// 60-point sparkline they merge into a caterpillar.</summary>
    public double PointSize { get; set; }

    // --- Number, for the stat box ---

    /// <summary>Point size of the number. Its own field, like the meter's: 20 suits a 150x120 tile and
    /// says nothing useful about what a dial or a bar wants.</summary>
    public double StatValueSize { get; set; } = 20;

    public bool ShowUnit { get; set; } = true;

    public double UnitSize { get; set; } = 13;

    /// <summary>Whether the number draws its own rounded fill. Off suits a desktop widget, which already
    /// has a styled panel of its own — two of them read as a box inside a box.</summary>
    public bool ShowPanel { get; set; } = true;

    // --- Bar, for the meter ---

    /// <summary>Bar height in DIPs. 6 is the dashboard's hairline, which is right in a dense card and thin
    /// on a widget with a whole panel to itself.</summary>
    public double BarThickness { get; set; } = 6;

    /// <summary>Corner radius of the bar. Half the thickness or more gives a pill; 0 gives square ends.</summary>
    public double BarCorner { get; set; } = 3;

    public MeterLayout Layout { get; set; } = MeterLayout.Beside;

    /// <summary>Point size of the meter's reading. Its own field rather than sharing the dial's: 24pt in
    /// the middle of a dial is right, and 24pt next to a 6px bar is a caption wearing the bar as a belt.
    /// One number that has to suit both is a number that suits neither.</summary>
    public double BarValueSize { get; set; } = 13;

    // --- Dial, for the radial gauge ---

    /// <summary>Thickness of the arc in DIPs. 14 suits the 170x190 default; a gauge dragged out to twice
    /// that leaves a thin ribbon in a lot of empty space.</summary>
    public double ArcThickness { get; set; } = 14;

    /// <summary>Rounding on the ends of the arc. 0 by default, and not only for looks: a rounded cap is
    /// drawn at a fixed size, so on a very short arc it is wider than the arc and jitters around zero.</summary>
    public double ArcCorner { get; set; }

    public GaugeSweep Sweep { get; set; } = GaugeSweep.Dial;

    /// <summary>Point size of the number in the dial's centre.</summary>
    public double ValueSize { get; set; } = 24;

    /// <summary>Whether that number is drawn at all — an arc on its own is a perfectly good glanceable
    /// widget, and the title above it already says what is being measured.</summary>
    public bool ShowValue { get; set; } = true;

    public string BorderColor { get; set; } = "#FFFFFF";
    public double BorderOpacity { get; set; } = 0.25;

    /// <summary>0 hides the border entirely.</summary>
    public double BorderThickness { get; set; } = 0;

    public DesktopWidgetDefinition Clone()
    {
        var copy = (DesktopWidgetDefinition)MemberwiseClone();
        copy.Series = Series.Select(s => s.Clone()).ToList();
        return copy;
    }
}

/// <summary>One reading inside a widget: which sensor, and the colour role it is drawn in.</summary>
public class WidgetSeries
{
    public string SensorIdentifier { get; set; } = string.Empty;

    /// <summary>A colour-profile role, e.g. "Aquila.Scheme.Series1" — held as a key rather than a colour
    /// so the series follows theme and profile changes.</summary>
    public string AccentKey { get; set; } = string.Empty;

    public WidgetSeries Clone() => (WidgetSeries)MemberwiseClone();
}
