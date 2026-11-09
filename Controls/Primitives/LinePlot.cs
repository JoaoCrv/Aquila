using System.Collections.Specialized;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace Aquila.Controls.Primitives;

/// <summary>
/// Up to four lines over time on one scale (<see cref="Lines"/>), with the area under each and a dot at each reading
/// when the look asks for them — what every chart in Aquila is drawn with.
///
/// Drawn by WPF itself, in OnRender, as <see cref="RingArc"/> and <see cref="BarFill"/> are: a line of sixty readings
/// has no axes, no legend and no hit-testing, and a chart library carries all of that plus a render loop of its own.
/// Like them it knows WPF and nothing of Aquila: whoever wraps it decides the colours, the look and the scale.
///
/// It reads the series where they are and copies nothing. A series that tells of its changes is listened to while the
/// plot is on screen, and drawn again when it changes — once a read, which is the only moment there is anything new,
/// and nothing between. One that does not is drawn again by <see cref="Refresh"/>.
///
/// It always draws the LAST <see cref="Window"/> readings (<see cref="PlotGeometry.Visible"/>), from the left, at the
/// whole window's spacing, so a series still shorter than its window grows across the box. A reading that is not a
/// number breaks the line there. The line keeps half its stroke inside the box, and anything past the scale is cut at
/// the box's edge. The lines are drawn in order, so a later one lies over an earlier one where they cross.
///
/// Its points are never animated. The X axis is time, and easing a point into place would draw a reading as arriving
/// between two reads, when nothing was read. What moves is everything else: the scale, which travels to new ends
/// rather than jumping (<see cref="Retarget"/>), and the ends' pulse (<see cref="ShowEnd"/>), both at the
/// <see cref="Pace"/> and only while they move.
/// </summary>
public class LinePlot : FrameworkElement
{
    /// <summary>How opaque each line's area is next to the one before: lighter, so where they overlap the first still
    /// reads as the first — the second at 0.7 of the look's opacity, the third at 0.49, the fourth at 0.34.</summary>
    private const double NextArea = 0.7;

