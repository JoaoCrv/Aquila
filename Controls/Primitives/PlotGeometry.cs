using System.Windows;

namespace Aquila.Controls.Primitives;

/// <summary>
/// The arithmetic of a line of readings — which are shown, on what scale, where each falls and the curve between
/// them — kept apart from the drawing, as <see cref="ArcGeometry"/> is for the ring, so each rule can be held for any
/// series without rendering anything.
///
/// A series is oldest first. Its X axis is time: one step per reading, the same step however many there are.
/// </summary>
public static class PlotGeometry
{
    /// <summary>The fewest readings a window can span: a line needs two ends.</summary>
    public const int LeastWindow = 2;

    /// <summary>
    /// Which readings a window shows: always the LAST of the series, as many as fit.
    ///
    /// The series is often longer than the window — a sensor keeps the history its longest chart asks for, and every
    /// other chart of it reads the same list. A window counted from the start would show the oldest readings there,
    /// minutes behind the present, which is what the charting library did.
    /// </summary>
    public static (int First, int Shown) Visible(int count, int window)
    {
        var total = Math.Max(0, count);
        var shown = Math.Min(total, Math.Max(LeastWindow, window));
        return (total - shown, shown);
    }

    /// <summary>
    /// How far past the furthest reading a fitted end of the scale reaches, as a share of the scale: a tenth.
    ///
    /// Fitted exactly, the highest reading touches the top of the box and the line runs along its edge, where a card's
    /// border or a widget's title sits. The charting library left far more — a fitted line used about half the height —
    /// and a tenth keeps most of what fitting gains while no line touches an edge. A fixed end is never moved: a scale
    /// from nought starts at nought.
    /// </summary>
    public const double Headroom = 0.1;

    /// <summary>
    /// The scale's ends.
    ///
    /// A finite end is kept; a NaN one is fitted to the readings shown of either series, so two lines share a scale,
    /// and given <see cref="Headroom"/> beyond them. Both given with the top at or under the bottom, the top is fitted
    /// instead — an editor lets the two be typed in any order, and passing through the moment they cross must not
    /// collapse the chart.
    ///
    /// A scale with no height — a flat series, a single reading, a fitted end meeting the fixed one — is opened, away
    /// from a fixed end, so noughts on a scale from nought lie along the bottom, which is what they say; and around
    /// the middle when both ends were fitted, so a flat series is drawn halfway up rather than pinned to an edge. With
    /// nothing to fit to, it is nought to one.
    /// </summary>
    public static (double Min, double Max) Range(IReadOnlyList<double>? values, IReadOnlyList<double>? second,
        int window, double min, double max)
    {
        var fitBottom = !double.IsFinite(min);
        var fitTop = !double.IsFinite(max) || (!fitBottom && max <= min);
        if (!fitBottom && !fitTop) return (min, max);

        var (low, high) = (double.PositiveInfinity, double.NegativeInfinity);
        Extent(values, window, ref low, ref high);
        Extent(second, window, ref low, ref high);

        var (bottom, top) = Ends(fitBottom ? low : min, fitTop ? high : max, fitBottom, fitTop);
        var room = (top - bottom) * Headroom;

        return (fitBottom ? bottom - room : bottom, fitTop ? top + room : top);
    }

    /// <summary>The ends the readings reach, opened where they have no height between them.</summary>
    private static (double Bottom, double Top) Ends(double bottom, double top, bool fitBottom, bool fitTop)
    {
        if (!double.IsFinite(bottom) && !double.IsFinite(top)) return (0, 1);
        if (!double.IsFinite(bottom)) bottom = top - Span(top);
        if (!double.IsFinite(top)) top = bottom + Span(bottom);
        if (top > bottom) return (bottom, top);

        return (fitBottom, fitTop) switch
        {
            (true, true) => (bottom - Span(bottom) / 2, bottom + Span(bottom) / 2),
            (true, false) => (top - Span(top), top),
            _ => (bottom, bottom + Span(bottom)),
        };
    }

    /// <summary>How far to open a scale with no height around a value: as far as the value is from nought, so the
    /// opening is in the reading's own terms (a volt and a byte rate alike), and one where the value is nought.</summary>
    private static double Span(double around) => around == 0 ? 1 : Math.Abs(around);

    private static void Extent(IReadOnlyList<double>? series, int window, ref double low, ref double high)
    {
        if (series is null) return;

        var (first, shown) = Visible(series.Count, window);
        for (var i = first; i < first + shown; i++)
        {
            var value = series[i];
            if (!double.IsFinite(value)) continue;
            if (value < low) low = value;
            if (value > high) high = value;
        }
    }

