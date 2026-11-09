using System.Windows;
using Aquila.Controls.Primitives;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Xunit;

namespace Aquila.Tests;

/// <summary>
/// A line of readings (<see cref="PlotGeometry"/>). A curve that overshoots, a chart showing readings from minutes ago,
/// a flat line pinned to an edge — each looks like a rendering glitch on screen and is a sum here, held for any series.
/// </summary>
public sealed class PlotProperties
{
    private const double Tolerance = 1e-9;

    private static readonly Rect Box = new(3, 5, 300, 80);

    /// <summary>A reading: anywhere a sensor goes, with the edges a chart meets most often drawn often.</summary>
    private static Gen<double> Readings() =>
        Gen.Frequency(
            (6, Gen.Choose(-1_000_000, 1_000_000).Select(v => v / 1_000d)),
            (2, Gen.Elements(0d, 100d, 1d, -1d)));

    /// <summary>A series, oldest first: none to a few hundred readings.</summary>
    private static Gen<double[]> Series() =>
        from count in Gen.Frequency((3, Gen.Choose(0, 70)), (1, Gen.Choose(70, 400)))
        from values in Readings().ArrayOf(count)
        select values;

    private static Gen<int> Windows() => Gen.Frequency((4, Gen.Choose(2, 400)), (1, Gen.Elements(-5, 0, 1, 60, 300)));

    private static Gen<double> Smoothness() =>
        Gen.OneOf(Gen.Choose(0, 1000).Select(v => v / 1000d), Gen.Elements(0d, 0.5, 1d, double.NaN, -1d, 2d));

    /// <summary>A run of placed points: rising X at any spacing, any heights — more than a chart places, on purpose.
    /// </summary>
    private static Gen<Point[]> Runs() =>
        from count in Gen.Choose(2, 40)
        from steps in Gen.Choose(1, 50).ArrayOf(count)
        from heights in Gen.Choose(-500, 500).ArrayOf(count)
        select steps.Select((step, i) => new Point(steps.Take(i + 1).Sum(), heights[i])).ToArray();

    private static Point Bezier(Point a, Point b, Point c, Point d, double t)
    {
        var u = 1 - t;
        return new Point(
            u * u * u * a.X + 3 * u * u * t * b.X + 3 * u * t * t * c.X + t * t * t * d.X,
            u * u * u * a.Y + 3 * u * u * t * b.Y + 3 * u * t * t * c.Y + t * t * t * d.Y);
    }

    private static bool Between(double value, double a, double b) =>
        value >= Math.Min(a, b) - Tolerance && value <= Math.Max(a, b) + Tolerance;

    /// <summary>
    /// The curve never passes the readings it joins, at any smoothness: each control point, and every point along the
    /// curve, lies between the two readings either side of it — in height and in time. A load that peaked at 100 is
    /// never drawn at 102.
    /// </summary>
    [Property(MaxTest = 1000)]
    public Property The_curve_never_passes_the_readings_it_joins() =>
        Prop.ForAll(Runs().ToArbitrary(), Smoothness().ToArbitrary(), (run, smoothness) =>
        {
            for (var i = 0; i + 1 < run.Length; i++)
            {
                var (a, d) = (run[i], run[i + 1]);
                var (b, c) = PlotGeometry.Controls(run, i, smoothness);

                if (!Between(b.Y, a.Y, d.Y) || !Between(c.Y, a.Y, d.Y)) return false;
                if (!Between(b.X, a.X, d.X) || !Between(c.X, a.X, d.X)) return false;

                for (var t = 0.05; t < 1; t += 0.05)
                {
                    var at = Bezier(a, b, c, d, t);
                    if (!Between(at.Y, a.Y, d.Y) || !Between(at.X, a.X, d.X)) return false;
                }
            }

            return true;
        });

    /// <summary>At nought smoothness the segments are straight: both control points sit on the ends.</summary>
    [Property(MaxTest = 300)]
    public Property No_smoothness_is_straight_segments() =>
        Prop.ForAll(Runs().ToArbitrary(), run =>
            Enumerable.Range(0, run.Length - 1).All(i =>
                PlotGeometry.Controls(run, i, 0) == (run[i], run[i + 1])));

    /// <summary>
    /// A window shows the LAST readings of the series, however long the series is: the last point placed is the newest
    /// reading. The charting library counted from the start, so a card sharing a sensor with a longer chart showed what
    /// happened minutes ago.
    /// </summary>
    [Property(MaxTest = 500)]
    public Property A_window_shows_the_last_readings() =>
        Prop.ForAll(Gen.Choose(0, 600).ToArbitrary(), Windows().ToArbitrary(), (count, window) =>
        {
            var values = Enumerable.Range(0, count).Select(i => (double)i).ToArray();
            var (first, shown) = PlotGeometry.Visible(count, window);
            if (shown != Math.Min(count, Math.Max(PlotGeometry.LeastWindow, window)) || first + shown != count)
                return false;

            var points = new Point[shown];
            var placed = PlotGeometry.Place(values, window, Box, 0, Math.Max(1, count), points);
            if (placed != shown) return false;

            // Read back from the height: the newest point placed is the newest reading.
            return shown == 0
                || Math.Abs(Box.Bottom - points[^1].Y - (count - 1) / Math.Max(1d, count) * Box.Height) < 1e-6;
        });

