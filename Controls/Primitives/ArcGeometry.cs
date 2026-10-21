using System.Windows;
using System.Windows.Media;

namespace Aquila.Controls.Primitives;

/// <summary>What a ring draws for a value: nothing, an arc, or the whole circle.</summary>
public enum ArcShape
{
    None,
    Arc,
    Circle,
}

/// <summary>How a ring takes the room it is given.</summary>
public enum RingFit
{
    /// <summary>The whole circle, centred, whatever the sweep: the ring keeps its place on its circle. The clock's
    /// rings, and the default.</summary>
    Circle,

    /// <summary>
    /// The swept arc, stroke included, as large as the room allows and centred in it.
    ///
    /// A 270° dial is not its circle: the open bottom is a quarter of the circle's height with nothing in it, and a
    /// dial centred on its circle keeps that quarter empty inside its own box. Fitted by what is drawn, the arc
    /// reaches the box's edges on the tighter axis and the circle's centre moves down to wherever that puts it.
    /// </summary>
    Sweep,
}

/// <summary>
/// Where an arc on a ring begins and ends — the arithmetic of <see cref="RingArc"/>, kept apart from the drawing so
/// it can be held to its rules without rendering anything.
///
/// Angles are in degrees, clockwise from three o'clock, because that is how a screen's axes already run: y grows
/// downwards, so the plain cos/sin angle is the one that turns clockwise. Twelve o'clock is −90; a dial that opens
/// at the bottom starts at −225 and sweeps 270.
///
/// A value is a share of the sweep, 0 to 1. Anything outside is drawn at the nearer end, and a NaN as nothing: a
/// value comes from a sensor or a sum, and a ring is the wrong place to discover that either went wrong.
/// </summary>
public static class ArcGeometry
{
    /// <summary>
    /// The least share of the sweep worth drawing. Below it the arc is nothing, not even a dot.
    ///
    /// Round ends reach half the stroke past each end, so an arc of almost nothing would still draw a disc a stroke
    /// wide — something, where the value says nothing. A thousandth of a turn is a third of a degree, under a pixel
    /// on any ring a desktop has room for.
    /// </summary>
    public const double Least = 0.001;

    /// <summary>The value as a share the drawing can use: 0 to 1, and 0 for a NaN.</summary>
    public static double Share(double value) => double.IsNaN(value) ? 0 : Math.Clamp(value, 0, 1);

    /// <summary>The sweep a ring can draw: none below nothing, and never more than a whole turn — a second turn would
    /// be drawn over the first and say nothing new.</summary>
    public static double Sweep(double sweepAngle) => double.IsNaN(sweepAngle) ? 0 : Math.Clamp(sweepAngle, 0, 360);

    /// <summary>
    /// What a value draws.
    ///
    /// A whole turn is a circle of its own, never an arc that happens to close: an arc is drawn from one end to the
    /// other, and on a full turn the two ends are the same point — which WPF, following SVG, draws as nothing at all.
    /// A dial's full sweep is still an arc, because its ends are apart.
    /// </summary>
    public static ArcShape ShapeOf(double value, double sweepAngle)
    {
        var sweep = Sweep(sweepAngle);
        var share = Share(value);

        if (share < Least || sweep <= 0) return ArcShape.None;
        return share >= 1 && sweep >= 360 ? ArcShape.Circle : ArcShape.Arc;
    }

    /// <summary>The angle the arc reaches at a value, in degrees: the start, plus that share of the sweep.</summary>
    public static double AngleAt(double value, double startAngle, double sweepAngle) =>
        startAngle + Share(value) * Sweep(sweepAngle);

    /// <summary>The point on a circle at an angle.</summary>
    public static Point PointAt(Point centre, double radius, double degrees)
    {
        var radians = degrees * Math.PI / 180;
        return new(centre.X + radius * Math.Cos(radians), centre.Y + radius * Math.Sin(radians));
    }

    /// <summary>Where the arc for a value starts and where it ends.</summary>
    public static (Point Start, Point End) Ends(Point centre, double radius, double value, double startAngle,
        double sweepAngle) =>
        (PointAt(centre, radius, startAngle), PointAt(centre, radius, AngleAt(value, startAngle, sweepAngle)));

