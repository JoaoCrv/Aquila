using System.Windows;
using System.Windows.Media;

namespace Aquila.Controls.Primitives;

/// <summary>
/// The last few points of a series as one line, from nought to the highest of them — a trend, not a magnitude.
///
/// Drawn by WPF itself, in OnRender, for the reason <see cref="RingArc"/> gives: a line of twenty points has no axes,
/// no legend and no hit-testing, and a chart carries all of that plus a render loop of its own to draw it. Like the
/// ring it knows WPF and nothing of Aquila, and whatever wraps it decides the colour and when to draw again.
///
/// It reads the points where they are and copies none. The series is told to it once and changes in place, so the
/// owner calls <see cref="Refresh"/> when it has new points — once a read, which is the only moment there are any.
///
/// The spacing is the same however many points there are, from the left edge, so a short series is a short line
/// that grows across the box until it fills it — never one stretched across the box to look like more history than
/// there is.
///
/// Never animated. Its axis is time, and easing a point into place would draw a reading as arriving between two
/// reads, when nothing was read.
/// </summary>
public class TrendLine : FrameworkElement
{
    public static readonly DependencyProperty ValuesProperty =
        Register<IReadOnlyList<double>?>(nameof(Values), null);

    /// <summary>The series, oldest first. Only its last <see cref="Count"/> points are drawn.</summary>
    public IReadOnlyList<double>? Values
    {
        get => (IReadOnlyList<double>?)GetValue(ValuesProperty);
        set => SetValue(ValuesProperty, value);
    }

    public static readonly DependencyProperty CountProperty = Register(nameof(Count), 20);

    /// <summary>How many points the line spans when the series has them all.</summary>
    public int Count
    {
        get => (int)GetValue(CountProperty);
        set => SetValue(CountProperty, value);
    }

    public static readonly DependencyProperty StrokeProperty = Register<Brush?>(nameof(Stroke), null);

    /// <summary>The line's colour. Null draws nothing.</summary>
    public Brush? Stroke
    {
        get => (Brush?)GetValue(StrokeProperty);
        set => SetValue(StrokeProperty, value);
    }

    public static readonly DependencyProperty ThicknessProperty = Register(nameof(Thickness), 1.5);

    /// <summary>The stroke, in pixels. Its round ends and corners are kept inside the box.</summary>
    public double Thickness
    {
        get => (double)GetValue(ThicknessProperty);
        set => SetValue(ThicknessProperty, value);
    }

    private static DependencyProperty Register<T>(string name, T fallback) =>
        DependencyProperty.Register(name, typeof(T), typeof(TrendLine),
            new FrameworkPropertyMetadata(fallback, FrameworkPropertyMetadataOptions.AffectsRender));

    /// <summary>Draws again from the points as they are now — for a series that has changed in place.</summary>
    public void Refresh() => InvalidateVisual();

    private (Brush Brush, double Thickness) _ink;
    private Pen? _pen;

    protected override void OnRender(DrawingContext drawing)
    {
        if (Values is not { } values || Stroke is not { } stroke) return;

        var count = Math.Max(2, Count);
        var shown = Math.Min(count, values.Count);
        if (shown < 2) return;

        var thickness = Thickness;
        if (!(thickness > 0)) return;

        var first = values.Count - shown;
        var peak = 0d;
        for (var i = first; i < values.Count; i++)
            if (values[i] > peak) peak = values[i];

        // Inset by half the stroke, so the round ends are drawn inside the box rather than cut by it.
        var inset = thickness / 2;
        var width = RenderSize.Width - thickness;
        var height = RenderSize.Height - thickness;
        if (width <= 0 || height <= 0) return;

        var step = width / (count - 1);

        // A series that has been nought throughout lies along the bottom, which is what it says.
        Point At(int i) => new(inset + step * (i - first),
            inset + height * (1 - (peak > 0 ? Math.Clamp(values[i] / peak, 0, 1) : 0)));

        var geometry = new StreamGeometry();
        using (var context = geometry.Open())
        {
            context.BeginFigure(At(first), isFilled: false, isClosed: false);

            var points = new Point[shown - 1];
            for (var i = 0; i < points.Length; i++) points[i] = At(first + 1 + i);
            context.PolyLineTo(points, isStroked: true, isSmoothJoin: true);
        }

        geometry.Freeze();
        drawing.DrawGeometry(null, PenFor(stroke, thickness), geometry);
    }

    /// <summary>The pen, made again only when the colour or the thickness changes. Frozen when its brush already is,
    /// and never otherwise, for the reason <see cref="RingArc"/> gives: freezing a pen freezes its brush with it.
    /// </summary>
    private Pen PenFor(Brush brush, double thickness)
    {
        if (_pen is not null && ReferenceEquals(_ink.Brush, brush) && _ink.Thickness == thickness) return _pen;

        var pen = new Pen(brush, thickness)
        {
            StartLineCap = PenLineCap.Round,
            EndLineCap = PenLineCap.Round,
            LineJoin = PenLineJoin.Round,
        };
        if (brush.IsFrozen) pen.Freeze();

        _ink = (brush, thickness);
        return _pen = pen;
    }
}
