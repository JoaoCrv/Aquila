using System.Windows;

namespace Aquila.Controls.Primitives;

/// <summary>What a ring draws for a value: nothing, an arc, or the whole circle.</summary>
public enum ArcShape
{
    None,
    Arc,
    Circle,
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
}