    /// <summary>Whether the arc goes the long way round — past half a turn. An arc is given only its two ends and a
    /// radius, and two circles of that radius pass through both: this is how it is told which.</summary>
    public static bool IsLargeArc(double value, double sweepAngle) => Share(value) * Sweep(sweepAngle) > 180;

    /// <summary>
    /// The radius the stroke is centred on: half the shorter side, less half the stroke.
    ///
    /// Centred because that is how a pen draws; inset by half of it so the outer edge touches the element's bounds
    /// and not a pixel is clipped. Zero or less means there is no room for the ring at all.
    /// </summary>
    public static double Radius(double width, double height, double thickness) =>
        Math.Min(width, height) / 2 - thickness / 2;

    /// <summary>Where a ring sits in a room of this size: the centre it is drawn around, and the radius its stroke is
    /// centred on. The one answer the drawing and anything placed over it both ask, so the two cannot disagree.
    /// </summary>
    public static (Point Centre, double Radius) Place(Size room, double thickness, double startAngle,
        double sweepAngle, PenLineCap caps, RingFit fit, Size middle = default) =>
        fit == RingFit.Sweep
            ? Fit(new Rect(room), thickness, startAngle, sweepAngle, caps, middle)
            : (new Point(room.Width / 2, room.Height / 2), Radius(room.Width, room.Height, thickness));

    /// <summary>
    /// The largest ring whose swept arc — the whole sweep, stroke and ends included — fits inside
    /// <paramref name="room"/>, and the centre that puts that arc in the middle of it. A radius of zero means there
    /// is no room for one.
    ///
    /// Fitted by the whole sweep because that is the track, and every value draws inside its track: a shorter arc
    /// with the same ends covers less of it, never more.
    ///
    /// <paramref name="middle"/> is a box centred on the ring's centre that has to fit as well — whatever is laid
    /// over the middle. Inside a dial's arc it changes nothing; a half dial's centre is the bottom edge of its arc,
    /// and fitted by the arc alone in a wide room, a number written there would hang half out of it. Both are
    /// placed together, and centred together.
    ///
    /// Solved rather than searched for. Every point that can be an edge of the drawing is a fixed direction times
    /// the radius plus a fixed offset (<see cref="Reaches"/>), so the distance between any two of them along an axis
    /// is a straight line in the radius, and the drawing fits when every such distance fits. Each one is a bound —
    /// above when it grows with the radius, below when it shrinks — and the answer is the highest radius every bound
    /// allows: exact, with no tolerance and no iteration, for any start, sweep, thickness and end.
    /// </summary>
    public static (Point Centre, double Radius) Fit(Rect room, double thickness, double startAngle, double sweepAngle,
        PenLineCap caps, Size middle = default)
    {
        var halfway = new Point(room.X + room.Width / 2, room.Y + room.Height / 2);
        var sweep = Sweep(sweepAngle);

        // Written as "not above" so a NaN from anywhere lands here too.
        if (!(sweep > 0) || !(thickness >= 0) || !(room.Width > 0) || !(room.Height > 0) || !double.IsFinite(startAngle))
            return (halfway, 0);

        var reaches = Reaches(thickness, startAngle, sweep, caps);

        // The box in the middle goes nowhere as the ring grows: its corners are offsets from the centre, and nothing
        // along.
        if (!middle.IsEmpty && (middle.Width > 0 || middle.Height > 0))
            foreach (var x in new[] { -middle.Width / 2, middle.Width / 2 })
                foreach (var y in new[] { -middle.Height / 2, middle.Height / 2 })
                    reaches.Add(new(new Vector(0, 0), new Vector(x, y)));

        double most = double.PositiveInfinity, least = 0;
        foreach (var (along, length) in new[] { (true, room.Width), (false, room.Height) })
            foreach (var a in reaches)
                foreach (var b in reaches)
                {
                    // How far a lies past b on this axis: slope × radius + gap.
                    var slope = along ? a.Along.X - b.Along.X : a.Along.Y - b.Along.Y;
                    var gap = along ? a.Offset.X - b.Offset.X : a.Offset.Y - b.Offset.Y;

                    if (slope > 0) most = Math.Min(most, (length - gap) / slope);
                    else if (slope < 0) least = Math.Max(least, (length - gap) / slope);
                    else if (gap > length) return (halfway, 0);
                }

        if (!(most >= least) || double.IsInfinity(most)) return (halfway, 0);

        var bounds = Bounds(reaches, most);
        return (new Point(room.X + (room.Width - bounds.Width) / 2 - bounds.X,
            room.Y + (room.Height - bounds.Height) / 2 - bounds.Y), most);
    }

