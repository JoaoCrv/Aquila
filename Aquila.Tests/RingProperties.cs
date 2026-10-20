using System.Windows;
using Aquila.Controls.Primitives;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Xunit;

namespace Aquila.Tests;

/// <summary>
/// The ring primitive's arithmetic (<see cref="RingMotion"/>, <see cref="ArcGeometry"/>). A ring that unwound a
/// whole turn backwards, arrived a little after it was told to, or drew its end off its own circle looks like a
/// rendering glitch on the desktop and is a sum here — so each rule is held for any move and any arc.
/// </summary>
public sealed class RingProperties
{
    /// <summary>A value on the ring: anywhere from nothing to full, with both ends and the middle drawn often, since
    /// an even spread would almost never land on them.</summary>
    private static Gen<double> Values() =>
        Gen.OneOf(
            Gen.Choose(0, 1_000_000).Select(v => v / 1_000_000d),
            Gen.Elements(0d, 0.5, 1d, 59 / 60d));

    /// <summary>A move: from, to, how long — including no time and less than none, which a late caller can ask for
    /// — and whether it may go back.</summary>
    private static Arbitrary<(double Current, double Target, TimeSpan Duration, bool Forward)> Moves() =>
        (from current in Values()
         from target in Values()
         from ticks in Gen.OneOf(Gen.Choose(-1_000_000, 50_000_000), Gen.Elements(0, 1, 9_750_000))
         from forward in Gen.Elements(true, false)
         select (current, target, TimeSpan.FromTicks(ticks), forward))
        .ToArbitrary();

    /// <summary>However a move is split, its stretches take exactly the time it was given — so a ring told to be
    /// somewhere at a second is there at that second, not a rounding later. Less than no time is taken as none.
    /// </summary>
    [Property(MaxTest = 1000)]
    public Property A_moves_stretches_add_up_to_its_duration() =>
        Prop.ForAll(Moves(), move =>
        {
            var (current, target, duration, forward) = move;
            var plan = RingMotion.Plan(current, target, duration, forward);

            var total = plan.Aggregate(TimeSpan.Zero, (sum, step) => sum + step.Duration);
            return total == (duration < TimeSpan.Zero ? TimeSpan.Zero : duration)
                && plan.All(step => step.Duration >= TimeSpan.Zero);
        });

    /// <summary>
    /// Forward only, a ring never goes back — except once, from full to nothing, which on a ring is the same place.
    /// Every stretch rises (or holds), and the only place one starts somewhere other than where the last ended is that
    /// reset. A slip of the last bit is allowed for, and only that: <see cref="RingMotion.Tolerance"/>.
    /// </summary>
    [Property(MaxTest = 1000)]
    public Property Forward_only_never_goes_back_but_once_to_nothing() =>
        Prop.ForAll(Moves(), move =>
        {
            var (current, target, duration, _) = move;
            var plan = RingMotion.Plan(current, target, duration, forwardOnly: true);

            var at = current;
            var resets = 0;

            foreach (var step in plan)
            {
                if (step.From != at)
                {
                    if (step.From != 0 || at != RingMotion.Full) return false;
                    resets++;
                }

                if (step.To < step.From - RingMotion.Tolerance) return false;
                at = step.To;
            }

            return resets <= 1;
        });

    /// <summary>Wherever it goes on the way, a move ends where it was told to.</summary>
    [Property(MaxTest = 1000)]
    public Property A_move_ends_at_its_target() =>
        Prop.ForAll(Moves(), move =>
        {
            var (current, target, duration, forward) = move;
            var plan = RingMotion.Plan(current, target, duration, forward);
            return plan.Length > 0 && plan[0].From == current && plan[^1].To == target;
        });

    /// <summary>Allowed to go back — a dial — a move is one stretch, straight there, whichever way it points.
    /// </summary>
    [Property(MaxTest = 1000)]
    public Property Without_forward_only_a_move_is_one_stretch() =>
        Prop.ForAll(Moves(), move =>
        {
            var (current, target, duration, _) = move;
            var plan = RingMotion.Plan(current, target, duration, forwardOnly: false);
            return plan.Length == 1
                && plan[0] == new RingMotion.Segment(current, target,
                    duration < TimeSpan.Zero ? TimeSpan.Zero : duration);
        });

