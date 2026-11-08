using Aquila.Controls;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Xunit;

namespace Aquila.Tests;

/// <summary>
/// The Number's trend arrow (<see cref="SensorTrend"/>): what it must say about any readings at all, and what it says
/// about the four the design was drawn from (docs/widget-numero).
/// </summary>
public sealed class TrendProperties
{
    private static readonly TimeSpan Now = TimeSpan.FromSeconds(100);

    /// <summary>One reading a second, the last of them now.</summary>
    private static List<(TimeSpan At, float? Value)> EverySecond(IReadOnlyList<float?> values) =>
        [.. values.Select((value, i) => (Now - TimeSpan.FromSeconds(values.Count - 1 - i), value))];

    private static TrendArrow Swapped(TrendArrow arrow) => arrow switch
    {
        TrendArrow.Up => TrendArrow.Down,
        TrendArrow.Down => TrendArrow.Up,
        _ => TrendArrow.None,
    };

    private static Gen<TrendArrow> Shown => Gen.Elements(TrendArrow.None, TrendArrow.Up, TrendArrow.Down);

    private static Gen<double?> Critical =>
        Gen.OneOf(Gen.Constant<double?>(null), Gen.Choose(1, 200).Select(c => (double?)c));

    private static Gen<float?> Reading => Gen.Choose(-1000, 100_000).Select(v => (float?)v);

    /// <summary>The range seen this session, or none yet.</summary>
    private static Gen<(float? Min, float? Max)> Range =>
        Gen.OneOf(
            Gen.Constant<(float?, float?)>((null, null)),
            from a in Gen.Choose(-1000, 100_000)
            from b in Gen.Choose(-1000, 100_000)
            select ((float?)Math.Min(a, b), (float?)Math.Max(a, b)));

    /// <summary>A reading that does not move has no arrow — whatever was showing before, whatever its limits.</summary>
    [Property(MaxTest = 500)]
    public Property A_steady_reading_has_no_arrow() =>
        Prop.ForAll(
            (from value in Reading
             from count in Gen.Choose(0, 20)
             from critical in Critical
             from range in Range
             from shown in Shown
             select (value, count, critical, range, shown)).ToArbitrary(),
            t => SensorTrend.Of(EverySecond(Enumerable.Repeat(t.value, t.count).ToList()), Now, t.critical,
                t.range.Min, t.range.Max, t.shown) == TrendArrow.None);

    /// <summary>A reading that has moved by more than its margin — here a fiftieth of its critical limit, with nothing
    /// before it to widen that — points the way it moved, whatever was showing.</summary>
    [Property(MaxTest = 500)]
    public Property A_move_past_the_margin_points_its_way() =>
        Prop.ForAll(
            (from start in Gen.Choose(0, 1000)
             from critical in Gen.Choose(1, 200)
             from extra in Gen.Choose(1, 50)
             from up in ArbMap.Default.GeneratorFor<bool>()
             from shown in Shown
             select (start, critical, extra, up, shown)).ToArbitrary(),
            t =>
            {
                var move = (int)Math.Ceiling(0.02 * t.critical) + t.extra;
                var after = t.start + (t.up ? move : -move);

                var values = Enumerable.Repeat((float?)t.start, 10).Concat(Enumerable.Repeat((float?)after, 3)).ToList();
                var arrow = SensorTrend.Of(EverySecond(values), Now, t.critical, null, null, t.shown);

                return arrow == (t.up ? TrendArrow.Up : TrendArrow.Down);
            });

    /// <summary>A reading jumping between two values points nowhere: its own spread widens the margin past anything
    /// the jumping can do to a mean — whatever was showing.</summary>
    [Property(MaxTest = 500)]
    public Property Noise_has_no_arrow() =>
        Prop.ForAll(
            (from a in Gen.Choose(0, 100_000)
             from b in Gen.Choose(0, 100_000)
             from length in Gen.Choose(13, 30)
             from critical in Critical
             from shown in Shown
             select (a, b, length, critical, shown)).ToArbitrary(),
            t =>
            {
                var values = Enumerable.Range(0, t.length).Select(i => (float?)(i % 2 == 0 ? t.a : t.b)).ToList();
                return SensorTrend.Of(EverySecond(values), Now, t.critical, null, null, t.shown) == TrendArrow.None;
            });

