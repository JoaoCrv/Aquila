using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Aquila.Controls.Primitives;

/// <summary>
/// The one place each shape is drawn: bars, marks, lines, areas, dots, guides and arcs, onto a
/// <see cref="DrawingContext"/>.
///
/// No state: everything comes in, is drawn, and nothing is kept. The elements call these in OnRender, and so may a piece
/// that draws many shapes in one visual without an element for each. Where the arithmetic is worth holding to a rule it
/// lives in <see cref="BarGeometry"/>, <see cref="PlotGeometry"/> and <see cref="ArcGeometry"/>; pens and fills come from
/// whoever calls, usually through its <see cref="Inks"/>.
///
/// Every geometry made here is frozen. The shapes that an element may keep while nothing they are made of changes — a
/// ring's track does not change while its arc travels — are offered on their own (<see cref="LineShape"/>,
/// <see cref="ArcShape"/>), so the element can keep them.
/// </summary>
public static class Draw
{
    // ── Bars ─────────────────────────────────────────────────────────────

    /// <summary>A bar: its track, under the filled share of it (<see cref="BarGeometry.Filled"/>). Each part's ends are
    /// rounded as far as it allows (<see cref="BarGeometry.Corner"/>), so a nearly empty bar ends in a dot.</summary>
    public static void Bar(DrawingContext drawing, Rect track, double fraction, Brush? fill, BarLook look,
        Orientation orientation = Orientation.Horizontal)
    {
        if (!(track.Width > 0) || !(track.Height > 0)) return;

        if (look.Track is { } under)
        {
            var corner = BarGeometry.Corner(look.Corner, track);
            drawing.DrawRoundedRectangle(under, null, track, corner, corner);
        }

        if (fill is null) return;

        var filled = BarGeometry.Filled(track, fraction, orientation);
        if (!(filled.Width > 0) || !(filled.Height > 0)) return;

        var round = BarGeometry.Corner(look.Corner, filled);
        drawing.DrawRoundedRectangle(fill, null, filled, round, round);
    }

    /// <summary>
    /// A bar split into parts — several shares of one whole, end to end (<see cref="BarGeometry.Segments"/>) — over
    /// its track, each part in its own brush at its own opacity. The bar's ends are rounded as a whole, never each
    /// part's: the parts are cut to the track's rounded outline, so a part that meets an end takes its curve and the
    /// joins between parts stay square.
    /// </summary>
    public static void Segments(DrawingContext drawing, Rect track, IReadOnlyList<(double Share, Brush Fill, double Opacity)> parts,
        BarLook look, Orientation orientation = Orientation.Horizontal)
    {
        if (!(track.Width > 0) || !(track.Height > 0)) return;

        var corner = BarGeometry.Corner(look.Corner, track);
        if (look.Track is { } under) drawing.DrawRoundedRectangle(under, null, track, corner, corner);

        var outline = new RectangleGeometry(track, corner, corner);
        outline.Freeze();
        drawing.PushClip(outline);

        var shares = new double[parts.Count];
        for (var i = 0; i < shares.Length; i++) shares[i] = parts[i].Share;
        var rects = BarGeometry.Segments(track, shares, orientation);

        for (var i = 0; i < rects.Length; i++)
        {
            if (!(rects[i].Width > 0) || !(rects[i].Height > 0)) continue;

            var (_, fill, opacity) = parts[i];
            var faded = opacity < 1;
            if (faded) drawing.PushOpacity(Math.Max(0, opacity));
            drawing.DrawRectangle(fill, null, rects[i]);
            if (faded) drawing.Pop();
        }

        drawing.Pop();
    }

    /// <summary>Marks across a track at shares of its length (<see cref="BarGeometry.Mark"/>), reaching
    /// <paramref name="reach"/> past it either side. Marks outside the track are not drawn.</summary>
    public static void Marks(DrawingContext drawing, Rect track, IReadOnlyList<double> marks, Pen pen, double reach,
        Orientation orientation = Orientation.Horizontal)
    {
        foreach (var mark in marks)
        {
            if (!BarGeometry.IsMark(mark)) continue;

            var (from, to) = BarGeometry.Mark(track, mark, reach, orientation);
            if (orientation == Orientation.Horizontal) Snapped(drawing, pen, from, to, from.X, vertical: true);
            else Snapped(drawing, pen, from, to, from.Y, vertical: false);
        }
    }

    // ── Lines ────────────────────────────────────────────────────────────

    /// <summary>A line through placed points (<see cref="PlotGeometry.Place"/>), broken at each gap, straight at a
    /// smoothness of nought and curved up to one without ever passing a reading (<see cref="PlotGeometry.Controls"/>).
    /// </summary>
    public static void Line(DrawingContext drawing, ReadOnlySpan<Point> points, double smoothness, Pen pen)
    {
        if (LineShape(points, smoothness) is { } shape) drawing.DrawGeometry(null, pen, shape);
    }

    /// <summary>The area between the same line and <paramref name="bottom"/>, one piece per stretch without a gap.
    /// </summary>
    public static void Area(DrawingContext drawing, ReadOnlySpan<Point> points, double smoothness, double bottom,
        Brush fill)
    {
        if (LineShape(points, smoothness, bottom) is { } shape) drawing.DrawGeometry(fill, null, shape);
    }

