using System.Collections.Specialized;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace Aquila.Controls.Primitives;

/// <summary>
/// One or two series as lines over time, with the area under each and a dot at each reading when the look asks for
/// them — what every chart in Aquila is drawn with.
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
/// the box's edge.
///
/// Its points are never animated. The X axis is time, and easing a point into place would draw a reading as arriving
/// between two reads, when nothing was read. What moves is everything else: the scale, which travels to new ends
/// rather than jumping (<see cref="Retarget"/>), and the end's pulse (<see cref="ShowEnd"/>), both at the
/// <see cref="Pace"/> and only while they move.
/// </summary>
public class LinePlot : FrameworkElement
{
    /// <summary>How opaque the second series' area is next to the first's: lighter, so where they overlap the first
    /// still reads as the first.</summary>
    private const double SecondArea = 0.7;

    public static readonly DependencyProperty ValuesProperty = RegisterSeries(nameof(Values));

    /// <summary>The first series, oldest first.</summary>
    public IReadOnlyList<double>? Values
    {
        get => (IReadOnlyList<double>?)GetValue(ValuesProperty);
        set => SetValue(ValuesProperty, value);
    }

    public static readonly DependencyProperty SecondValuesProperty = RegisterSeries(nameof(SecondValues));

    /// <summary>A second series on the same scale, drawn over the first — a download beside its upload.</summary>
    public IReadOnlyList<double>? SecondValues
    {
        get => (IReadOnlyList<double>?)GetValue(SecondValuesProperty);
        set => SetValue(SecondValuesProperty, value);
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

    public static readonly DependencyProperty StrokeProperty = Register<Brush?>(nameof(Stroke), null);

    /// <summary>The first series' colour: its line, its area and its dots. Null draws nothing of it.</summary>
    public Brush? Stroke
    {
        get => (Brush?)GetValue(StrokeProperty);
        set => SetValue(StrokeProperty, value);
    }

    public static readonly DependencyProperty SecondStrokeProperty = Register<Brush?>(nameof(SecondStroke), null);

    /// <summary>The second series' colour. Null draws nothing of it.</summary>
    public Brush? SecondStroke
    {
        get => (Brush?)GetValue(SecondStrokeProperty);
        set => SetValue(SecondStrokeProperty, value);
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

    /// <summary>Values on the scale to draw a fine dashed line across at — a reading's limits. Under the series, and
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

    public static readonly DependencyProperty BandsProperty =
        Register<IReadOnlyList<(double From, Color Color)>?>(nameof(Bands), null);

    /// <summary>
    /// A colour for each stretch of the scale, each from its value up to the next one's — the first from below
    /// anything: the first series' line, area and dots are painted in the colour of the height they are drawn at,
    /// which is the colour of the value. Null paints the series in <see cref="Stroke"/>, one colour throughout.
    ///
    /// One stroke, never a line broken into pieces: a vertical brush with a hard edge at each band, laid over the box
    /// as it is drawn, so a segment crossing a limit changes colour exactly where it crosses. Made again only when the
    /// scale or the bands change.
    /// </summary>
    public IReadOnlyList<(double From, Color Color)>? Bands
    {
        get => (IReadOnlyList<(double From, Color Color)>?)GetValue(BandsProperty);
        set => SetValue(BandsProperty, value);
    }

    private (IReadOnlyList<(double From, Color Color)> Bands, double Min, double Max, double Top, double Bottom) _banded;
    private Brush? _bandBrush;

    private Brush? BandBrush(Rect box, double min, double max)
    {
        if (Bands is not { Count: > 0 } bands || !(max > min) || !(box.Height > 0)) return null;

        var made = (bands, min, max, box.Top, box.Bottom);
        if (_bandBrush is not null && made.Equals(_banded)) return _bandBrush;

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

        _banded = made;
        return _bandBrush = brush;
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

    /// <summary>A series: drawn again when it is replaced, and listened to in its place while the plot is on screen.
    /// A new series is a new scale, put in place at once — it is not the same readings moving.</summary>
    private static DependencyProperty RegisterSeries(string name) =>
        DependencyProperty.Register(name, typeof(IReadOnlyList<double>), typeof(LinePlot),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender,
                (plot, change) =>
                {
                    var line = (LinePlot)plot;
                    line.Replace(change.OldValue, change.NewValue);
                    line.Retarget(travel: false);
                }));

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

    // ── Listening ────────────────────────────────────────────────────────

    /// <summary>Whether the plot is listening to its series — from Loaded to Unloaded, so a plot off screen holds on to
    /// nothing and is held by nothing.</summary>
    private bool _hooked;

    private void Hook()
    {
        if (_hooked) return;
        _hooked = true;
        Listen(Values, true);
        Listen(SecondValues, true);

        // Whatever the readings did while it was off screen, it comes back on their scale, not travelling to it.
        Retarget(travel: false);
        InvalidateVisual();
    }

    private void Unhook()
    {
        if (!_hooked) return;
        _hooked = false;
        Listen(Values, false);
        Listen(SecondValues, false);
    }

    private void Replace(object? before, object? after)
    {
        if (!_hooked) return;
        Listen(before, false);
        Listen(after, true);
    }

    private void Listen(object? series, bool on)
    {
        if (series is not INotifyCollectionChanged told) return;
        if (on) told.CollectionChanged += OnSeriesChanged;
        else told.CollectionChanged -= OnSeriesChanged;
    }

    /// <summary>A reading arrived. The series raise their changes on the UI thread — the poll is a DispatcherTimer — and
    /// a reading that adds one and drops the oldest raises two, which WPF draws as one frame. A new reading on the first
    /// series pulses its end, when the end is shown and anything moves.</summary>
    private void OnSeriesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        InvalidateVisual();
        Retarget(travel: true);

        if (ShowEnd && e.Action == NotifyCollectionChangedAction.Add && ReferenceEquals(sender, Values) && Pace.Moves)
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
        var target = PlotGeometry.Range(Values, SecondValues, Window, RangeMin, RangeMax);
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

    // ── The end ──────────────────────────────────────────────────────────

    public static readonly DependencyProperty ShowEndProperty = Register(nameof(ShowEnd), false);

    /// <summary>
    /// A dot on the newest reading of the first series, in its colour — which is the colour of the reading's state, as
    /// whoever wraps the plot paints it. While anything moves it pulses once as each reading arrives: a ring leaving
    /// the dot and fading, over the <see cref="Pace"/>'s duration, and then nothing until the next.
    ///
    /// A pulse is drawn by the plot alone, at the frame rate of the pace, and only while it lasts.
    /// </summary>
    public bool ShowEnd
    {
        get => (bool)GetValue(ShowEndProperty);
        set => SetValue(ShowEndProperty, value);
    }

    /// <summary>How far through its pulse the end is, 0 to 1; 1, its resting value, draws no pulse.</summary>
    private static readonly DependencyProperty PulseProperty = Register("Pulse", 1d);

    /// <summary>How large the end's dot is against the line: four strokes across, and never under six pixels.
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
    private Point[] _first = [];
    private Point[] _second = [];

    protected override void OnRender(DrawingContext drawing)
    {
        var (width, height) = (RenderSize.Width, RenderSize.Height);
        if (!(width > 0) || !(height > 0)) return;

        var (values, second) = (Values, SecondValues);
        if (values is null && second is null) return;

        var look = Look;
        var window = Window;
        var (min, max) = _scaled ? Shown() : PlotGeometry.Range(values, second, window, RangeMin, RangeMax);

        // Inset by half the stroke or half a dot, whichever is more, so the line's round ends and the dots at the edges
        // are drawn inside the box rather than cut by it — and, on the three sides the newest reading can touch, by the
        // end's dot and the reach of its pulse, when shown.
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

        var placed = Series(drawing, values, Stroke, BandBrush(box, min, max), look, look.AreaOpacity, window, box, min,
            max, height, ref _first);
        Series(drawing, second, SecondStroke, null, look, look.AreaOpacity * SecondArea, window, box, min, max, height,
            ref _second);

        if (showEnd && Stroke is { } stroke) DrawEnd(drawing, new ReadOnlySpan<Point>(_first, 0, placed), stroke, look);
    }

    /// <summary>One series: its area to the bottom of the plot, its line over that, its dots over both — in its colour,
    /// or in the colour of each height when it is <paramref name="banded"/>. How many of its points were placed, in
    /// <paramref name="buffer"/>.</summary>
    private int Series(DrawingContext drawing, IReadOnlyList<double>? values, Brush? stroke, Brush? banded,
        LineLook look, double areaOpacity, int window, Rect box, double min, double max, double floor,
        ref Point[] buffer)
    {
        if (values is null || stroke is null) return 0;

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