    /// <summary>
    /// Part-way through a move, <see cref="RingMotion.At"/> puts the ring exactly where carrying out the plan would
    /// have it — which is what lets a clock shown mid-second start where it should and carry on without a lurch. Full
    /// and nothing are one place on a ring, so a position at the reset may be reported as either.
    /// </summary>
    [Property(MaxTest = 1000)]
    public Property Part_way_through_a_move_is_on_its_plan() =>
        Prop.ForAll(
            (from current in Values()
             from target in Values()
             from share in Gen.Choose(0, 1000).Select(s => s / 1000d)
             from forward in Gen.Elements(true, false)
             select (current, target, share, forward))
            .ToArbitrary(),
            move =>
            {
                var (current, target, share, forward) = move;
                var second = TimeSpan.FromSeconds(1);
                var plan = RingMotion.Plan(current, target, second, forward);

                var elapsed = second * share;
                var walked = plan[^1].To;
                foreach (var step in plan)
                {
                    if (elapsed <= step.Duration)
                    {
                        walked = step.Duration > TimeSpan.Zero
                            ? step.From + (step.To - step.From) * (elapsed / step.Duration)
                            : step.To;
                        break;
                    }

                    elapsed -= step.Duration;
                }

                var apart = Math.Abs(RingMotion.At(current, target, share, forward) - walked);
                return apart < 1e-6 || (forward && Math.Abs(apart - RingMotion.Full) < 1e-6);
            });

    /// <summary>The top of the minute, as an example: from :59 to :00 the seconds ring goes on to full in the whole of
    /// the time, and starts again from nothing — never back round.</summary>
    [Fact]
    public void At_the_top_of_the_minute_the_seconds_go_on_round()
    {
        RingMotion.Segment[] expected =
        [
            new(59 / 60d, RingMotion.Full, TimeSpan.FromMilliseconds(975)),
            new(0, 0, TimeSpan.Zero),
        ];

        Assert.Equal(expected, RingMotion.Plan(59 / 60d, 0, TimeSpan.FromMilliseconds(975), forwardOnly: true));
    }

    /// <summary>A target that differs from the ring only in the last bit is the same place, not a turn away.</summary>
    [Fact]
    public void A_slip_of_the_last_bit_is_not_a_whole_turn()
    {
        var plan = RingMotion.Plan(0.1 + 0.2, 0.3, TimeSpan.FromSeconds(1), forwardOnly: true);
        Assert.Single(plan);
    }

    /// <summary>Any arc's two ends lie on its circle — wherever it starts, however far it sweeps, and for values
    /// outside the ring too, which are drawn at the nearer end.</summary>
    [Property(MaxTest = 1000)]
    public Property An_arcs_ends_lie_on_its_circle() =>
        Prop.ForAll(
            (from x in Gen.Choose(-1000, 1000)
             from y in Gen.Choose(-1000, 1000)
             from radius in Gen.Choose(1, 100_000)
             from value in Gen.Choose(-500, 1500)
             from start in Gen.Choose(-720, 720)
             from sweep in Gen.Choose(-90, 450)
             select (Centre: new Point(x, y), Radius: radius / 100d, Value: value / 1000d, Start: (double)start,
                 Sweep: (double)sweep))
            .ToArbitrary(),
            arc =>
            {
                var (start, end) = ArcGeometry.Ends(arc.Centre, arc.Radius, arc.Value, arc.Start, arc.Sweep);
                return OnCircle(start) && OnCircle(end);

                bool OnCircle(Point point) =>
                    Math.Abs((point - arc.Centre).Length - arc.Radius) <= 1e-9 * Math.Max(1, arc.Radius);
            });

    /// <summary>The more the value, the further round the arc reaches — never less, whatever the start or sweep.
    /// </summary>
    [Property(MaxTest = 1000)]
    public Property The_angle_rises_with_the_value() =>
        Prop.ForAll(
            (from a in Gen.Choose(-500, 1500)
             from b in Gen.Choose(-500, 1500)
             from start in Gen.Choose(-720, 720)
             from sweep in Gen.Choose(-90, 450)
             select (Low: Math.Min(a, b) / 1000d, High: Math.Max(a, b) / 1000d, Start: (double)start,
                 Sweep: (double)sweep))
            .ToArbitrary(),
            arc => ArcGeometry.AngleAt(arc.Low, arc.Start, arc.Sweep) <= ArcGeometry.AngleAt(arc.High, arc.Start, arc.Sweep));

    /// <summary>What a value draws: nothing below the least worth drawing — not even a dot — an arc above it, and a
    /// whole circle only at a full turn. A dial's full sweep is an arc, because its ends are apart.</summary>
    [Theory]
    [InlineData(0, 360, ArcShape.None)]
    [InlineData(0.0009, 360, ArcShape.None)]
    [InlineData(double.NaN, 360, ArcShape.None)]
    [InlineData(0.5, 0, ArcShape.None)]
    [InlineData(0.001, 360, ArcShape.Arc)]
    [InlineData(0.99, 360, ArcShape.Arc)]
    [InlineData(1, 360, ArcShape.Circle)]
    [InlineData(1.5, 360, ArcShape.Circle)]
    [InlineData(1, 270, ArcShape.Arc)]
    public void What_a_value_draws(double value, double sweep, ArcShape shape) =>
        Assert.Equal(shape, ArcGeometry.ShapeOf(value, sweep));
}
