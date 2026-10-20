using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace Aquila.Controls.Primitives;

/// <summary>
/// An arc on a ring, over a track — the clock's rings, and the shape a dial is made of.
///
/// Drawn by WPF itself, in OnRender, rather than by a charting library. A ring is one arc: it has no axes, no
/// legend, no series and no hit-testing worth the name, and a chart carries all of that — plus a render loop of its
/// own, beside WPF's — to draw it. Here the arc is a single geometry built from two points, redrawn when a property
/// it depends on changes, and animated by WPF's own clock, which already knows when the screen is shown.
///
/// The start of a library of chart primitives of our own. It knows WPF and nothing of Aquila — no preset, no
/// sensor, no setting — so it can move to a project of its own, and whatever wraps it decides colours and timing.
///
/// The value is a share of the sweep, 0 to 1. The track runs the whole sweep under it; the arc runs
/// <c>Value × SweepAngle</c> from <see cref="StartAngle"/>, clockwise.
/// </summary>
public class RingArc : FrameworkElement
{
    public static readonly DependencyProperty ValueProperty = Register(nameof(Value), 0d);

    /// <summary>How much of the sweep the arc covers: 0 to 1, and drawn at the nearer end outside that.</summary>
    public double Value
    {
        get => (double)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public static readonly DependencyProperty ThicknessProperty = Register(nameof(Thickness), 10d);

    /// <summary>The stroke, in pixels. The arc and the track share it.</summary>
    public double Thickness
    {
        get => (double)GetValue(ThicknessProperty);
        set => SetValue(ThicknessProperty, value);
    }

    public static readonly DependencyProperty StartAngleProperty = Register(nameof(StartAngle), -90d);

    /// <summary>Where the sweep begins, in degrees clockwise from three o'clock — so −90, the default, is twelve.
    /// </summary>
    public double StartAngle
    {
        get => (double)GetValue(StartAngleProperty);
        set => SetValue(StartAngleProperty, value);
    }

    public static readonly DependencyProperty SweepAngleProperty = Register(nameof(SweepAngle), 360d);

    /// <summary>How far round a full value reaches, in degrees: 360, the default, is a ring; a dial is 270 from −225.
    /// </summary>
    public double SweepAngle
    {
        get => (double)GetValue(SweepAngleProperty);
        set => SetValue(SweepAngleProperty, value);
    }

    public static readonly DependencyProperty StrokeProperty = Register<Brush?>(nameof(Stroke), null);

    /// <summary>The arc's colour. Null draws no arc.</summary>
    public Brush? Stroke
    {
        get => (Brush?)GetValue(StrokeProperty);
        set => SetValue(StrokeProperty, value);
    }

    public static readonly DependencyProperty TrackBrushProperty = Register<Brush?>(nameof(TrackBrush), null);

    /// <summary>The whole sweep, under the arc. Null draws no track.</summary>
    public Brush? TrackBrush
    {
        get => (Brush?)GetValue(TrackBrushProperty);
        set => SetValue(TrackBrushProperty, value);
    }

    public static readonly DependencyProperty CapsProperty = Register(nameof(Caps), PenLineCap.Round);

    /// <summary>
    /// The ends of the arc, and of the track when the sweep stops short of a turn (a whole turn has no ends).
    ///
    /// Round reaches half the stroke past each end, so a short arc is a pill and never a stub: a clock's first
    /// second is a dot with a tail, not a sliver. That is meant. The value is still honest where it matters — at the
    /// arc's centre line — and an arc that vanished below some length, which is what a corner fitted INSIDE the arc
    /// forces, said nothing at all for the first seconds of every minute.
    /// </summary>
    public PenLineCap Caps
    {
        get => (PenLineCap)GetValue(CapsProperty);
        set => SetValue(CapsProperty, value);
    }

    private static DependencyProperty Register<T>(string name, T fallback) =>
        DependencyProperty.Register(name, typeof(T), typeof(RingArc),
            new FrameworkPropertyMetadata(fallback, FrameworkPropertyMetadataOptions.AffectsRender));

    // ── Drawing ──────────────────────────────────────────────────────────────

    /// <summary>What a geometry was built for. Any of it changing is a new geometry; anything else — a colour, the
    /// ends — is a new pen over the same one.</summary>
    private readonly record struct Figure(double Value, double Width, double Height, double Thickness, double Start,
        double Sweep);

    private readonly record struct Ink(Brush Brush, double Thickness, PenLineCap Caps);

    private Figure _arcFigure;
    private StreamGeometry? _arc;
    private Figure _trackFigure;
    private StreamGeometry? _track;

    private Ink _strokeInk;
    private Pen? _strokePen;
    private Ink _trackInk;
    private Pen? _trackPen;

    protected override void OnRender(DrawingContext drawing)
    {
        var thickness = Thickness;
        if (!(thickness > 0)) return;

        var (width, height) = (RenderSize.Width, RenderSize.Height);
        var radius = ArcGeometry.Radius(width, height, thickness);
        if (!(radius > 0)) return;

        var centre = new Point(width / 2, height / 2);
        var caps = Caps;

        if (TrackBrush is { } track)
            Draw(drawing, PenFor(ref _trackPen, ref _trackInk, new(track, thickness, caps)),
                ref _track, ref _trackFigure, new(1, width, height, thickness, StartAngle, SweepAngle), centre, radius);

        if (Stroke is { } stroke)
            Draw(drawing, PenFor(ref _strokePen, ref _strokeInk, new(stroke, thickness, caps)),
                ref _arc, ref _arcFigure, new(Value, width, height, thickness, StartAngle, SweepAngle), centre, radius);
    }

    private static void Draw(DrawingContext drawing, Pen pen, ref StreamGeometry? cached, ref Figure built,
        Figure figure, Point centre, double radius)
    {
        switch (ArcGeometry.ShapeOf(figure.Value, figure.Sweep))
        {
            case ArcShape.Circle:
                drawing.DrawEllipse(null, pen, centre, radius, radius);
                return;

            case ArcShape.Arc:
                if (cached is null || built != figure)
                {
                    cached = Arc(centre, radius, figure.Value, figure.Start, figure.Sweep);
                    built = figure;
                }

                drawing.DrawGeometry(null, pen, cached);
                return;
        }
    }

    private static StreamGeometry Arc(Point centre, double radius, double value, double start, double sweep)
    {
        var (from, to) = ArcGeometry.Ends(centre, radius, value, start, sweep);

        var geometry = new StreamGeometry();
        using (var context = geometry.Open())
        {
            context.BeginFigure(from, isFilled: false, isClosed: false);
            context.ArcTo(to, new Size(radius, radius), 0, ArcGeometry.IsLargeArc(value, sweep),
                SweepDirection.Clockwise, isStroked: true, isSmoothJoin: false);
        }

        geometry.Freeze();
        return geometry;
    }

    /// <summary>
    /// The pen for a brush, a thickness and an end — made again only when one of them changes, since the arc is
    /// redrawn every frame it moves and none of them does.
    ///
    /// Frozen when its brush already is, which is what makes it cheap to draw with. Never frozen otherwise: freezing
    /// a pen freezes its brush with it, and the brush belongs to whoever handed it over.
    /// </summary>
    private static Pen PenFor(ref Pen? cached, ref Ink made, Ink ink)
    {
        if (cached is not null && made == ink) return cached;

        var pen = new Pen(ink.Brush, ink.Thickness) { StartLineCap = ink.Caps, EndLineCap = ink.Caps };
        if (ink.Brush.IsFrozen) pen.Freeze();

        made = ink;
        return cached = pen;
    }

    // ── Motion ───────────────────────────────────────────────────────────────

    /// <summary>Counts the moves asked for, so a move still waiting to carry on knows when a newer one has replaced
    /// it.</summary>
    private int _moves;

    /// <summary>
    /// Moves the arc to <paramref name="target"/> over <paramref name="duration"/> — steadily, from wherever it is
    /// drawn right now.
    ///
    /// Linear, because a ring this drives may be time itself, and an eased one would say a second goes faster at its
    /// start than at its end. Handed off by snapshot, so a move asked for mid-way carries on from where the arc IS,
    /// not from where the last move began or meant to end: a late tick bends the path, it never jumps it.
    ///
    /// <paramref name="forwardOnly"/> is for a clock: a target behind the arc is reached by going on round, through
    /// the full sweep and on from nothing — see <see cref="RingMotion.Plan"/>. A duration of zero or less sets the
    /// value at once. <paramref name="frameRate"/>, when given, is the most frames a second the move may ask to be
    /// drawn at; how many that should be is the caller's decision, not the ring's.
    /// </summary>
    public void MoveTo(double target, TimeSpan duration, bool forwardOnly = false, int? frameRate = null) =>
        Run(RingMotion.Plan(Value, target, duration, forwardOnly), 0, ++_moves, frameRate);

    /// <summary>Holds the arc where it is drawn now, and drops whatever of a move was still to come.</summary>
    public void Stop() => MoveTo(Value, TimeSpan.Zero);

    private void Run(RingMotion.Segment[] plan, int step, int move, int? frameRate)
    {
        for (; step < plan.Length; step++)
        {
            var (from, to, duration) = plan[step];

            // The reset at the top of a turn: this stretch starts somewhere the last did not end.
            if (step > 0 && from != plan[step - 1].To) Set(from);

            // Nothing to travel, or no time to travel it in. Set rather than animated: an animation from a value to
            // itself would still have WPF draw every frame of it.
            if (duration <= TimeSpan.Zero || to == from)
            {
                Set(to);
                continue;
            }

            // No From: the move starts wherever the arc is drawn, which is what the snapshot below hands on.
            var animation = new DoubleAnimation(to, duration);
            if (frameRate is > 0) Timeline.SetDesiredFrameRate(animation, frameRate);

            if (step + 1 < plan.Length)
            {
                var next = step + 1;
                animation.Completed += (_, _) =>
                {
                    if (move == _moves) Run(plan, next, move, frameRate);
                };
            }

            BeginAnimation(ValueProperty, animation, HandoffBehavior.SnapshotAndReplace);
            return;
        }
    }

    /// <summary>Puts the value where it is told, at once. The animation is let go first: a property held by one
    /// ignores what it is set to, and an arc that once moved would freeze where it last stopped.</summary>
    private void Set(double value)
    {
        BeginAnimation(ValueProperty, null);
        Value = value;
    }
}
