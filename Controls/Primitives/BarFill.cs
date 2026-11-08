using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace Aquila.Controls.Primitives;

/// <summary>
/// A bar: its track, how much of it is filled, and marks across it at fixed shares of its length.
///
/// Drawn by WPF itself, in OnRender, as <see cref="RingArc"/> is, and for a reason of its own as well: the fill is a
/// number this draws, not a size it is laid out at. A bar that animates the Width of a Border lays the whole widget out
/// again on every frame of the sweep; this one only draws again. Like the ring it knows WPF and nothing of Aquila:
/// whatever wraps it decides the colours, the look and how long a move takes.
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

    private static DependencyProperty Register<T>(string name, T fallback, bool measures = false) =>
        DependencyProperty.Register(name, typeof(T), typeof(BarFill),
            new FrameworkPropertyMetadata(fallback, measures
                ? FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender
                : FrameworkPropertyMetadataOptions.AffectsRender));

    /// <summary>No length of its own — it takes the length it is given — and across, the track and the marks' reach.
    /// </summary>
    protected override Size MeasureOverride(Size available)
    {
        var across = Math.Max(0, Look.Thickness) + 2 * Math.Max(0, MarkReach);
        return Orientation == Orientation.Horizontal ? new Size(0, across) : new Size(across, 0);
    }

    private readonly Inks _inks = new();

    protected override void OnRender(DrawingContext drawing)
    {
        var look = Look;
        var orientation = Orientation;
        var track = BarGeometry.Track(RenderSize, look.Thickness, orientation);

        Draw.Bar(drawing, track, Fraction, Fill, look, orientation);

        if (Marks is { Count: > 0 } marks && MarkBrush is { } markBrush)
            Draw.Marks(drawing, track, marks, _inks.Pen(markBrush, 1), MarkReach, orientation);
    }

    // ── Motion ───────────────────────────────────────────────────────────

    /// <summary>Whether the bar has been put anywhere yet. Its first place is jumped to, never travelled to from
    /// nothing: a bar appearing on screen is not a reading rising.</summary>
    private bool _placed;

    /// <summary>
    /// Takes the bar to <paramref name="fraction"/> — never shown shorter than <see cref="BarGeometry.Least"/> of its
    /// track — over <paramref name="duration"/>, on <paramref name="easing"/>, at no more than
    /// <paramref name="frameRate"/> frames a second. How long, on what curve and how often are the caller's to say; the
    /// bar knows nothing of settings.
    ///
    /// It jumps instead the first time, and for a duration of nought or less. The jump lets go of any animation first:
    /// a value held by one ignores what it is set to, so a bar moved once and then told to jump would freeze where it
    /// last went. A move has no From and is handed off by snapshot, so it starts where the bar is DRAWN: a reading
    /// arriving mid-way redirects instead of jumping back to begin again.
    /// </summary>
    public void MoveTo(double fraction, TimeSpan duration, IEasingFunction? easing = null, int? frameRate = null)
    {
        var target = BarGeometry.Shown(fraction);

        if (!_placed || duration <= TimeSpan.Zero)
        {
            _placed = true;
            BeginAnimation(FractionProperty, null);
            SetValue(FractionProperty, target);
            return;
        }

        var move = new DoubleAnimation(target, duration) { EasingFunction = easing };
        if (frameRate is > 0) Timeline.SetDesiredFrameRate(move, frameRate);

        BeginAnimation(FractionProperty, move, HandoffBehavior.SnapshotAndReplace);
    }
}
