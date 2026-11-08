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
///
/// Where it sits in its box is <see cref="Fit"/>: on its circle, centred — the clock's rings — or by the arc it
/// sweeps, which a dial needs. <see cref="Centre"/> says where that put it, for whatever is laid over the ring.
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

    public static readonly DependencyProperty LookProperty = RegisterShape(nameof(Look), ArcLook.Default);

    /// <summary>
    /// The stroke the arc and the track share, their ends, and the track's colour, as one value — changing preset is
    /// one assignment.
    ///
    /// The ends reach the arc and the track alike, and the track's only when the sweep stops short of a turn (a whole
    /// turn has no ends). Round reaches half the stroke past each end, so a short arc is a pill and never a stub: a
    /// clock's first second is a dot with a tail, not a sliver. That is meant. The value is still honest where it
    /// matters — at the arc's centre line — and an arc that vanished below some length, which is what a corner fitted
    /// INSIDE the arc forces, said nothing at all for the first seconds of every minute.
    /// </summary>
    public ArcLook Look
    {
        get => (ArcLook)GetValue(LookProperty);
        set => SetValue(LookProperty, value);
    }

    public static readonly DependencyProperty StartAngleProperty = RegisterShape(nameof(StartAngle), -90d);

    /// <summary>Where the sweep begins, in degrees clockwise from three o'clock — so −90, the default, is twelve.
    /// </summary>
    public double StartAngle
    {
        get => (double)GetValue(StartAngleProperty);
        set => SetValue(StartAngleProperty, value);
    }

    public static readonly DependencyProperty SweepAngleProperty = RegisterShape(nameof(SweepAngle), 360d);

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

    public static readonly DependencyProperty FitProperty = RegisterShape(nameof(Fit), RingFit.Circle);

    /// <summary>How the ring takes its box: on its circle, centred — the default, and the clock's — or by the arc it
    /// sweeps, so a dial's open bottom is not room kept for nothing. See <see cref="RingFit"/>.</summary>
    public RingFit Fit
    {
        get => (RingFit)GetValue(FitProperty);
        set => SetValue(FitProperty, value);
    }

    public static readonly DependencyProperty CentreSizeProperty = RegisterShape(nameof(CentreSize), default(Size));

    /// <summary>
    /// The size of whatever is laid over the ring's centre, which a fitted ring keeps inside its box along with the
    /// arc (<see cref="ArcGeometry.Fit"/>). Nothing by default, and nothing to a ring on its circle.
    ///
    /// Inside a dial's arc it never matters. A half dial's centre is the bottom edge of the arc, so without it the
    /// lower half of a number there would hang out of a wide box.
    /// </summary>
    public Size CentreSize
    {
        get => (Size)GetValue(CentreSizeProperty);
        set => SetValue(CentreSizeProperty, value);
    }

    /// <summary>The centre the ring is drawn around at its present size, in its own coordinates — where something
    /// laid over it, a number, belongs. Asked of the same placement the drawing uses.</summary>
    public Point Centre => Placement().Centre;

    private static DependencyProperty Register<T>(string name, T fallback) =>
        DependencyProperty.Register(name, typeof(T), typeof(RingArc),
            new FrameworkPropertyMetadata(fallback, FrameworkPropertyMetadataOptions.AffectsRender));

    /// <summary>A property the ring's place depends on: it moves <see cref="Centre"/>, so the parent is asked to lay
    /// out again, and whatever it put at the centre follows. Never the value or a colour — those change every frame
    /// a ring moves, and the place never does.</summary>
    private static DependencyProperty RegisterShape<T>(string name, T fallback) =>
        DependencyProperty.Register(name, typeof(T), typeof(RingArc),
            new FrameworkPropertyMetadata(fallback,
                FrameworkPropertyMetadataOptions.AffectsRender | FrameworkPropertyMetadataOptions.AffectsParentArrange));

    // ── Drawing ──────────────────────────────────────────────────────────────

    /// <summary>What a shape was made for. Any of it changing is a new shape; anything else — a colour, the ends — is a
    /// new pen over the same one.</summary>
    private readonly record struct Figure(double Value, Point Centre, double Radius, double Start, double Sweep);

    /// <summary>What a placement was worked out for. The fitted one is a small sum, but the ring is drawn every frame
    /// it moves, and its box almost never changes.</summary>
    private readonly record struct Room(Size Size, double Thickness, double Start, double Sweep, PenLineCap Caps,
        RingFit Fit, Size Middle);

    private Figure _arcFigure;
    private Geometry? _arc;
    private Figure _trackFigure;
    private Geometry? _track;

    /// <summary>The pens, made again only when a colour, the thickness or the ends change — never as the arc moves.
    /// </summary>
    private readonly Inks _inks = new();

    private Room? _room;
    private (Point Centre, double Radius) _placed;

    private (Point Centre, double Radius) Placement()
    {
        var look = Look;
        var room = new Room(RenderSize, look.Thickness, StartAngle, SweepAngle, look.Caps, Fit, CentreSize);
        if (room == _room) return _placed;

        _room = room;
        return _placed = ArcGeometry.Place(room.Size, room.Thickness, room.Start, room.Sweep, room.Caps, room.Fit,
            room.Middle);
    }

    protected override void OnRender(DrawingContext drawing)
    {
        var look = Look;
        if (!(look.Thickness > 0)) return;

        var (centre, radius) = Placement();
        if (!(radius > 0)) return;

        if (look.Track is { } track)
            Trace(drawing, _inks.Pen(track, look.Thickness, look.Caps), ref _track, ref _trackFigure,
                new(1, centre, radius, StartAngle, SweepAngle));

        if (Stroke is { } stroke)
            Trace(drawing, _inks.Pen(stroke, look.Thickness, look.Caps), ref _arc, ref _arcFigure,
                new(Value, centre, radius, StartAngle, SweepAngle));
    }

    /// <summary>Draws the arc a figure stands for (<see cref="Draw.ArcShape"/>), kept while the figure is the same:
    /// the track does not change while the arc travels, and the arc does not change while only its colour does.
    /// Nothing at all below <see cref="ArcGeometry.Least"/> of the sweep.</summary>
    private static void Trace(DrawingContext drawing, Pen pen, ref Geometry? kept, ref Figure made, Figure figure)
    {
        if (ArcGeometry.ShapeOf(figure.Value, figure.Sweep) == ArcShape.None) return;

        if (kept is null || made != figure)
        {
            kept = Draw.ArcShape(figure.Centre, figure.Radius, figure.Start,
                ArcGeometry.Share(figure.Value) * ArcGeometry.Sweep(figure.Sweep));
            made = figure;
        }

        if (kept is not null) drawing.DrawGeometry(null, pen, kept);
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
