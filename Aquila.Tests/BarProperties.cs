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

    /// <summary>A look scaled keeps its track and scales its size — the bar under a number, at two thirds.</summary>
    [Fact]
    public void A_scaled_look_keeps_its_track()
    {
        var look = new BarLook(6, 3, null).Scaled(2.0 / 3);
        Assert.Equal(4, look.Thickness, 9);
        Assert.Equal(2, look.Corner, 9);
    }
}
