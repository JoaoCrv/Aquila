using System.Windows;
using System.Windows.Controls;
using Aquila.Controls.Primitives;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Xunit;

namespace Aquila.Tests;

/// <summary>
/// A bar (<see cref="BarGeometry"/>). A fill past its track, a corner rounder than the bar, a nub of a reading drawn as
/// nothing — each is a sum, held for any bar, lying down or standing up.
/// </summary>
public sealed class BarProperties
{
    private const double Tolerance = 1e-9;

    /// <summary>A fraction: anywhere, with the ends, the least share and what an animation or a bad sum can hand over.
    /// </summary>
    private static Gen<double> Fractions() =>
        Gen.Frequency(
            (6, Gen.Choose(-200_000, 1_200_000).Select(v => v / 1_000_000d)),
            (2, Gen.Elements(0d, 1d, BarGeometry.Least, BarGeometry.Least / 2, double.NaN, double.PositiveInfinity)));

    private static Gen<Rect> Tracks() =>
        from x in Gen.Choose(-100, 100)
        from y in Gen.Choose(-100, 100)
        from width in Gen.Choose(0, 500)
        from height in Gen.Choose(0, 500)
        select new Rect(x, y, width, height);

    private static Gen<Orientation> Orientations() => Gen.Elements(Orientation.Horizontal, Orientation.Vertical);

    private static Arbitrary<(Rect Track, double Fraction, Orientation Orientation)> Bars() =>
        (from track in Tracks()
         from fraction in Fractions()
         from orientation in Orientations()
         select (track, fraction, orientation)).ToArbitrary();

    /// <summary>The share drawn is 0 to 1 whatever is handed over, and a NaN is nothing.</summary>
    [Property(MaxTest = 500)]
    public Property A_share_is_held_to_the_track() =>
        Prop.ForAll(Fractions().ToArbitrary(), fraction =>
        {
            var share = BarGeometry.Share(fraction);
            return share is >= 0 and <= 1 && (!double.IsNaN(fraction) || share == 0);
        });

    /// <summary>A bar is never shown shorter than <see cref="BarGeometry.Least"/> — nought included, so a reading at
    /// rest still shows the end of a bar — and from there up a reading is shown as it is.</summary>
    [Property(MaxTest = 500)]
    public Property A_bar_is_never_shown_shorter_than_the_least() =>
        Prop.ForAll(Fractions().ToArbitrary(), fraction =>
        {
            var (share, shown) = (BarGeometry.Share(fraction), BarGeometry.Shown(fraction));
            return share < BarGeometry.Least ? shown == BarGeometry.Least : shown == share;
        });

    /// <summary>The filled part lies inside its track, starts where the track starts — the left lying down, the bottom
    /// standing up — and is the share of its length, never of its thickness.</summary>
    [Property(MaxTest = 1000)]
    public Property The_fill_starts_where_the_track_does_and_stays_in_it() =>
        Prop.ForAll(Bars(), bar =>
        {
            var (track, fraction, orientation) = bar;
            var filled = BarGeometry.Filled(track, fraction, orientation);
            var share = BarGeometry.Share(fraction);

            var inside = filled.Left >= track.Left - Tolerance && filled.Right <= track.Right + Tolerance
                && filled.Top >= track.Top - Tolerance && filled.Bottom <= track.Bottom + Tolerance;

            return inside && (orientation == Orientation.Horizontal
                ? filled.Left == track.Left && filled.Height == track.Height
                    && Math.Abs(filled.Width - track.Width * share) < 1e-6
                : Math.Abs(filled.Bottom - track.Bottom) < 1e-6 && filled.Width == track.Width
                    && Math.Abs(filled.Height - track.Height * share) < 1e-6);
        });

    /// <summary>A corner is never rounder than half the part's shorter side, and never less than nothing — so a nearly
    /// empty bar ends in a dot, not in a shape wider than it measures.</summary>
    [Property(MaxTest = 1000)]
    public Property A_corner_is_never_rounder_than_its_part() =>
        Prop.ForAll(Tracks().ToArbitrary(), Gen.OneOf(Gen.Choose(-10, 300).Select(c => (double)c),
            Gen.Elements(double.NaN, double.PositiveInfinity)).ToArbitrary(), (part, corner) =>
        {
            var round = BarGeometry.Corner(corner, part);
            return round >= 0 && round <= Math.Min(part.Width, part.Height) / 2 + Tolerance
                && (!(corner > 0) || round == Math.Min(corner, Math.Min(part.Width, part.Height) / 2));
        });