    /// <summary>
    /// The step is the whole window's, from the left: a series shorter than its window is a short line growing across
    /// the box, and only a full window reaches the right edge.
    /// </summary>
    [Property(MaxTest = 500)]
    public Property A_short_series_grows_from_the_left() =>
        Prop.ForAll(Series().ToArbitrary(), Windows().ToArbitrary(), (values, window) =>
        {
            var points = new Point[values.Length];
            var placed = PlotGeometry.Place(values, window, Box, 0, 1, points);
            if (placed == 0) return true;

            var span = Math.Max(PlotGeometry.LeastWindow, window) - 1;
            var step = Box.Width / span;
            var spaced = Enumerable.Range(0, placed).All(i => Math.Abs(points[i].X - (Box.X + i * step)) < 1e-6);
            var reachesRight = Math.Abs(points[placed - 1].X - Box.Right) < 1e-6;

            return spaced && points[0].X == Box.X && reachesRight == (placed == span + 1);
        });

    /// <summary>A fitted scale holds every reading shown, of either series, and a scale given whole is kept.</summary>
    [Property(MaxTest = 500)]
    public Property A_fitted_scale_holds_every_reading_shown() =>
        Prop.ForAll(
            (from values in Series()
             from second in Series()
             from window in Windows()
             select (values, second, window)).ToArbitrary(),
            chart =>
        {
            var (values, second, window) = chart;
            var (min, max) = PlotGeometry.Range(values, second, window, double.NaN, double.NaN);
            if (!(max > min)) return false;

            bool Held(double[] series)
            {
                var (first, shown) = PlotGeometry.Visible(series.Length, window);
                return series.Skip(first).Take(shown).All(v => v >= min && v <= max);
            }

            return Held(values) && Held(second)
                && PlotGeometry.Range(values, second, window, -3, 7) == (-3, 7);
        });

    /// <summary>A flat series on a fitted scale is drawn halfway up, whatever its value — never pinned to an edge.
    /// </summary>
    [Property(MaxTest = 300)]
    public Property A_flat_series_is_drawn_halfway_up() =>
        Prop.ForAll(Readings().ToArbitrary(), Gen.Choose(1, 100).ToArbitrary(), (value, count) =>
        {
            var values = Enumerable.Repeat(value, count).ToArray();
            var (min, max) = PlotGeometry.Range(values, null, 60, double.NaN, double.NaN);
            return Math.Abs(PlotGeometry.YAt(value, Box, min, max) - (Box.Top + Box.Bottom) / 2) < 1e-6;
        });

    /// <summary>Noughts on a scale from nought lie along the bottom, which is what they say — a disk at rest is not
    /// drawn half busy.</summary>
    [Fact]
    public void Noughts_on_a_scale_from_nought_lie_along_the_bottom()
    {
        var values = new double[30];
        var (min, max) = PlotGeometry.Range(values, null, 60, 0, double.NaN);
        Assert.Equal(0, min);
        Assert.True(max > 0);
        Assert.Equal(Box.Bottom, PlotGeometry.YAt(0, Box, min, max));
    }

    /// <summary>The top typed under the bottom is fitted, never a scale with no height — the bottom kept as typed.
    /// </summary>
    [Fact]
    public void A_top_under_the_bottom_is_fitted_instead()
    {
        var (min, max) = PlotGeometry.Range([10, 40, 25], null, 60, 30, 20);
        Assert.Equal(30, min);
        Assert.Equal(40 + 10 * PlotGeometry.Headroom, max, 9);
    }

    /// <summary>
    /// A fitted end reaches <see cref="PlotGeometry.Headroom"/> of the scale past the furthest reading, so no line
    /// touches the top or the bottom of its box; a fixed end stays exactly where it was given.
    /// </summary>
    [Property(MaxTest = 500)]
    public Property A_fitted_end_keeps_its_headroom() =>
        Prop.ForAll(Series().ToArbitrary(), Windows().ToArbitrary(), (values, window) =>
        {
            var (first, shown) = PlotGeometry.Visible(values.Length, window);
            var seen = values.Skip(first).Take(shown).ToArray();
            if (seen.Length == 0 || seen.Max() == seen.Min()) return true;

            var (low, high) = (seen.Min(), seen.Max());
            var (min, max) = PlotGeometry.Range(values, null, window, double.NaN, double.NaN);
            var room = (max - min) * PlotGeometry.Headroom / (1 + 2 * PlotGeometry.Headroom);

            var (zeroMin, zeroMax) = PlotGeometry.Range(values, null, window, 0, double.NaN);
            var keepsNought = zeroMin == 0;

            return Math.Abs(max - high - room) < 1e-6 * Math.Max(1, max - min)
                && Math.Abs(low - min - room) < 1e-6 * Math.Max(1, max - min)
                && keepsNought && zeroMax > Math.Max(high, 0);
        });

