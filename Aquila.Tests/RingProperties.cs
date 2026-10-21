using System.Windows;
using System.Windows.Media;
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

    /// <summary>A ring and a room to fit it in: anywhere on the canvas, from a sliver to past any screen, with a stroke
    /// up to a quarter of the shorter side — so there is always room for some ring — any start, any sweep from a
    /// tenth of a degree to past a whole turn, every kind of end, and a box at the centre — often none, otherwise up
    /// to half the room each way, so there is always room for it too.</summary>
    private static Arbitrary<(Rect Room, double Thickness, double Start, double Sweep, PenLineCap Caps, Size Middle)>
        Fittings() =>
        (from x in Gen.Choose(-1000, 1000)
         from y in Gen.Choose(-1000, 1000)
         from width in Gen.Choose(1, 400_000)
         from height in Gen.Choose(1, 400_000)
         from stroke in Gen.Choose(0, 1000)
         from start in Gen.Choose(-7200, 7200)
         from sweep in Gen.OneOf(Gen.Choose(1, 4500).Select(s => s / 10d), Gen.Elements(90d, 180d, 270d, 360d))
         from caps in Gen.Elements(PenLineCap.Flat, PenLineCap.Round, PenLineCap.Square, PenLineCap.Triangle)
         from across in Gen.OneOf(Gen.Constant(0), Gen.Choose(0, 500))
         from down in Gen.OneOf(Gen.Constant(0), Gen.Choose(0, 500))
         let room = new Rect(x, y, width / 100d, height / 100d)
         select (room, Math.Min(room.Width, room.Height) / 4 * stroke / 1000, start / 10d, sweep, caps,
             new Size(room.Width * across / 1000, room.Height * down / 1000)))
        .ToArbitrary();

    /// <summary>
    /// A ring fitted by its sweep lies inside its room — stroke, ends and the box at its centre included — and fills
    /// it on at least one axis: as large as the room allows, for any start, sweep, size, thickness, end and box.
    ///
    /// Checked against the drawing itself, not the sum that placed it: the stroke's two edges are walked along the
    /// whole sweep, and each end is added as the pen draws it — a disc for a round end, the far corners for a square
    /// one, the point for a triangle — so a mistake in the placement cannot hide behind the same mistake in the check.
    /// A whole turn is a circle, and has no ends. The walk is fine enough that the room it can miss an edge by is a
    /// ten-thousandth of the room.
    /// </summary>
    [Property(MaxTest = 1000)]
    public Property A_fitted_arc_lies_inside_its_room_and_fills_it() =>
        Prop.ForAll(Fittings(), fitting =>
        {
            var (room, thickness, start, sweepAngle, caps, middle) = fitting;
            var (centre, radius) = ArcGeometry.Fit(room, thickness, start, sweepAngle, caps, middle);
            if (!(radius > 0)) return false;

            var drawn = Drawn(centre, radius, thickness, start, ArcGeometry.Sweep(sweepAngle), caps);
            // No box is no box — not a point at the centre, which a quarter arc's box need not contain.
            if (middle.Width > 0 || middle.Height > 0)
                drawn.Union(new Rect(centre.X - middle.Width / 2, centre.Y - middle.Height / 2, middle.Width,
                    middle.Height));

            var size = Math.Max(room.Width, room.Height);
            var slack = 1e-9 * (radius + Math.Abs(centre.X) + Math.Abs(centre.Y) + size);
            var miss = 1e-4 * size;

            var inside = drawn.Left >= room.Left - slack && drawn.Right <= room.Right + slack
                && drawn.Top >= room.Top - slack && drawn.Bottom <= room.Bottom + slack;
            var fills = drawn.Width >= room.Width - miss || drawn.Height >= room.Height - miss;

            return inside && fills;
        });

    /// <summary>The box round what a pen draws for a ring's whole sweep, walked point by point.</summary>
    private static Rect Drawn(Point centre, double radius, double thickness, double start, double sweep, PenLineCap caps)
    {
        var half = thickness / 2;
        var points = new List<Point>();

        const int Steps = 4000;
        for (var step = 0; step <= Steps; step++)
        {
            var angle = start + sweep * step / Steps;
            points.Add(ArcGeometry.PointAt(centre, radius + half, angle));
            points.Add(ArcGeometry.PointAt(centre, radius - half, angle));
        }

        if (sweep < 360)
            foreach (var (angle, onward) in new[] { (start, -1d), (start + sweep, 1d) })
            {
                var end = ArcGeometry.PointAt(centre, radius, angle);
                var radians = angle * Math.PI / 180;
                var outward = new Vector(Math.Cos(radians), Math.Sin(radians)) * half;
                var past = new Vector(-Math.Sin(radians), Math.Cos(radians)) * (onward * half);

                switch (caps)
                {
                    case PenLineCap.Round:
                        for (var degree = 0; degree < 360; degree++) points.Add(ArcGeometry.PointAt(end, half, degree));
                        break;

                    case PenLineCap.Square:
                        points.Add(end + outward + past);
                        points.Add(end - outward + past);
                        break;

                    case PenLineCap.Triangle:
                        points.Add(end + past);
                        break;
                }
            }

        return new Rect(new Point(points.Min(p => p.X), points.Min(p => p.Y)),
            new Point(points.Max(p => p.X), points.Max(p => p.Y)));
    }

    /// <summary>
    /// A dial in a square box: the circle sits low, because the open bottom has nothing in it. Fitted by its sweep, the
    /// arc reaches both sides and the top, and the empty quarter below is split evenly above and below the figure
    /// instead of being kept inside it — which, centred on its circle, it was.
    /// </summary>
    [Fact]
    public void A_dial_in_a_square_box_fills_its_width_and_sits_low()
    {
        var room = new Rect(0, 0, 200, 200);
        var (centre, radius) = ArcGeometry.Fit(room, 14, -225, 270, PenLineCap.Flat);

        // Both sides: the outer edge at three and at nine o'clock is the box's width.
        Assert.Equal(93, radius, precision: 9);
        Assert.Equal(100, centre.X, precision: 9);

        // Below the middle, by half of what the open bottom leaves.
        var drawn = ArcGeometry.Bounds(radius, 14, -225, 270, PenLineCap.Flat);
        Assert.Equal((200 - drawn.Height) / 2 + 100, centre.Y, precision: 9);
        Assert.True(centre.Y > 110);
    }

    /// <summary>A half dial's centre is the bottom edge of its arc. In a wide box, fitted by the arc alone, a number
    /// there hangs half out of the box; fitted with the number's line, the arc gives up enough to keep it in.</summary>
    [Fact]
    public void A_half_dial_keeps_its_number_inside_a_wide_box()
    {
        var room = new Rect(0, 0, 300, 100);
        var line = new Size(0, 30);

        var (alone, _) = ArcGeometry.Fit(room, 14, -180, 180, PenLineCap.Flat);
        Assert.True(alone.Y + line.Height / 2 > room.Bottom);

        var (kept, _) = ArcGeometry.Fit(room, 14, -180, 180, PenLineCap.Flat, line);
        Assert.Equal(room.Bottom, kept.Y + line.Height / 2, precision: 9);
    }

    /// <summary>The default is the ring as it always was — its whole circle, centred, whatever it sweeps — so a clock's
    /// rings are where they were before a ring could be fitted any other way.</summary>
    [Theory]
    [InlineData(360d)]
    [InlineData(270d)]
    [InlineData(10d)]
    public void By_default_a_ring_is_its_circle_centred(double sweep)
    {
        var (centre, radius) = ArcGeometry.Place(new Size(200, 120), 10, -225, sweep, PenLineCap.Round, RingFit.Circle);

        Assert.Equal(new Point(100, 60), centre);
        Assert.Equal(ArcGeometry.Radius(200, 120, 10), radius);
    }

    /// <summary>A stroke wider than the room has no ring that fits, and is not drawn rather than drawn past its box.
    /// </summary>
    [Fact]
    public void A_stroke_wider_than_its_room_has_no_room()
    {
        var (_, radius) = ArcGeometry.Fit(new Rect(0, 0, 10, 200), 12, -225, 270, PenLineCap.Round);
        Assert.Equal(0, radius);
    }
}
