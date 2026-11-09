using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace Aquila.Controls.Primitives;

/// <summary>
/// A bar: its track, how much of it is filled — in one colour or in bands — marks across it at fixed shares of its
/// length, and the highest recent reading held over it. Drawn whole or, as the look says, in blocks.
///
/// Drawn by WPF itself, in OnRender, as <see cref="RingArc"/> is, and for a reason of its own as well: the fill is a
/// number this draws, not a size it is laid out at. A bar that animates the Width of a Border lays the whole widget out
/// again on every frame of the sweep; this one only draws again. Like the ring it knows WPF and nothing of Aquila:
/// whatever wraps it decides the colours and the look, and the <see cref="Pace"/> its host sets how it moves.
///
/// Lying down it fills from the left and is as tall as its look and its marks' reach; standing up it fills from the
/// bottom and is as wide. Along its length it takes what it is given.
/// </summary>
public class BarFill : FrameworkElement
{
    public static readonly DependencyProperty FractionProperty = Register(nameof(Fraction), 0d);

    /// <summary>How much of the track is filled, 0 to 1 — what <see cref="MoveTo"/> moves. Held to that range where it is
    /// drawn rather than coerced where it is set, since an animation passes through it.</summary>
    public double Fraction => (double)GetValue(FractionProperty);

    public static readonly DependencyProperty FillProperty = Register<Brush?>(nameof(Fill), null);

    /// <summary>The filled part: the colour of the reading. Null draws none.</summary>
    public Brush? Fill
    {
        get => (Brush?)GetValue(FillProperty);
        set => SetValue(FillProperty, value);
    }

    public static readonly DependencyProperty LookProperty =
        Register(nameof(Look), BarLook.Default, measures: true);

    /// <summary>The thickness, the corners and the track, as one value — changing preset is one assignment.</summary>
    public BarLook Look
    {
        get => (BarLook)GetValue(LookProperty);
        set => SetValue(LookProperty, value);
    }

    public static readonly DependencyProperty OrientationProperty =
        Register(nameof(Orientation), Orientation.Horizontal, measures: true);

    /// <summary>Lying down, filling from the left — the default — or standing up, filling from the bottom.</summary>
    public Orientation Orientation
    {
        get => (Orientation)GetValue(OrientationProperty);
        set => SetValue(OrientationProperty, value);
    }

    public static readonly DependencyProperty MarksProperty = Register<IReadOnlyList<double>?>(nameof(Marks), null);

    /// <summary>Where the marks cross the bar, each a share of its length from where it fills.</summary>
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

    /// <summary>How far a mark reaches past the track on either side.</summary>
    public double MarkReach
    {
        get => (double)GetValue(MarkReachProperty);
        set => SetValue(MarkReachProperty, value);
    }

    public static readonly DependencyProperty BandsProperty =
        Register<IReadOnlyList<(double From, Color Color)>?>(nameof(Bands), null);

    /// <summary>
    /// The filled part in the colour of each stretch a reading has passed, a VU meter's: each band a share of the length
    /// from where the bar fills, in ascending order, and its colour from there on. Null, or empty, fills with
    /// <see cref="Fill"/>.
    /// </summary>
    public IReadOnlyList<(double From, Color Color)>? Bands
    {
        get => (IReadOnlyList<(double From, Color Color)>?)GetValue(BandsProperty);
        set => SetValue(BandsProperty, value);
    }

    public static readonly DependencyProperty PeakProperty = Register(nameof(Peak), double.NaN);

    /// <summary>The highest recent reading, a share of the length; NaN draws none. Set, never travelled to: it holds a
    /// reading that was read, and moves only when another is.</summary>
    public double Peak
    {
        get => (double)GetValue(PeakProperty);
        set => SetValue(PeakProperty, value);
    }

    public static readonly DependencyProperty PeakBrushProperty = Register<Brush?>(nameof(PeakBrush), null);

    /// <summary>The peak's colour. Null draws none.</summary>
    public Brush? PeakBrush
    {
        get => (Brush?)GetValue(PeakBrushProperty);
        set => SetValue(PeakBrushProperty, value);
    }

    private static DependencyProperty Register<T>(string name, T fallback, bool measures = false) =>
        DependencyProperty.Register(name, typeof(T), typeof(BarFill),
            new FrameworkPropertyMetadata(fallback, measures
                ? FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender
                : FrameworkPropertyMetadataOptions.AffectsRender));

    /// <summary>No length of its own — it takes the length it is given — and across, the track and the marks' reach; or
    /// nothing across either, for a look whose thickness is NaN: a bar as thick as it is given.</summary>
    protected override Size MeasureOverride(Size available)
    {
        var thickness = Look.Thickness;
        var across = double.IsNaN(thickness) ? 0 : Math.Max(0, thickness) + 2 * Math.Max(0, MarkReach);
        return Orientation == Orientation.Horizontal ? new Size(0, across) : new Size(across, 0);
    }

    private readonly Inks _inks = new();

    protected override void OnRender(DrawingContext drawing)
    {
        var look = Look;
        var orientation = Orientation;
        var track = BarGeometry.Track(RenderSize, look.Thickness, orientation);

        Draw.Bar(drawing, track, Fraction, Fill, look, orientation, Painted(Bands));

        if (Marks is { Count: > 0 } marks && MarkBrush is { } markBrush)
            Draw.Marks(drawing, track, marks, _inks.Pen(markBrush, 1), MarkReach, orientation);

        if (PeakBrush is { } peakBrush)
            Draw.Peak(drawing, track, Peak, Fraction, peakBrush, look, MarkReach, orientation);
    }

    /// <summary>The bands as fills, each colour's brush kept between frames.</summary>
    private (double From, Brush Fill)[]? Painted(IReadOnlyList<(double From, Color Color)>? bands)
    {
        if (bands is not { Count: > 0 }) return null;

        var painted = new (double From, Brush Fill)[bands.Count];
        for (var i = 0; i < painted.Length; i++) painted[i] = (bands[i].From, _inks.Solid(bands[i].Color));
        return painted;
    }

    // ── Motion ───────────────────────────────────────────────────────────

    /// <summary>Whether the bar has been put anywhere yet. Its first place is jumped to, never travelled to from
    /// nothing: a bar appearing on screen is not a reading rising.</summary>
    private bool _placed;

    /// <summary>
    /// Takes the bar to <paramref name="fraction"/> — never shown shorter than <see cref="BarGeometry.Least"/> of its
    /// track — at the <see cref="Pace"/> in force: its duration, its curve, its frame rate.
    ///
    /// It jumps instead the first time, when <paramref name="animate"/> says not to — a sensor just handed over, a bar
    /// just turned — and when nothing moves. The jump lets go of any animation first: a value held by one ignores what
    /// it is set to, so a bar moved once and then told to jump would freeze where it last went. A move has no From and
    /// is handed off by snapshot, so it starts where the bar is DRAWN: a reading arriving mid-way redirects instead of
    /// jumping back to begin again.
    /// </summary>
    public void MoveTo(double fraction, bool animate = true)
    {
        var target = BarGeometry.Shown(fraction);

        if (!_placed || !animate || !Pace.Moves)
        {
            _placed = true;
            BeginAnimation(FractionProperty, null);
            SetValue(FractionProperty, target);
            return;
        }

        BeginAnimation(FractionProperty, Pace.To(target), HandoffBehavior.SnapshotAndReplace);
    }
}