    /// <summary>The box round a ring's whole sweep as drawn — stroke and ends included — for a ring centred on the
    /// origin.</summary>
    public static Rect Bounds(double radius, double thickness, double startAngle, double sweepAngle, PenLineCap caps) =>
        Bounds(Reaches(thickness, startAngle, Sweep(sweepAngle), caps), radius);

    private static Rect Bounds(List<Reach> reaches, double radius)
    {
        double left = double.PositiveInfinity, top = double.PositiveInfinity;
        double right = double.NegativeInfinity, bottom = double.NegativeInfinity;

        foreach (var reach in reaches)
        {
            var point = reach.At(radius);
            left = Math.Min(left, point.X);
            right = Math.Max(right, point.X);
            top = Math.Min(top, point.Y);
            bottom = Math.Max(bottom, point.Y);
        }

        return new Rect(new Point(left, top), new Point(right, bottom));
    }

    /// <summary>A point that can be an edge of the drawing: <see cref="Along"/> times the radius, plus
    /// <see cref="Offset"/>, from the centre.</summary>
    private readonly record struct Reach(Vector Along, Vector Offset)
    {
        public Point At(double radius) => new(Along.X * radius + Offset.X, Along.Y * radius + Offset.Y);
    }

    /// <summary>
    /// Every point that can be the furthest the drawing goes in some direction of the screen's axes.
    ///
    /// The stroke is every point within half the thickness of the circle, across the sweep. Along an axis, such a
    /// point is furthest out either at the stroke's outer or inner edge, and either at an end of the sweep or where
    /// the sweep crosses one of the axes — the quarter turns. Past the ends, each kind of end adds its own: a round
    /// end is a disc, furthest out half the stroke along each axis; a square one carries the stroke's edge half the
    /// stroke on; a triangle reaches half the stroke on, at its point. A whole turn is drawn as a circle and has
    /// no ends at all.
    ///
    /// The inner edge at a quarter turn is never the furthest while the ring has a hole in it. It is listed anyway,
    /// so a ring whose stroke is wider than its circle — the hole closed, the inner edge folded through the centre —
    /// is still bounded truly.
    /// </summary>
    private static List<Reach> Reaches(double thickness, double startAngle, double sweep, PenLineCap caps)
    {
        var half = thickness / 2;
        var reaches = new List<Reach>();

        var first = Math.Ceiling(startAngle / 90);
        var last = Math.Floor((startAngle + sweep) / 90);
        for (var quarter = first; quarter <= last; quarter++)
        {
            var along = Unit(quarter * 90);
            reaches.Add(new(along, along * half));
            reaches.Add(new(along, -along * half));
        }

        if (sweep >= 360) return reaches;

        // The way out of the sweep at each end: back along the circle at the start, on along it at the end.
        foreach (var (angle, onward) in new[] { (startAngle, -1d), (startAngle + sweep, 1d) })
        {
            var along = Unit(angle);
            var past = new Vector(-along.Y, along.X) * (onward * half);

            reaches.Add(new(along, along * half));
            reaches.Add(new(along, -along * half));

            switch (caps)
            {
                case PenLineCap.Round:
                    reaches.Add(new(along, new Vector(half, 0)));
                    reaches.Add(new(along, new Vector(-half, 0)));
                    reaches.Add(new(along, new Vector(0, half)));
                    reaches.Add(new(along, new Vector(0, -half)));
                    break;

                case PenLineCap.Square:
                    reaches.Add(new(along, along * half + past));
                    reaches.Add(new(along, -along * half + past));
                    break;

                case PenLineCap.Triangle:
                    reaches.Add(new(along, past));
                    break;
            }
        }

        return reaches;
    }

    private static Vector Unit(double degrees)
    {
        var radians = degrees * Math.PI / 180;
        return new(Math.Cos(radians), Math.Sin(radians));
    }
}