    /// <summary>
    /// A gap breaks the line: the stretches found cover every reading exactly once, and none crosses a gap. A reading
    /// that was never taken is not drawn as a slope between its neighbours.
    /// </summary>
    [Property(MaxTest = 500)]
    public Property A_gap_breaks_the_line() =>
        Prop.ForAll(
            (from count in Gen.Choose(0, 80)
             from gaps in Gen.Elements(true, false, false, false).ArrayOf(count)
             select gaps).ToArbitrary(),
            gaps =>
            {
                var points = gaps.Select((gap, i) => new Point(i, gap ? double.NaN : i)).ToArray();
                var covered = new bool[points.Length];

                var at = 0;
                while (PlotGeometry.NextRun(points, ref at, out var start, out var length))
                    for (var i = start; i < start + length; i++)
                    {
                        if (gaps[i] || covered[i]) return false;
                        covered[i] = true;
                    }

                return Enumerable.Range(0, points.Length).All(i => covered[i] != gaps[i]);
            });

    /// <summary>
    /// Labels set apart keep their order, stand at least a label's height apart, stay inside the box when they fit,
    /// and labels that never touched are left exactly where they wanted to be.
    /// </summary>
    [Property(MaxTest = 1000)]
    public Property Labels_are_set_apart_by_the_least_that_clears_them() =>
        Prop.ForAll(
            (from count in Gen.Choose(1, 6)
             from wanted in Gen.Choose(0, 2000).Select(v => v / 10d).ArrayOf(count)
             from tall in Gen.Choose(5, 30)
             select (wanted, (double)tall)).ToArbitrary(),
            labels =>
            {
                var (wanted, tall) = labels;
                const double top = 0, bottom = 200;
                var placed = PlotGeometry.Spread(wanted, tall, top, bottom);
                var order = Enumerable.Range(0, wanted.Length).OrderBy(i => wanted[i]).ThenBy(i => i).ToArray();

                for (var k = 1; k < order.Length; k++)
                    if (placed[order[k]] - placed[order[k - 1]] < tall - 1e-6) return false;

                var fits = wanted.Length * tall <= bottom - top;
                if (fits && placed.Any(y => y < top + tall / 2 - 1e-6 || y > bottom - tall / 2 + 1e-6)) return false;

                var apart = Enumerable.Range(1, order.Length - 1).All(k => wanted[order[k]] - wanted[order[k - 1]] >= tall)
                    && wanted.All(y => y >= top + tall / 2 && y <= bottom - tall / 2);
                return !apart || placed.SequenceEqual(wanted);
            });

    /// <summary>Two labels wanting the same place share the move — one up, one down, by half a label each — rather than
    /// one staying and the other taking it all.</summary>
    [Fact]
    public void Two_labels_on_one_place_move_half_each()
    {
        Assert.Equal([95d, 105d], PlotGeometry.Spread([100d, 100d], 10, 0, 200));
        Assert.Equal([175d, 185d, 195d], PlotGeometry.Spread([198d, 198d, 199d], 10, 0, 200));
    }

    /// <summary>The peak is the highest reading the window shows — the first of them when several share it — and none
    /// when nothing shown is a number.</summary>
    [Property(MaxTest = 500)]
    public Property The_peak_is_the_highest_reading_shown() =>
        Prop.ForAll(Series().ToArbitrary(), Windows().ToArbitrary(), (values, window) =>
        {
            var (first, shown) = PlotGeometry.Visible(values.Length, window);
            var seen = values.Skip(first).Take(shown).ToArray();
            var at = PlotGeometry.Peak(values, window);

            return seen.Length == 0 ? at == -1 : at == Array.IndexOf(seen, seen.Max());
        });

    /// <summary>The window of the bug that started this: 300 readings in a window of 60 show the last 60.</summary>
    [Fact]
    public void Three_hundred_readings_in_a_window_of_sixty_show_the_last_sixty() =>
        Assert.Equal((240, 60), PlotGeometry.Visible(300, 60));

    /// <summary>At a peak the curve is level: it turns at the reading, not past it.</summary>
    [Fact]
    public void A_peak_is_level()
    {
        Point[] run = [new(0, 50), new(10, 0), new(20, 50)];
        Assert.Equal(0, PlotGeometry.Tangent(run, 1));
    }
}