    public static readonly DependencyProperty LinesProperty =
        DependencyProperty.Register(nameof(Lines), typeof(IReadOnlyList<PlotLine>), typeof(LinePlot),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender,
                (plot, change) => ((LinePlot)plot).Replace(
                    change.OldValue as IReadOnlyList<PlotLine>, change.NewValue as IReadOnlyList<PlotLine>)));

    /// <summary>
    /// The lines, first to last, on one scale. A new list of the same readings — a colour changed, a line repainted on
    /// every reading — is the same lines in new clothes; a list whose readings differ is new lines, and a new scale.
    /// </summary>
    public IReadOnlyList<PlotLine>? Lines
    {
        get => (IReadOnlyList<PlotLine>?)GetValue(LinesProperty);
        set => SetValue(LinesProperty, value);
    }

    public static readonly DependencyProperty WindowProperty = RegisterScale(nameof(Window), 60, travels: false);

    /// <summary>How many readings fit across the box. The last of them are drawn, however long the series is.</summary>
    public int Window
    {
        get => (int)GetValue(WindowProperty);
        set => SetValue(WindowProperty, value);
    }

    public static readonly DependencyProperty RangeMinProperty = RegisterScale(nameof(RangeMin), double.NaN, travels: true);

    /// <summary>The bottom of the scale; NaN fits it to the readings shown (<see cref="PlotGeometry.Range"/>).</summary>
    public double RangeMin
    {
        get => (double)GetValue(RangeMinProperty);
        set => SetValue(RangeMinProperty, value);
    }

    public static readonly DependencyProperty RangeMaxProperty = RegisterScale(nameof(RangeMax), double.NaN, travels: true);

    /// <summary>The top of the scale; NaN fits it to the readings shown.</summary>
    public double RangeMax
    {
        get => (double)GetValue(RangeMaxProperty);
        set => SetValue(RangeMaxProperty, value);
    }

    public static readonly DependencyProperty LookProperty = Register(nameof(Look), LineLook.Default);

    /// <summary>The line's thickness, its area, its smoothness and its dots, as one value — changing preset is one
    /// assignment.</summary>
    public LineLook Look
    {
        get => (LineLook)GetValue(LookProperty);
        set => SetValue(LookProperty, value);
    }

    public static readonly DependencyProperty GuidesProperty = Register<IReadOnlyList<double>?>(nameof(Guides), null);

    /// <summary>Values on the scale to draw a fine dashed line across at — a reading's limits. Under the lines, and
    /// only where the scale reaches them: a guide never stretches the scale to be seen.</summary>
    public IReadOnlyList<double>? Guides
    {
        get => (IReadOnlyList<double>?)GetValue(GuidesProperty);
        set => SetValue(GuidesProperty, value);
    }

    public static readonly DependencyProperty GuideBrushProperty = Register<Brush?>(nameof(GuideBrush), null);

    /// <summary>The guides' colour. Null draws none.</summary>
    public Brush? GuideBrush
    {
        get => (Brush?)GetValue(GuideBrushProperty);
        set => SetValue(GuideBrushProperty, value);
    }

    /// <summary>The guides' dashes: three on, three off, in strokes of one pixel — fine enough never to be read as a
    /// series.</summary>
    private static readonly DashStyle GuideDashes = Frozen(new DashStyle([3, 3], 0));

    private static DashStyle Frozen(DashStyle dashes)
    {
        dashes.Freeze();
        return dashes;
    }

    private static DependencyProperty Register<T>(string name, T fallback) =>
        DependencyProperty.Register(name, typeof(T), typeof(LinePlot),
            new FrameworkPropertyMetadata(fallback, FrameworkPropertyMetadataOptions.AffectsRender));

    /// <summary>A property the scale is worked out from. A change of the scale's own ends travels, as the readings
    /// do; a change of the window jumps, since it is a different stretch of time and not the same one rescaled.
    /// </summary>
    private static DependencyProperty RegisterScale<T>(string name, T fallback, bool travels) =>
        DependencyProperty.Register(name, typeof(T), typeof(LinePlot),
            new FrameworkPropertyMetadata(fallback, FrameworkPropertyMetadataOptions.AffectsRender,
                (plot, _) => ((LinePlot)plot).Retarget(travels)));

    public LinePlot()
    {
        ClipToBounds = true;
        Loaded += (_, _) => Hook();
        Unloaded += (_, _) => Unhook();
    }

    /// <summary>Draws again from the series as they are now — for a series that changed in place without saying so.
    /// </summary>
    public void Refresh() => InvalidateVisual();

    /// <summary>The first line's readings: the line whose readings pace the slide and the pulse.</summary>
    private IReadOnlyList<double>? Lead => Lines is { Count: > 0 } lines ? lines[0].Values : null;

    private IEnumerable<IReadOnlyList<double>?> Readings => Lines?.Select(line => line.Values) ?? [];

    // ── Listening ────────────────────────────────────────────────────────

    /// <summary>Whether the plot is listening to its series — from Loaded to Unloaded, so a plot off screen holds on to
    /// nothing and is held by nothing.</summary>
    private bool _hooked;

    private void Hook()
    {
        if (_hooked) return;
        _hooked = true;
        foreach (var series in Readings.Distinct()) Listen(series, true);

        // Whatever the readings did while it was off screen, it comes back on their scale, not travelling to it.
        Retarget(travel: false);
        InvalidateVisual();
    }

    private void Unhook()
    {
        if (!_hooked) return;
        _hooked = false;
        foreach (var series in Readings.Distinct()) Listen(series, false);

        // A slide off screen would go on asking for frames nobody sees.
        Rest();
        _lastReading = 0;
    }

    /// <summary>The lines replaced: listened to in their place, and — only when their readings differ — a new scale,
    /// put in place at once, since it is not the same readings moving.</summary>
    private void Replace(IReadOnlyList<PlotLine>? before, IReadOnlyList<PlotLine>? after)
    {
        var old = before?.Select(line => line.Values).ToArray() ?? [];
        var now = after?.Select(line => line.Values).ToArray() ?? [];
        var same = old.Length == now.Length && old.Zip(now).All(pair => ReferenceEquals(pair.First, pair.Second));
        if (same) return;

        if (_hooked)
        {
            foreach (var series in old.Distinct()) Listen(series, false);
            foreach (var series in now.Distinct()) Listen(series, true);
        }

        Retarget(travel: false);
    }

    private void Listen(object? series, bool on)
    {
        if (series is not INotifyCollectionChanged told) return;
        if (on) told.CollectionChanged += OnSeriesChanged;
        else told.CollectionChanged -= OnSeriesChanged;
    }

    /// <summary>A reading arrived. The series raise their changes on the UI thread — the poll is a DispatcherTimer — and
    /// a reading that adds one and drops the oldest raises two, which WPF draws as one frame. A new reading on the first
    /// line slides the lines and pulses their ends, when those are asked for and anything moves.</summary>
    private void OnSeriesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        InvalidateVisual();
        Retarget(travel: true);

        if (e.Action != NotifyCollectionChangedAction.Add || !ReferenceEquals(sender, Lead)) return;

        Slide();

        if (ShowEnd && Pace.Moves)
        {
            var pulse = new DoubleAnimation(0, 1, Pace.Duration) { FillBehavior = FillBehavior.Stop };
            Timeline.SetDesiredFrameRate(pulse, Pace.FrameRate);
            BeginAnimation(PulseProperty, pulse);
        }
    }

    // ── The scale ────────────────────────────────────────────────────────

    /// <summary>How far the scale is through its travel, 0 to 1; 1, its resting value, is the scale arrived.</summary>
    private static readonly DependencyProperty TravelProperty = Register("Travel", 1d);

    /// <summary>Where the scale's travel started and where it is going. Two numbers, so the travel is a progress the
    /// plot interpolates rather than a value WPF could animate on its own.</summary>
    private (double Min, double Max) _from, _to;

    /// <summary>Whether the scale has been put anywhere yet — until then it is worked out where it is drawn.</summary>
    private bool _scaled;

    /// <summary>The scale as it is drawn now: arrived, or part way from where it was to where it is going.</summary>
    private (double Min, double Max) Shown()
    {
        var progress = (double)GetValue(TravelProperty);
        if (!(progress < 1)) return _to;

        return (_from.Min + (_to.Min - _from.Min) * progress, _from.Max + (_to.Max - _from.Max) * progress);
    }

    /// <summary>
    /// Works the scale out again from the readings and the given ends, and takes the drawing to it: travelling at
    /// the <see cref="Pace"/> — from wherever the scale is drawn, so a reading arriving mid-way bends the travel
    /// instead of restarting it — or there at once, when told not to or when nothing moves.
    ///
    /// The readings never move; only the ruler they are drawn against. So a spike entering the window raises the top
    /// smoothly and every point already drawn settles down under it, and one leaving lets the scale back down the
    /// same way.
    /// </summary>
    private void Retarget(bool travel)
    {
        var target = PlotGeometry.Range(Readings, Window, RangeMin, RangeMax);
        if (_scaled && target == _to) return;

        var now = _scaled ? Shown() : target;
        (_from, _to) = (now, target);

        if (travel && _scaled && Pace.Moves && now != target)
        {
            BeginAnimation(TravelProperty, Pace.Progress());
        }
        else
        {
            BeginAnimation(TravelProperty, null);
            SetValue(TravelProperty, 1d);
            InvalidateVisual();
        }

        _scaled = true;
    }

    // ── Sliding ──────────────────────────────────────────────────────────

    /// <summary>How far the lines have slid towards the next reading, 0 to 1 of a step.</summary>
    private static readonly DependencyProperty SlideProperty = Register("Slide", 0d);

    /// <summary>When the first line last took a reading, and the time between its last two — which is the poll's,
    /// measured rather than told.</summary>
    private long _lastReading;
    private TimeSpan _interval;

    /// <summary>
    /// With the <see cref="Pace"/>'s slide on, a reading starts the lines moving left one step over the time the last
    /// one took to come, at the pace's frame rate, so the newest reading enters at the right edge and the lines have
    /// moved on by a step when the next arrives — which puts every point back where the step would have. A late
    /// reading leaves the lines held a step short until it comes. Only a full window slides: a series still growing
    /// across the box has nothing to give up at the left.
    ///
    /// The one movement that never stops, which is why it is asked for apart from the motion.
    /// </summary>
    private void Slide()
    {
        var now = Environment.TickCount64;
        if (_lastReading > 0) _interval = TimeSpan.FromMilliseconds(Math.Clamp(now - _lastReading, 100, 10_000));
        _lastReading = now;

        if (Pace.Slide && _interval > TimeSpan.Zero && Lead is { } values && values.Count >= Window)
        {
            var slide = new DoubleAnimation(0, 1, _interval);
            Timeline.SetDesiredFrameRate(slide, Pace.FrameRate);
            BeginAnimation(SlideProperty, slide);
        }
        else
        {
            Rest();
        }
    }

    private void Rest()
    {
        BeginAnimation(SlideProperty, null);
        SetValue(SlideProperty, 0d);
    }

    // ── The ends ─────────────────────────────────────────────────────────

    public static readonly DependencyProperty ShowEndProperty = Register(nameof(ShowEnd), false);

    /// <summary>
    /// A dot on the newest reading of each line, in its colour — which is the colour of the reading's state, as
    /// whoever wraps the plot paints it. While anything moves they pulse together once as each reading arrives: a ring
    /// leaving each dot and fading, over the <see cref="Pace"/>'s duration, and then nothing until the next.
    ///
    /// A pulse is drawn by the plot alone, at the frame rate of the pace, and only while it lasts.
    /// </summary>
    public bool ShowEnd
    {
        get => (bool)GetValue(ShowEndProperty);
        set => SetValue(ShowEndProperty, value);
    }

    /// <summary>How far through its pulse the ends are, 0 to 1; 1, its resting value, draws no pulse.</summary>
    private static readonly DependencyProperty PulseProperty = Register("Pulse", 1d);

    /// <summary>How large an end's dot is against the line: four strokes across, and never under six pixels.
    /// </summary>
    private static double EndRadius(LineLook look) => Math.Max(3, look.Thickness * 2);

    /// <summary>How far a pulse's ring grows past the dot, in dots: one more, so to twice its radius.</summary>
    private const double PulseGrowth = 1;

    private void DrawEnd(DrawingContext drawing, ReadOnlySpan<Point> points, Brush stroke, LineLook look)
    {
        var at = points.Length - 1;
        while (at >= 0 && !PlotGeometry.IsPlaced(points[at])) at--;
        if (at < 0) return;

        var end = points[at];
        var radius = EndRadius(look);
        drawing.DrawEllipse(stroke, null, end, radius, radius);

        var pulse = (double)GetValue(PulseProperty);
        if (!(pulse < 1)) return;

        var ring = radius * (1 + PulseGrowth * pulse);
        drawing.PushOpacity(1 - pulse);
        drawing.DrawEllipse(null, _inks.Pen(stroke, Math.Max(1, look.Thickness)), end, ring, ring);
        drawing.Pop();
    }

    // ── Drawing ──────────────────────────────────────────────────────────

    private readonly Inks _inks = new();

    /// <summary>Each line's placed points, kept from frame to frame so drawing allocates nothing at rest.</summary>
    private readonly List<Point[]> _points = [];

    /// <summary>Each line's band brush and what it was made for — made again only when the bands or the scale change.
    /// </summary>
    private readonly List<((IReadOnlyList<(double From, Color Color)> Bands, double Min, double Max, double Top,
        double Bottom) Made, Brush Brush)?> _bands = [];

    protected override void OnRender(DrawingContext drawing)
    {
        var (width, height) = (RenderSize.Width, RenderSize.Height);
        if (!(width > 0) || !(height > 0)) return;

        if (Lines is not { Count: > 0 } lines) return;

        var look = Look;
        var window = Window;
        var (min, max) = _scaled ? Shown() : PlotGeometry.Range(Readings, window, RangeMin, RangeMax);

        // Inset by half the stroke or half a dot, whichever is more, so the line's round ends and the dots at the edges
        // are drawn inside the box rather than cut by it — and, on the three sides the newest reading can touch, by the
        // ends' dots and the reach of their pulse, when shown.
        var showEnd = ShowEnd;
        var inset = Math.Max(0, Math.Max(look.Thickness, look.PointSize)) / 2;
        var reach = showEnd
            ? Math.Max(inset, EndRadius(look) * (1 + PulseGrowth) + Math.Max(1, look.Thickness) / 2)
            : inset;
        var box = new Rect(inset, reach, Math.Max(0, width - inset - reach), Math.Max(0, height - 2 * reach));

        if (Guides is { Count: > 0 } guides && GuideBrush is { } guideBrush)
        {
            var ys = new double[guides.Count];
            for (var i = 0; i < ys.Length; i++) ys[i] = PlotGeometry.YAt(guides[i], box, min, max);
            Draw.Guides(drawing, new Rect(0, 0, width, height), ys,
                _inks.Pen(guideBrush, 1, PenLineCap.Flat, PenLineJoin.Miter, GuideDashes));
        }

        // The lines slide left by their share of a step; the scale, the guides and the bands stay where they are.
        var slid = box;
        var slide = (double)GetValue(SlideProperty);
        if (slide > 0) slid.X -= slide * box.Width / (Math.Max(PlotGeometry.LeastWindow, window) - 1);

        while (_points.Count < lines.Count) _points.Add([]);
        while (_bands.Count < lines.Count) _bands.Add(null);

        var placed = new int[lines.Count];
        var areaOpacity = look.AreaOpacity;
        for (var i = 0; i < lines.Count; i++)
        {
            var buffer = _points[i];
            placed[i] = Series(drawing, lines[i], BandBrush(i, lines[i].Bands, box, min, max), look, areaOpacity,
                window, slid, min, max, height, ref buffer);
            _points[i] = buffer;
            areaOpacity *= NextArea;
        }

        if (!showEnd) return;

        for (var i = 0; i < lines.Count; i++)
            if (lines[i].Stroke is { } stroke)
                DrawEnd(drawing, new ReadOnlySpan<Point>(_points[i], 0, placed[i]), stroke, look);
    }

    /// <summary>
    /// The brush that paints line <paramref name="index"/> in the colour of each height: a vertical brush with a hard
    /// edge at each band, laid over the box, so a segment crossing a limit changes colour exactly where it crosses.
    /// Null for a line with no bands.
    /// </summary>
    private Brush? BandBrush(int index, IReadOnlyList<(double From, Color Color)>? bands, Rect box, double min,
        double max)
    {
        if (bands is not { Count: > 0 } || !(max > min) || !(box.Height > 0)) return null;

        var made = (bands, min, max, box.Top, box.Bottom);
        if (_bands[index] is { } kept && kept.Made.Equals(made)) return kept.Brush;

        double Offset(double value) => Math.Clamp((value - min) / (max - min), 0, 1);

        var colour = bands[0].Color;
        var stops = new GradientStopCollection { new(colour, 0) };
        for (var i = 1; i < bands.Count; i++)
        {
            var at = Offset(bands[i].From);
            stops.Add(new GradientStop(colour, at));
            colour = bands[i].Color;
            stops.Add(new GradientStop(colour, at));
        }
        stops.Add(new GradientStop(colour, 1));

        var brush = new LinearGradientBrush(stops, new Point(0, box.Bottom), new Point(0, box.Top))
        {
            MappingMode = BrushMappingMode.Absolute,
        };
        brush.Freeze();

        _bands[index] = (made, brush);
        return brush;
    }

    /// <summary>One line: its area to the bottom of the plot, its line over that, its dots over both — in its colour,
    /// or in the colour of each height when it is <paramref name="banded"/>. How many of its points were placed, in
    /// <paramref name="buffer"/>.</summary>
    private int Series(DrawingContext drawing, PlotLine line, Brush? banded, LineLook look, double areaOpacity,
        int window, Rect box, double min, double max, double floor, ref Point[] buffer)
    {
        if (line.Stroke is not { } stroke) return 0;

        var values = line.Values;
        var (_, shown) = PlotGeometry.Visible(values.Count, window);
        if (buffer.Length < shown) buffer = new Point[shown];

        var placed = PlotGeometry.Place(values, window, box, min, max, buffer);
        var points = new ReadOnlySpan<Point>(buffer, 0, placed);

        if (banded is null)
        {
            if (_inks.Area(stroke, areaOpacity, look.Area) is { } fill)
                Draw.Area(drawing, points, look.Smoothness, floor, fill);
        }
        else if (_inks.AreaMask(areaOpacity, look.Area) is { } mask)
        {
            // The bands' colours under the line, with the area's opacity — even or fading — laid over them as a mask.
            drawing.PushOpacityMask(mask);
            Draw.Area(drawing, points, look.Smoothness, floor, banded);
            drawing.Pop();
        }

        var ink = banded ?? stroke;
        if (look.Thickness > 0)
            Draw.Line(drawing, points, look.Smoothness,
                _inks.Pen(ink, look.Thickness, PenLineCap.Round, PenLineJoin.Round));

        Draw.Dots(drawing, points, look.PointSize, ink);
        return placed;
    }
}