    /// <summary>A mark crosses the track at its share of the length, counted from where the fill starts, and reaches
    /// as far past it on either side.</summary>
    [Property(MaxTest = 500)]
    public Property A_mark_crosses_at_its_share() =>
        Prop.ForAll(Bars(), Gen.Choose(0, 20).ToArbitrary(), (bar, reach) =>
        {
            var (track, fraction, orientation) = bar;
            var mark = BarGeometry.Share(fraction);
            var (from, to) = BarGeometry.Mark(track, mark, reach, orientation);
            var tip = BarGeometry.Filled(track, mark, orientation);

            return orientation == Orientation.Horizontal
                ? from.X == to.X && Math.Abs(from.X - tip.Right) < 1e-6
                    && from.Y == track.Top - reach && to.Y == track.Bottom + reach
                : from.Y == to.Y && Math.Abs(from.Y - tip.Top) < 1e-6
                    && from.X == track.Left - reach && to.X == track.Right + reach;
        });

    /// <summary>A track sits across the middle of its room, so a bar given exactly its thickness and its marks' reach
    /// lies between the marks' ends.</summary>
    [Fact]
    public void A_track_is_centred_across_its_room()
    {
        Assert.Equal(new Rect(0, 3, 120, 4), BarGeometry.Track(new Size(120, 10), 4, Orientation.Horizontal));
        Assert.Equal(new Rect(8, 0, 4, 90), BarGeometry.Track(new Size(20, 90), 4, Orientation.Vertical));
    }

    /// <summary>
    /// A bar's parts lie end to end from where it fills, each its share of the length, never outside the track — and
    /// they stop where the track does, so shares adding up to more than the whole never draw past it. A share below
    /// nothing is nothing.
    /// </summary>
    [Property(MaxTest = 500)]
    public Property Segments_lie_end_to_end_inside_the_track() =>
        Prop.ForAll(
            (from track in Tracks()
             from orientation in Orientations()
             from count in Gen.Choose(1, 6)
             from shares in Gen.Choose(-100, 700).Select(v => v / 1000d).ArrayOf(count)
             select (track, orientation, shares)).ToArbitrary(),
            bar =>
            {
                var (track, orientation, shares) = bar;
                var parts = BarGeometry.Segments(track, shares, orientation);
                var horizontal = orientation == Orientation.Horizontal;
                var length = horizontal ? track.Width : track.Height;

                var at = 0d;
                for (var i = 0; i < parts.Length; i++)
                {
                    var part = parts[i];
                    var size = horizontal ? part.Width : part.Height;
                    var start = horizontal ? part.Left - track.Left : track.Bottom - part.Bottom;
                    var want = Math.Min(at + Math.Max(0, shares[i]) * length, length) - Math.Min(at, length);

                    if (Math.Abs(start - Math.Min(at, length)) > 1e-6 || Math.Abs(size - want) > 1e-6) return false;
                    if (part.Left < track.Left - Tolerance || part.Right > track.Right + Tolerance
                        || part.Top < track.Top - Tolerance || part.Bottom > track.Bottom + Tolerance) return false;

                    at += Math.Max(0, shares[i]) * length;
                }

                return true;
            });

    /// <summary>A bar whose thickness is NaN is as thick as its room — a core's bar, as wide as its column.</summary>
    [Fact]
    public void A_bar_without_a_thickness_takes_its_room()
    {
        Assert.Equal(new Rect(0, 0, 30, 92), BarGeometry.Track(new Size(30, 92), double.NaN, Orientation.Vertical));
        Assert.Equal(new Rect(0, 0, 200, 8), BarGeometry.Track(new Size(200, 8), double.NaN, Orientation.Horizontal));
    }

    /// <summary>A look scaled keeps its track and scales its size — the bar under a number, at two thirds — its blocks
    /// included.</summary>
    [Fact]
    public void A_scaled_look_keeps_its_track()
    {
        var look = new BarLook(6, 3, null, Block: 6, BlockGap: 3, Fade: true).Scaled(2.0 / 3);
        Assert.Equal(4, look.Thickness, 9);
        Assert.Equal(2, look.Corner, 9);
        Assert.Equal((4d, 2d), (Math.Round(look.Block, 9), Math.Round(look.BlockGap, 9)));
        Assert.True(look.Fade);
        Assert.Equal((0d, false), (BarLook.Default.Block, BarLook.Default.Fade));
    }