    /// <summary>A reading that was not there counts for nothing: missing readings put anywhere before the last one
    /// change nothing.</summary>
    [Property(MaxTest = 500)]
    public Property Missing_readings_change_nothing() =>
        Prop.ForAll(
            (from values in Reading.ListOf()
             from gaps in Gen.Choose(0, 40).ListOf()
             from critical in Critical
             from range in Range
             from shown in Shown
             select (values, gaps, critical, range, shown)).ToArbitrary(),
            t =>
            {
                var samples = EverySecond([.. t.values]);

                // Half a second off the readings, and every one of them before now — so the last reading is still the
                // last one taken.
                var holed = samples
                    .Concat(t.gaps.Select(gap => (Now - TimeSpan.FromSeconds(gap + 0.5), (float?)null)))
                    .ToList();

                return SensorTrend.Of(samples, Now, t.critical, t.range.Min, t.range.Max, t.shown)
                    == SensorTrend.Of(holed, Now, t.critical, t.range.Min, t.range.Max, t.shown);
            });

    /// <summary>Too few readings say nothing — under two in the last three seconds, or under five in the ten before
    /// — however far apart they are, and whatever was showing.</summary>
    [Property(MaxTest = 500)]
    public Property Too_few_readings_say_nothing() =>
        Prop.ForAll(
            (from recent in Gen.Choose(0, 3)
             from before in Gen.Choose(0, 10)
             where recent < 2 || before < 5
             from values in Reading.ArrayOf(recent + before)
             from critical in Critical
             from shown in Shown
             select (recent, before, values, critical, shown)).ToArbitrary(),
            t =>
            {
                // The recent ones in the last three seconds, the earlier ones in the ten before them.
                var samples = Enumerable.Range(0, t.recent)
                    .Select(i => (Now - TimeSpan.FromSeconds(i * 0.9), t.values[i]))
                    .Concat(Enumerable.Range(0, t.before)
                        .Select(i => (Now - TimeSpan.FromSeconds(3.5 + i * 0.9), t.values[t.recent + i])))
                    .ToList();

                return SensorTrend.Of(samples, Now, t.critical, null, null, t.shown) == TrendArrow.None;
            });

    /// <summary>
    /// An arrow showing stays on a move that would not have put it there: between half the margin and the margin, the
    /// arrow that was showing goes on showing, and none showing stays none — so a reading on the edge does not blink.
    /// </summary>
    [Property(MaxTest = 500)]
    public Property An_arrow_stays_on_half_its_margin() =>
        Prop.ForAll(
            (from start in Gen.Choose(0, 1000)
             from steps in Gen.Choose(1, 4)
             from up in ArbMap.Default.GeneratorFor<bool>()
             select (start, steps, up)).ToArbitrary(),
            t =>
            {
                // A critical limit of 50 a step makes the margin a whole number, and three quarters of it a float that
                // is exact — so the move is exactly between the two edges.
                var critical = 50.0 * t.steps;
                var move = (float)(0.75 * 0.02 * critical);
                var after = t.start + (t.up ? move : -move);

                var values = Enumerable.Repeat((float?)t.start, 10).Concat(Enumerable.Repeat((float?)after, 3)).ToList();
                var samples = EverySecond(values);
                var arrow = t.up ? TrendArrow.Up : TrendArrow.Down;

                return SensorTrend.Of(samples, Now, critical, null, null, arrow) == arrow
                    && SensorTrend.Of(samples, Now, critical, null, null, TrendArrow.None) == TrendArrow.None
                    && SensorTrend.Of(samples, Now, critical, null, null, Swapped(arrow)) == TrendArrow.None;
            });

