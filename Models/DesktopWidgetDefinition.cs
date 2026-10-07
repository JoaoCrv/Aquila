namespace Aquila.Models;

/// <summary>
/// The widget shapes the desktop can show. Names are persisted in widgets.json by name, not by number,
/// so entries may be added freely but not renamed without breaking existing layouts.
/// </summary>
/// <summary>Which side of the widget the title sits on.</summary>
public enum TitlePlacement { Top, Bottom, Left, Right }

public enum DesktopWidgetKind
{
    RadialGauge,
    MiniSparkline,
    SensorMeter,
    StatBox,
    SparklineChart,

    /// <summary>Reads nothing and exists to sit behind: the card look, and later the page's background.</summary>
    Backdrop,

    /// <summary>Words. A caption on its own, or a caption with a reading beside it.</summary>
    Text,

    /// <summary>The time or the date, on a clock of its own rather than the hardware poll.</summary>
    Clock,

    /// <summary>A row of vertical bars, one per reading — an equaliser. The only kind whose count the
    /// hardware decides rather than the catalogue.</summary>
    Bars,
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
    /// <summary>Floor at zero; the ceiling fixed for a percentage or a temperature (SensorScale.Fixed) and at
    /// whatever the data reaches otherwise.</summary>
    FromZero,

    /// <summary>Both ends follow the readings on screen. The line uses the full height — at the cost of a
    /// scale that moves, so idle noise looks dramatic and two widgets stop being comparable.</summary>
    FitToData,

    /// <summary>Both ends fixed by the user.</summary>
    Manual,
}

/// <summary>Where a piece of text sits in the space it is given.</summary>
public enum TextAlign { Left, Center, Right }

/// <summary>
/// Which way a row of bars runs.
///
/// On the widget and not in the preset, by the preset's own test: swapping Ember for Sky must never turn
/// an equaliser on its side. It changes nothing about what is on screen, but it is a fact about how THIS
/// widget fits the space it was placed in — the same category as how wide it is.
/// </summary>
public enum BarDirection
{
    /// <summary>Standing up, growing from the bottom. The equaliser.</summary>
    Vertical,

    /// <summary>Lying down, growing from the left. A stack of meters, one per reading.</summary>
    Horizontal,
}

/// <summary>
/// What a clock shows. Presets rather than a pattern, because a pattern is a language and this has to work
/// for someone who has never seen one.
///
/// Each maps to a standard .NET specifier, so the result follows the machine's own locale — 24-hour or
/// 12-hour, day-month or month-day, in the user's language — without a single option to get wrong.
/// </summary>
public enum ClockFormat
{
    /// <summary>Short time: 14:07, or 2:07 PM.</summary>
    Time,

    /// <summary>Long time, with seconds.</summary>
    TimeWithSeconds,

    /// <summary>Short date: 28/08/2026, or 8/28/2026.</summary>
    Date,

    /// <summary>Long date, written out.</summary>
    DateLong,

    /// <summary>Both, short.</summary>
    DateAndTime,
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

    /// <summary>
    /// Where the widget lives, prefixed — see <see cref="WidgetSurface"/>. Today always a monitor
    /// (<c>screen:DEL-A1B2-DP1</c>); a page claims its own namespace without needing a second field.
    ///
    /// A monitor is named by a stable identity derived from its EDID manufacturer/product code and
    /// connection, not by index or DeviceName — both shift when monitors are unplugged or rearranged. If
    /// the screen is gone the widget falls back to the primary one rather than disappearing.
    /// </summary>
    public string Surface { get; set; } = string.Empty;

    public double X { get; set; }
    public double Y { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }

    /// <summary>Which widget draws on top where two overlap — higher is nearer the front. Without it the
    /// order is whatever the layout file happens to list, which the user has no way to change.</summary>
    public int ZIndex { get; set; }

    /// <summary>
    /// Which preset dresses this widget, by id. Empty means the default — which is what a widget that has
    /// never been dressed says, and what keeps a fresh layout from naming a preset it does not care about.
    ///
    /// A reference, never a copy. Every appearance value used to live here — around thirty of them per
    /// widget, near-identical across a layout — and changing a look meant editing each one. The reference
    /// is what makes "edit the preset, every widget follows" possible at all.
    /// </summary>
    public string Preset { get; set; } = string.Empty;

    // --- Content the preset must never hold ---
    //
    // Appearance is shared and travels; these do not. A preset that carried the words would rename every
    // widget wearing it, and the decisive test — the same preset dressing a CPU widget and a network one
    // without editing — would fail on the first caption.

    /// <summary>What a Text widget says. Shown alone, or before the reading when one is chosen — the value
    /// and its unit are formatted by the app, so nobody has to learn a format string to write "CPU".</summary>
    /// <summary>
    /// Whether the reading is drawn at all. Only the kinds whose graphic stands on its own can be asked —
    /// a dial or a bar — and the editor offers it nowhere else.
    ///
    /// Here rather than in the preset, where it started. A preset has to be safe to try on: changing from
    /// one to another should change how things are read, never what is on the screen to read. And the
    /// decision is per widget by nature — two dials side by side, one you read and one that is decoration,
    /// needed two presets differing by a boolean, which is the duplication presets exist to prevent.
    /// </summary>
    public bool ShowValue { get; set; } = true;

    /// <summary>Whether the reading carries its unit. Same reasoning as <see cref="ShowValue"/>: the unit
    /// is information, and whether it is worth repeating depends on what the title above already says —
    /// which is a fact about this widget, not about a palette.</summary>
    public bool ShowUnit { get; set; } = true;

    /// <summary>Which way a bars widget runs. Meaningless to every other kind, which is why the catalogue
    /// declares who can be asked.</summary>
    public BarDirection BarDirection { get; set; } = BarDirection.Vertical;

    public string Text { get; set; } = string.Empty;

    /// <summary>What a Clock widget shows. Which of time or date is content; how big it is drawn is not.</summary>
    public ClockFormat ClockFormat { get; set; } = ClockFormat.Time;

    /// <summary>How many readings a chart shows. Readings, not seconds: the time they cover is this times
    /// the poll interval, which the user can change. How much data to show is a question about the data,
    /// not about how it looks.</summary>
    public int PointCount { get; set; } = 60;

    /// <summary>How a chart's vertical scale is decided, and its ends when the user decides them. Also
    /// about the data: the same preset should dress a chart pinned to 30–90 and one fitted to its own
    /// readings without knowing the difference.</summary>
    public ChartScale Scale { get; set; } = ChartScale.FromZero;
    public double ScaleMin { get; set; }
    public double ScaleMax { get; set; } = 100;

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

    /// <summary>
    /// Which ramp in the preset draws this reading. Empty takes the preset's primary.
    ///
    /// A name, not a colour, and no longer a choice between "fixed" and "follows the value" — every reading
    /// follows, and a fixed colour is a ramp whose four stops are the same. One mechanism, no bypass.
    /// </summary>
    public string Ramp { get; set; } = string.Empty;

    /// <summary>What this reading IS — "Cpu.Temperature" — so it can be judged. Stored rather than worked
    /// out at render time: the sensor alone cannot say whether 62 °C is a die or a drive, and the catalog
    /// that CAN say is only consulted while listing sensors.</summary>
    public string Metric { get; set; } = string.Empty;

    public WidgetSeries Clone() => (WidgetSeries)MemberwiseClone();
}
