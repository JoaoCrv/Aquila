using System.Collections.Specialized;
using System.Windows;
using System.Windows.Media;

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
/// Never animated. Its X axis is time, and easing a point into place would draw a reading as arriving between two
/// reads, when nothing was read.
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

    public static readonly DependencyProperty WindowProperty = Register(nameof(Window), 60);

    /// <summary>How many readings fit across the box. The last of them are drawn, however long the series is.</summary>
    public int Window
    {
        get => (int)GetValue(WindowProperty);
        set => SetValue(WindowProperty, value);
    }

    public static readonly DependencyProperty RangeMinProperty = Register(nameof(RangeMin), double.NaN);

    /// <summary>The bottom of the scale; NaN fits it to the readings shown (<see cref="PlotGeometry.Range"/>).</summary>
    public double RangeMin
    {
        get => (double)GetValue(RangeMinProperty);
        set => SetValue(RangeMinProperty, value);
    }

    public static readonly DependencyProperty RangeMaxProperty = Register(nameof(RangeMax), double.NaN);

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
    /// </summary>
    private static DependencyProperty RegisterSeries(string name) =>
        DependencyProperty.Register(name, typeof(IReadOnlyList<double>), typeof(LinePlot),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender,
                (plot, change) => ((LinePlot)plot).Replace(change.OldValue, change.NewValue)));

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
    /// a reading that adds one and drops the oldest raises two, which WPF draws as one frame.</summary>
    private void OnSeriesChanged(object? sender, NotifyCollectionChangedEventArgs e) => InvalidateVisual();

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
        var (min, max) = PlotGeometry.Range(values, second, window, RangeMin, RangeMax);

        // Inset by half the stroke or half a dot, whichever is more, so the line's round ends and the dots at the edges
        // are drawn inside the box rather than cut by it.
        var inset = Math.Max(0, Math.Max(look.Thickness, look.PointSize)) / 2;
        var box = new Rect(inset, inset, Math.Max(0, width - 2 * inset), Math.Max(0, height - 2 * inset));

        if (Guides is { Count: > 0 } guides && GuideBrush is { } guideBrush)
        {
            var ys = new double[guides.Count];
            for (var i = 0; i < ys.Length; i++) ys[i] = PlotGeometry.YAt(guides[i], box, min, max);
            Draw.Guides(drawing, new Rect(0, 0, width, height), ys,
                _inks.Pen(guideBrush, 1, PenLineCap.Flat, PenLineJoin.Miter, GuideDashes));
        }

        Series(drawing, values, Stroke, look, look.AreaOpacity, window, box, min, max, height, ref _first);
        Series(drawing, second, SecondStroke, look, look.AreaOpacity * SecondArea, window, box, min, max, height,
            ref _second);
    }

    /// <summary>One series: its area to the bottom of the plot, its line over that, its dots over both.</summary>
    private void Series(DrawingContext drawing, IReadOnlyList<double>? values, Brush? stroke, LineLook look,
        double areaOpacity, int window, Rect box, double min, double max, double floor, ref Point[] buffer)
    {
        if (values is null || stroke is null) return;

        var (_, shown) = PlotGeometry.Visible(values.Count, window);
        if (buffer.Length < shown) buffer = new Point[shown];

        var points = new ReadOnlySpan<Point>(buffer, 0, PlotGeometry.Place(values, window, box, min, max, buffer));

        if (_inks.Area(stroke, areaOpacity, look.Area) is { } fill)
            Draw.Area(drawing, points, look.Smoothness, floor, fill);

        if (look.Thickness > 0)
            Draw.Line(drawing, points, look.Smoothness,
                _inks.Pen(stroke, look.Thickness, PenLineCap.Round, PenLineJoin.Round));

        Draw.Dots(drawing, points, look.PointSize, stroke);
    }
}