    // ── Bands, blocks and the peak (docs/widget-barras) ─────────────────────

    private static double Length(Rect track, Orientation orientation) =>
        orientation == Orientation.Horizontal ? track.Width : track.Height;

    /// <summary>How far along the track a point is, from where the bar fills.</summary>
    private static double Along(Rect track, Rect part, Orientation orientation, bool end = false) =>
        orientation == Orientation.Horizontal
            ? (end ? part.Right : part.Left) - track.Left
            : track.Bottom - (end ? part.Top : part.Bottom);

    private static bool Inside(Rect part, Rect track) =>
        part.Left >= track.Left - 1e-6 && part.Right <= track.Right + 1e-6
        && part.Top >= track.Top - 1e-6 && part.Bottom <= track.Bottom + 1e-6;

    /// <summary>
    /// Blocks cover the track and never pass it: the first starts where the bar fills and the last ends where the track
    /// does, all the same length — never shorter than asked, unless the track itself is — with the same gap between
    /// each. A block of nought is a bar drawn whole: no blocks.
    /// </summary>
    [Property(MaxTest = 1000)]
    public Property Blocks_cover_the_track_without_passing_it() =>
        Prop.ForAll(
            (from track in Tracks()
             from orientation in Orientations()
             from block in Gen.OneOf(Gen.Choose(0, 400).Select(v => v / 10d), Gen.Elements(-1d, double.NaN))
             from gap in Gen.OneOf(Gen.Choose(0, 100).Select(v => v / 10d), Gen.Elements(-2d, double.NaN))
             select (track, orientation, block, gap)).ToArbitrary(),
            bar =>
            {
                var (track, orientation, block, gap) = bar;
                var blocks = BarGeometry.Blocks(track, block, gap, orientation);
                var length = Length(track, orientation);

                if (!(block > 0) || !(length > 0)) return blocks.Length == 0;
                if (blocks.Length == 0) return false;

                var space = double.IsFinite(gap) ? Math.Max(0, gap) : 0;
                var size = Length(blocks[0], orientation);
                var asked = Math.Max(block, BarGeometry.LeastBlock);

                for (var i = 0; i < blocks.Length; i++)
                {
                    if (!Inside(blocks[i], track)) return false;
                    if (Math.Abs(Length(blocks[i], orientation) - size) > 1e-6) return false;
                    if (Math.Abs(Along(track, blocks[i], orientation) - i * (size + space)) > 1e-6) return false;
                }

                return Math.Abs(Along(track, blocks[0], orientation)) < 1e-6
                    && Math.Abs(Along(track, blocks[^1], orientation, end: true) - length) < 1e-6
                    && (size >= asked - 1e-6 || blocks.Length == 1 && Math.Abs(size - length) < 1e-6);
            });

    /// <summary>A reading lights exactly the blocks whose middle it has passed: each stands for an equal share of the
    /// scale, so the bar is read to the nearest block. (Readings within a hair of a middle are left out: which side of
    /// it a sum lands on is the arithmetic's, not the rule's.)</summary>
    [Property(MaxTest = 1000)]
    public Property A_block_lights_once_the_reading_passes_its_middle() =>
        Prop.ForAll(Gen.Choose(0, 60).ToArbitrary(), Fractions().ToArbitrary(), (count, fraction) =>
        {
            var share = BarGeometry.Share(fraction);
            var near = Enumerable.Range(0, count).Any(i => Math.Abs(share * count - (i + 0.5)) < 1e-9);
            var middles = Enumerable.Range(0, count).Count(i => (i + 0.5) / count <= share);

            return near || BarGeometry.Lit(count, fraction) == middles;
        });

