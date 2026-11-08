using System.Windows;
using System.Windows.Media;

namespace Aquila.Controls.Primitives;

/// <summary>
/// A bar: its track, how much of it is filled, and marks across it at fixed shares of its length.
///
/// Drawn by WPF itself, in OnRender, as <see cref="TrendLine"/> and <see cref="RingArc"/> are, and for a reason of its
/// own as well: the fill is a number this draws, not a size it is laid out at. A bar that animates the Width of a
/// Border — as the meter's and the list's do — lays the whole widget out again on every frame of the sweep; this one
/// only draws again. Like the ring and the line it knows WPF and nothing of Aquila: whatever wraps it decides the
/// colours, the share and when it moves.
///
/// As tall as the track and the marks' reach above and below it, and as wide as it is given.
/// </summary>
public class BarFill : FrameworkElement
{
    public static readonly DependencyProperty FractionProperty = Register(nameof(Fraction), 0d);

    /// <summary>How much of the track is filled, from 0 to 1. What an animation moves, so it is held to that range
    /// where it is drawn rather than coerced where it is set.</summary>
    public double Fraction
    {
        get => (double)GetValue(FractionProperty);
        set => SetValue(FractionProperty, value);
    }

    public static readonly DependencyProperty FillProperty = Register<Brush?>(nameof(Fill), null);

    /// <summary>The filled part. Null draws none.</summary>
    public Brush? Fill
    {
        get => (Brush?)GetValue(FillProperty);
        set => SetValue(FillProperty, value);
    }

    public static readonly DependencyProperty TrackProperty = Register<Brush?>(nameof(Track), null);

    /// <summary>The whole length, under the fill. Null draws none.</summary>
    public Brush? Track
    {
        get => (Brush?)GetValue(TrackProperty);
        set => SetValue(TrackProperty, value);
    }

    public static readonly DependencyProperty ThicknessProperty = Register(nameof(Thickness), 4d, measures: true);

    /// <summary>How thick the track is, in pixels.</summary>
    public double Thickness
    {
        get => (double)GetValue(ThicknessProperty);
        set => SetValue(ThicknessProperty, value);
    }

    public static readonly DependencyProperty CornerProperty = Register(nameof(Corner), 0d);

    /// <summary>The radius of the ends, held to half the thickness — past that a corner has nothing left to round.
    /// </summary>
    public double Corner
    {
        get => (double)GetValue(CornerProperty);
        set => SetValue(CornerProperty, value);
    }

    public static readonly DependencyProperty MarksProperty = Register<IReadOnlyList<double>?>(nameof(Marks), null);

    /// <summary>Where the marks cross the bar, each a share of its length from 0 to 1.</summary>
    public IReadOnlyList<double>? Marks
    {
        get => (IReadOnlyList<double>?)GetValue(MarksProperty);
        set => SetValue(MarksProperty, value);
    }

    public static readonly DependencyProperty MarkBrushProperty = Register<Brush?>(nameof(MarkBrush), null);

    /// <summary>The marks' colour. Null draws none.</summary>
    public Brush? MarkBrush
    {
        get => (Brush?)GetValue(MarkBrushProperty);
        set => SetValue(MarkBrushProperty, value);
    }

    public static readonly DependencyProperty MarkReachProperty = Register(nameof(MarkReach), 0d, measures: true);

    /// <summary>How far a mark reaches past the track, above and below it.</summary>
    public double MarkReach
    {
        get => (double)GetValue(MarkReachProperty);
        set => SetValue(MarkReachProperty, value);
    }

    private static DependencyProperty Register<T>(string name, T fallback, bool measures = false) =>
        DependencyProperty.Register(name, typeof(T), typeof(BarFill),
            new FrameworkPropertyMetadata(fallback, measures
                ? FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender
                : FrameworkPropertyMetadataOptions.AffectsRender));

    /// <summary>No width of its own — it takes the width it is given — and the height of the track and the marks.
    /// </summary>
    protected override Size MeasureOverride(Size available) =>
        new(0, Math.Max(0, Thickness) + 2 * Math.Max(0, MarkReach));

    private Pen? _pen;

    protected override void OnRender(DrawingContext drawing)
    {
        var width = RenderSize.Width;
        var thickness = Thickness;
        if (!(width > 0) || !(thickness > 0)) return;

        var reach = Math.Max(0, MarkReach);
        var corner = Math.Clamp(Corner, 0, thickness / 2);

        if (Track is { } track)
            drawing.DrawRoundedRectangle(track, null, new Rect(0, reach, width, thickness), corner, corner);

        // A NaN — only an animation could hand one over — is drawn as nothing rather than as a bar of no width.
        var fraction = double.IsNaN(Fraction) ? 0 : Math.Clamp(Fraction, 0, 1);
        if (Fill is { } fill && fraction > 0)
        {
            // A fill shorter than its two corners would round past its own length: the corners shrink with it, so a
            // nearly empty bar ends in a dot, never in a shape wider than what it measures.
            var filled = width * fraction;
            var round = Math.Min(corner, filled / 2);
            drawing.DrawRoundedRectangle(fill, null, new Rect(0, reach, filled, thickness), round, round);
        }

        if (Marks is not { Count: > 0 } marks || MarkBrush is not { } markBrush) return;

        var pen = PenFor(markBrush);
        var height = thickness + 2 * reach;

        foreach (var mark in marks)
        {
            if (!(mark is >= 0 and <= 1)) continue;

            // On the pixel grid: a line one pixel wide between two pixels is drawn two pixels wide at half strength.
            var x = width * mark;
            drawing.PushGuidelineSet(new GuidelineSet([x - 0.5, x + 0.5], []));
            drawing.DrawLine(pen, new Point(x, 0), new Point(x, height));
            drawing.Pop();
        }
    }

    /// <summary>The marks' pen, made again only when their colour changes, and frozen when its brush already is — for
    /// the reason <see cref="RingArc"/> gives: freezing a pen freezes its brush with it.</summary>
    private Pen PenFor(Brush brush)
    {
        if (_pen is not null && ReferenceEquals(_pen.Brush, brush)) return _pen;

        var pen = new Pen(brush, 1);
        if (brush.IsFrozen) pen.Freeze();

        return _pen = pen;
    }
}