    /// <summary>
    /// The line's shape, frozen: a figure for each stretch of two points or more between gaps. Closed down to
    /// <paramref name="closedTo"/> and filled when it is given — the area under the line — and open otherwise. Null
    /// when there is nothing to draw.
    /// </summary>
    public static StreamGeometry? LineShape(ReadOnlySpan<Point> points, double smoothness, double? closedTo = null)
    {
        var geometry = new StreamGeometry();
        var drawn = false;

        using (var context = geometry.Open())
        {
            var at = 0;
            while (PlotGeometry.NextRun(points, ref at, out var start, out var length))
            {
                if (length < 2) continue;

                var run = points.Slice(start, length);
                if (closedTo is { } floor)
                {
                    context.BeginFigure(new Point(run[0].X, floor), isFilled: true, isClosed: true);
                    context.LineTo(run[0], isStroked: false, isSmoothJoin: false);
                }
                else
                {
                    context.BeginFigure(run[0], isFilled: false, isClosed: false);
                }

                Through(context, run, smoothness, stroked: closedTo is null);

                if (closedTo is { } end) context.LineTo(new Point(run[^1].X, end), isStroked: false, isSmoothJoin: false);
                drawn = true;
            }
        }

        if (!drawn) return null;

        geometry.Freeze();
        return geometry;
    }

    private static void Through(StreamGeometryContext context, ReadOnlySpan<Point> run, double smoothness, bool stroked)
    {
        if (!(smoothness > 0))
        {
            for (var i = 1; i < run.Length; i++) context.LineTo(run[i], stroked, isSmoothJoin: true);
            return;
        }

        for (var i = 0; i + 1 < run.Length; i++)
        {
            var (first, second) = PlotGeometry.Controls(run, i, smoothness);
            context.BezierTo(first, second, run[i + 1], stroked, isSmoothJoin: true);
        }
    }

    /// <summary>A dot of <paramref name="size"/> across at each placed point — a lone reading between two gaps
    /// included, which no line can show.</summary>
    public static void Dots(DrawingContext drawing, ReadOnlySpan<Point> points, double size, Brush fill)
    {
        if (!(size > 0)) return;

        var radius = size / 2;
        foreach (var point in points)
            if (PlotGeometry.IsPlaced(point))
                drawing.DrawEllipse(fill, null, point, radius, radius);
    }

    /// <summary>Lines across a plot at heights already placed (<see cref="PlotGeometry.YAt"/>) — a limit, say. One
    /// outside the plot is not drawn.</summary>
    public static void Guides(DrawingContext drawing, Rect plot, IReadOnlyList<double> ys, Pen pen)
    {
        foreach (var y in ys)
        {
            if (!double.IsFinite(y) || y < plot.Top || y > plot.Bottom) continue;
            Snapped(drawing, pen, new Point(plot.Left, y), new Point(plot.Right, y), y, vertical: false);
        }
    }

    // ── Arcs ─────────────────────────────────────────────────────────────

    /// <summary>An arc on a circle, <paramref name="sweep"/> degrees clockwise from <paramref name="from"/>
    /// (<see cref="ArcShape"/>).</summary>
    public static void Arc(DrawingContext drawing, Point centre, double radius, double from, double sweep, Pen pen)
    {
        if (ArcShape(centre, radius, from, sweep) is { } shape) drawing.DrawGeometry(null, pen, shape);
    }

    /// <summary>
    /// An arc's shape, frozen: <paramref name="sweep"/> degrees clockwise from <paramref name="from"/>, both counted as
    /// <see cref="ArcGeometry"/> counts them. Nothing at no sweep; a whole circle at a full turn, never an arc that
    /// happens to close — WPF, following SVG, draws an arc whose two ends are the same point as nothing at all.
    /// </summary>
    public static Geometry? ArcShape(Point centre, double radius, double from, double sweep)
    {
        var turn = ArcGeometry.Sweep(sweep);
        if (!(radius > 0) || turn <= 0) return null;

        Geometry shape;
        if (turn >= 360)
        {
            shape = new EllipseGeometry(centre, radius, radius);
        }
        else
        {
            var arc = new StreamGeometry();
            using (var context = arc.Open())
            {
                context.BeginFigure(ArcGeometry.PointAt(centre, radius, from), isFilled: false, isClosed: false);
                context.ArcTo(ArcGeometry.PointAt(centre, radius, from + turn), new Size(radius, radius), 0,
                    isLargeArc: turn > 180, SweepDirection.Clockwise, isStroked: true, isSmoothJoin: false);
            }

            shape = arc;
        }

        shape.Freeze();
        return shape;
    }

    // ── Shared ───────────────────────────────────────────────────────────

    /// <summary>A straight line held to the pixel grid across its length: a line one pixel wide that falls between two
    /// pixels is otherwise drawn two pixels wide at half strength.</summary>
    private static void Snapped(DrawingContext drawing, Pen pen, Point from, Point to, double at, bool vertical)
    {
        var half = pen.Thickness / 2;
        double[] guides = [at - half, at + half];

        drawing.PushGuidelineSet(vertical ? new GuidelineSet(guides, []) : new GuidelineSet([], guides));
        drawing.DrawLine(pen, from, to);
        drawing.Pop();
    }
}