    /// <summary>
    /// A band starts at its limit and stops where the bar stops: each part runs from its start, held to the track, to
    /// the end of the filled part — and is nothing there for a band the bar has not reached. So, painted in order, each
    /// stretch of the bar is the colour of the highest band it has passed.
    /// </summary>
    [Property(MaxTest = 1000)]
    public Property Bands_start_at_their_limits_and_stop_where_the_bar_does() =>
        Prop.ForAll(
            (from bar in Bars().Generator
             from count in Gen.Choose(1, 4)
             from froms in Gen.OneOf(Gen.Choose(-500, 1500).Select(v => v / 1000d), Gen.Constant(double.NegativeInfinity))
                .ArrayOf(count)
             select (bar, froms.OrderBy(f => f).ToArray())).ToArbitrary(),
            banded =>
            {
                var ((track, fraction, orientation), froms) = banded;
                var parts = BarGeometry.Banded(track, fraction, froms, orientation);
                var filled = BarGeometry.Filled(track, fraction, orientation);
                var length = Length(track, orientation);
                var end = BarGeometry.Share(fraction) * length;

                return parts.Length == froms.Length && parts.Select((part, i) =>
                {
                    var start = Math.Min(Math.Clamp(froms[i], 0, 1) * length, end);
                    return Inside(part, filled)
                        && Math.Abs(Along(track, part, orientation) - start) < 1e-6
                        && Math.Abs(Along(track, part, orientation, end: true) - end) < 1e-6;
                }).All(ok => ok);
            });

    /// <summary>A stretch of the bar is the colour of the highest band whose start it has reached; below the first, of
    /// none.</summary>
    [Fact]
    public void A_share_takes_the_highest_band_it_has_reached()
    {
        double[] froms = [double.NegativeInfinity, 0.5, 0.75, 0.9];

        Assert.Equal(0, BarGeometry.BandAt(froms, 0.1));
        Assert.Equal(1, BarGeometry.BandAt(froms, 0.5));
        Assert.Equal(2, BarGeometry.BandAt(froms, 0.8));
        Assert.Equal(3, BarGeometry.BandAt(froms, 1));
        Assert.Equal(-1, BarGeometry.BandAt([0.5], 0.2));
    }

    /// <summary>
    /// The peak's line crosses the bar at its share, reaching as far past it as the marks do, a third of the bar's
    /// thickness along it — never less than <see cref="BarGeometry.LeastPeak"/>, never more than the track — and is kept
    /// inside the track's length at either end.
    /// </summary>
    [Property(MaxTest = 1000)]
    public Property The_peak_crosses_the_bar_at_its_share() =>
        Prop.ForAll(Bars(), Gen.Choose(0, 20).ToArbitrary(), (bar, reach) =>
        {
            var (track, peak, orientation) = bar;
            var line = BarGeometry.PeakLine(track, peak, reach, orientation);
            var horizontal = orientation == Orientation.Horizontal;
            var length = Length(track, orientation);
            var across = horizontal ? track.Height : track.Width;
            var along = Math.Min(length, Math.Max(BarGeometry.LeastPeak, across / 3));
            var centre = Along(track, line, orientation) + along / 2;
            var wanted = Math.Clamp(BarGeometry.Share(peak) * length, along / 2, length - along / 2);

            var reaches = horizontal
                ? line.Top == track.Top - reach && Math.Abs(line.Height - (across + 2 * reach)) < 1e-6
                : line.Left == track.Left - reach && Math.Abs(line.Width - (across + 2 * reach)) < 1e-6;

            return reaches
                && Math.Abs(Length(line, orientation) - along) < 1e-6
                && Math.Abs(centre - wanted) < 1e-6
                && Along(track, line, orientation) >= -1e-6
                && Along(track, line, orientation, end: true) <= length + 1e-6;
        });

    /// <summary>A block keeps its corners small — a quarter of its shorter side at most — so a preset's round bar ends do
    /// not turn its blocks into dots; a smaller corner is kept as it is.</summary>
    [Fact]
    public void A_block_s_corners_stay_small()
    {
        Assert.Equal(1.25, BarGeometry.BlockCorner(3, new Rect(0, 0, 14, 5)));
        Assert.Equal(0.5, BarGeometry.BlockCorner(0.5, new Rect(0, 0, 14, 5)));
        Assert.Equal(0, BarGeometry.BlockCorner(double.NaN, new Rect(0, 0, 14, 5)));
    }

    /// <summary>In blocks, the peak lights the highest block it would light as a reading — none below the first.
    /// </summary>
    [Fact]
    public void A_peak_in_blocks_lights_the_block_it_would_light_as_a_reading()
    {
        Assert.Equal(2, BarGeometry.PeakBlock(10, 0.33));
        Assert.Equal(9, BarGeometry.PeakBlock(10, 1));
        Assert.Equal(-1, BarGeometry.PeakBlock(10, 0.02));
        Assert.Equal(-1, BarGeometry.PeakBlock(10, double.NaN));
    }
}