    /// <summary>The rule has no favourite direction: the same readings upside down point the other way.</summary>
    [Property(MaxTest = 500)]
    public Property Negated_readings_swap_the_arrow() =>
        Prop.ForAll(
            (from values in Gen.Choose(-1000, 1000).Select(v => (float?)v).ListOf()
             from critical in Critical
             from range in Range
             from shown in Shown
             select (values, critical, range, shown)).ToArbitrary(),
            t =>
            {
                var samples = EverySecond([.. t.values]);
                var negated = EverySecond([.. t.values.Select(v => -v)]);

                return SensorTrend.Of(negated, Now, t.critical, -t.range.Max, -t.range.Min, Swapped(t.shown))
                    == Swapped(SensorTrend.Of(samples, Now, t.critical, t.range.Min, t.range.Max, t.shown));
            });

    // ── The design's four examples ───────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Walks a reading through time a second at a time, as the widget does — each answer handed to the next as the
    /// arrow showing — and gives every answer.
    /// </summary>
    private static List<TrendArrow> Walk(IReadOnlyList<float> readings, double? critical, float? min, float? max)
    {
        var arrows = new List<TrendArrow>();
        var shown = TrendArrow.None;

        for (var i = 0; i < readings.Count; i++)
        {
            var now = TimeSpan.FromSeconds(i);
            var samples = readings.Take(i + 1).Select((value, j) => (TimeSpan.FromSeconds(j), (float?)value)).ToList();

            shown = SensorTrend.Of(samples, now, critical, min, max, shown);
            arrows.Add(shown);
        }

        return arrows;
    }

    /// <summary>A CPU going from 45 to 60 °C in five seconds, critical at 95: up within two or three seconds of the
    /// climb starting, and nothing before it, though it wobbled half a degree while it idled.</summary>
    [Fact]
    public void A_cpu_heating_up_points_up_within_three_seconds()
    {
        float[] idle = [45, 45.5f, 44.5f, 45, 45.5f, 44.5f, 45, 45.5f, 44.5f, 45];
        float[] climb = [48, 51, 54, 57, 60];

        var arrows = Walk([.. idle, .. climb], 95, 41, 60);

        Assert.All(arrows.Take(idle.Length), arrow => Assert.Equal(TrendArrow.None, arrow));
        Assert.Equal(TrendArrow.Up, arrows[idle.Length + 2]);
    }

    /// <summary>A load jumping about between 5 and 40% at rest, critical at 92: never an arrow.</summary>
    [Fact]
    public void A_load_jumping_at_rest_has_no_arrow()
    {
        float[] pattern = [5, 40, 5, 5, 40, 5, 40, 40, 5, 40, 5, 5, 40, 40, 5];
        var readings = Enumerable.Range(0, 4).SelectMany(_ => pattern).ToList();

        Assert.All(Walk(readings, 92, 5, 40), arrow => Assert.Equal(TrendArrow.None, arrow));
    }

    /// <summary>A GPU drawing a steady 180 W, give or take 3, with no limits and a session that went from 21 to 331:
    /// never an arrow.</summary>
    [Fact]
    public void A_steady_draw_has_no_arrow()
    {
        float[] pattern = [177, 183, 180, 178, 182, 181, 179, 183, 177, 180, 182, 178];
        var readings = Enumerable.Range(0, 4).SelectMany(_ => pattern).ToList();

        Assert.All(Walk(readings, null, 21, 331), arrow => Assert.Equal(TrendArrow.None, arrow));
    }

    /// <summary>A drive reading 300 MB/s that stops, with no limits and a session peak of 980 MB/s: down.</summary>
    [Fact]
    public void A_drive_going_quiet_points_down()
    {
        const float megabyte = 1_048_576f;
        var readings = Enumerable.Repeat(300 * megabyte, 10).Concat(Enumerable.Repeat(0f, 3)).ToList();

        Assert.Equal(TrendArrow.Down, Walk(readings, null, 0, 980 * megabyte)[^1]);
    }
}