    /// <summary>Where a value falls in a box on a scale: the bottom edge at <paramref name="min"/>, the top at
    /// <paramref name="max"/>, and outside the box beyond them — whoever draws clips, so a line leaving the scale
    /// leaves it where it really would. NaN for a value that is not a number, or a scale with no height.</summary>
    public static double YAt(double value, Rect box, double min, double max)
    {
        var height = max - min;
        return double.IsFinite(value) && height > 0 ? box.Bottom - (value - min) / height * box.Height : double.NaN;
    }

    /// <summary>
    /// The readings a window shows, placed in a box, into <paramref name="into"/>; how many were written.
    ///
    /// The step is the whole window's — the box's width over <c>window − 1</c> — from the left edge, so a series still
    /// shorter than its window is a short line growing across the box, never one stretched across it to look like more
    /// history than there is. A reading that is not a number is placed with a NaN height: a gap (<see cref="NextRun"/>).
    /// </summary>
    public static int Place(IReadOnlyList<double> values, int window, Rect box, double min, double max, Span<Point> into)
    {
        var (first, shown) = Visible(values.Count, window);
        var step = box.Width / (Math.Max(LeastWindow, window) - 1);
        var count = Math.Min(shown, into.Length);

        for (var i = 0; i < count; i++)
            into[i] = new Point(box.X + i * step, YAt(values[first + i], box, min, max));

        return count;
    }

    /// <summary>Whether a point was placed — a reading, not a gap.</summary>
    public static bool IsPlaced(Point point) => double.IsFinite(point.X) && double.IsFinite(point.Y);

    /// <summary>
    /// The next stretch of placed points with no gap in it, from <paramref name="from"/> on, which it moves past the
    /// stretch: where it starts and how many points it has. False when there are no more.
    ///
    /// A gap breaks the line there, so a reading that was never taken is not drawn as a slope between its neighbours.
    /// A stretch of one point — a reading with a gap either side — is a dot, never a line.
    /// </summary>
    public static bool NextRun(ReadOnlySpan<Point> points, ref int from, out int start, out int length)
    {
        while (from < points.Length && !IsPlaced(points[from])) from++;
        start = from;
        while (from < points.Length && IsPlaced(points[from])) from++;
        length = from - start;
        return length > 0;
    }

    /// <summary>
    /// The two control points of the curve from <c>run[i]</c> to <c>run[i + 1]</c>: Steffen's tangents (1990) at each
    /// end, scaled by <paramref name="smoothness"/>.
    ///
    /// Each control point lies between the two readings the curve joins, and a cubic stays within its control points,
    /// so the curve never passes the readings: a load that peaked at 100 is never drawn at 102, and a reading that
    /// dipped to its floor never below it. At nought both control points sit on the ends, and the segment is straight.
    /// </summary>
    public static (Point First, Point Second) Controls(ReadOnlySpan<Point> run, int i, double smoothness)
    {
        var share = double.IsNaN(smoothness) ? 0 : Math.Clamp(smoothness, 0, 1);
        var (from, to) = (run[i], run[i + 1]);
        var third = (to.X - from.X) / 3 * share;

        return (new Point(from.X + third, from.Y + Tangent(run, i) * third),
            new Point(to.X - third, to.Y - Tangent(run, i + 1) * third));
    }

    /// <summary>
    /// The slope the curve has at <c>run[k]</c>.
    ///
    /// Inside a run, Steffen's: level at a peak, a trough or beside a flat stretch, so the curve turns at the reading and
    /// not past it; otherwise the slope through the neighbours, held to twice the gentler of the two sides, which keeps
    /// each control point within its own segment's rise. At either end, the slope of the one segment there.
    /// </summary>
    public static double Tangent(ReadOnlySpan<Point> run, int k)
    {
        if (run.Length < 2) return 0;
        if (k == 0) return Slope(run[0], run[1]);
        if (k == run.Length - 1) return Slope(run[k - 1], run[k]);

        var (before, after) = (Slope(run[k - 1], run[k]), Slope(run[k], run[k + 1]));
        if (before * after <= 0) return 0;

        var (left, right) = (run[k].X - run[k - 1].X, run[k + 1].X - run[k].X);
        var through = (before * right + after * left) / (left + right);

        return Math.Sign(before) * Math.Min(2 * Math.Min(Math.Abs(before), Math.Abs(after)), Math.Abs(through));
    }

    private static double Slope(Point from, Point to)
    {
        var run = to.X - from.X;
        return run > 0 ? (to.Y - from.Y) / run : 0;
    }
}
