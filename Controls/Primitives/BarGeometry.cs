using System.Windows;
using System.Windows.Controls;

namespace Aquila.Controls.Primitives;

/// <summary>
/// Where a bar's parts go — the track, the filled part, its corners and the marks across it — kept apart from the
/// drawing, as <see cref="ArcGeometry"/> is for the ring, so each rule can be held without rendering anything.
///
/// A fraction is a share of the track, 0 to 1. Anything outside is drawn at the nearer end, and a NaN as nothing: it
/// comes from a reading or an animation, and a bar is the wrong place to discover that either went wrong.
///
/// A bar lying down fills from the left; one standing up fills from the bottom.
/// </summary>
public static class BarGeometry
{
    /// <summary>
    /// The least share a bar is shown at.
    ///
    /// A bar at its true length shows nothing for a reading at rest, and a bare track reads as a bar that is not
    /// working. At a fiftieth of the track there is always the end of a bar to see — the list's rows and the Number's
    /// bar were drawn so, each with its own copy of the number, before this was their one home.
    /// </summary>
    public const double Least = 0.02;

    /// <summary>The fraction as a share the drawing can use: 0 to 1, and 0 for a NaN.</summary>
    public static double Share(double fraction) => double.IsNaN(fraction) ? 0 : Math.Clamp(fraction, 0, 1);

    /// <summary>The share a reading is shown at: its own, but never under <see cref="Least"/>.</summary>
    public static double Shown(double fraction) => Math.Max(Share(fraction), Least);

    /// <summary>
    /// The track in a room: the whole length, <paramref name="thickness"/> across, centred across the room — or the
    /// whole room across for a thickness of NaN, a bar as thick as it is given (a CPU card's core, as wide as its
    /// column).
    ///
    /// Centred rather than at the start, so a bar given more room than it needs — a column of Bars wider than its bar —
    /// sits in the middle of it, and one given exactly its thickness and its marks' reach either side sits between them.
    /// </summary>
    public static Rect Track(Size room, double thickness, Orientation orientation)
    {
        var width = Math.Max(0, room.Width);
        var height = Math.Max(0, room.Height);
        var across = double.IsNaN(thickness)
            ? (orientation == Orientation.Horizontal ? height : width)
            : Math.Max(0, thickness);

        return orientation == Orientation.Horizontal
            ? new Rect(0, (height - across) / 2, width, across)
            : new Rect((width - across) / 2, 0, across, height);
    }

    /// <summary>The filled part of a track: from the left lying down, from the bottom standing up, the
    /// <see cref="Share"/> of its length.</summary>
    public static Rect Filled(Rect track, double fraction, Orientation orientation)
    {
        var share = Share(fraction);

        return orientation == Orientation.Horizontal
            ? new Rect(track.X, track.Y, track.Width * share, track.Height)
            : new Rect(track.X, track.Bottom - track.Height * share, track.Width, track.Height * share);
    }

    /// <summary>
    /// The radius a part's ends are drawn with: the corner asked for, but never more than half the part's shorter side.
    ///
    /// Past half the thickness a corner has nothing left to round; past half the length — a nearly empty bar — the two
    /// ends would round past each other and draw a shape wider than what it measures. Held, it ends in a dot.
    /// </summary>
    public static double Corner(double corner, Rect part)
    {
        if (!(corner > 0) || part.IsEmpty) return 0;
        return Math.Min(corner, Math.Min(part.Width, part.Height) / 2);
    }

    /// <summary>
    /// A bar split into consecutive parts, one per share, from where the bar fills: each the share of the track's
    /// length, laid end to end and held to the track — a share below nothing is nothing, and the parts stop where the
    /// track does, so shares that add up to more than the whole never draw past it.
    /// </summary>
    public static Rect[] Segments(Rect track, IReadOnlyList<double> shares, Orientation orientation)
    {
        var parts = new Rect[shares.Count];
        var horizontal = orientation == Orientation.Horizontal;
        var length = horizontal ? track.Width : track.Height;
        var done = 0d;

        for (var i = 0; i < parts.Length; i++)
        {
            var share = double.IsNaN(shares[i]) ? 0 : Math.Max(0, shares[i]);
            var from = Math.Min(done, length);
            var to = Math.Min(done + share * length, length);
            done += share * length;

            parts[i] = horizontal
                ? new Rect(track.X + from, track.Y, to - from, track.Height)
                : new Rect(track.X, track.Bottom - to, track.Width, to - from);
        }

        return parts;
    }

    /// <summary>
    /// How strong a fading bar is where it starts (<see cref="BarLook.Fade"/>); its tip has its whole colour. A quarter
    /// and not nothing: faded to nothing, a short bar would seem to start halfway along, and a reading at rest would
    /// show no bar at all — what <see cref="Least"/> exists to prevent.
    /// </summary>
    public const double FadeFrom = 0.25;

    // ── Bands ────────────────────────────────────────────────────────────

    /// <summary>
    /// The filled part split where its colour changes — a VU meter's bar. Each band starts at a share of the track,
    /// counted from where the bar fills, and paints from there to the end of the filled part: in ascending order, as a
    /// reading's limits are, each later band lies over the one before, so the joins have no seam between two edges.
    ///
    /// One part per band, in the bands' order: from its start, held to the track, to the end of the
    /// <see cref="Filled"/> part — and nothing, at that end, for a band the bar has not reached.
    /// </summary>
    public static Rect[] Banded(Rect track, double fraction, IReadOnlyList<double> froms, Orientation orientation)
    {
        var parts = new Rect[froms.Count];
        var horizontal = orientation == Orientation.Horizontal;
        var length = horizontal ? track.Width : track.Height;
        var end = Share(fraction) * length;

        for (var i = 0; i < parts.Length; i++)
        {
            var from = Math.Min(Math.Clamp(double.IsNaN(froms[i]) ? 0 : froms[i], 0, 1) * length, end);

            parts[i] = horizontal
                ? new Rect(track.X + from, track.Y, end - from, track.Height)
                : new Rect(track.X, track.Bottom - end, track.Width, end - from);
        }

        return parts;
    }

    /// <summary>The band a share of the track falls in: the last whose start it has reached — the highest limit a
    /// reading has passed. -1 below the first.</summary>
    public static int BandAt(IReadOnlyList<double> froms, double share)
    {
        var at = -1;
        for (var i = 0; i < froms.Count; i++)
            if (froms[i] <= share)
                at = i;

        return at;
    }

    // ── Blocks ───────────────────────────────────────────────────────────

    /// <summary>The shortest a block is drawn: a pixel. Shorter, a track would be thousands of blocks, and no screen
    /// shows one.</summary>
    public const double LeastBlock = 1;

    /// <summary>
    /// A track as blocks — an LED meter's — laid from where the bar fills, as many as fit at
    /// <paramref name="block"/> long with <paramref name="gap"/> between them, and each stretched by the same amount so
    /// the first starts where the track does and the last ends where it ends: the top of the track is the top of the
    /// scale, blocks or not. None for a block of nought — a bar drawn whole.
    /// </summary>
    public static Rect[] Blocks(Rect track, double block, double gap, Orientation orientation)
    {
        var horizontal = orientation == Orientation.Horizontal;
        var length = horizontal ? track.Width : track.Height;
        if (!(block > 0) || !(length > 0)) return [];

        var size = Math.Max(block, LeastBlock);
        var space = double.IsFinite(gap) ? Math.Max(0, gap) : 0;
        var count = (int)Math.Max(1, Math.Floor((length + space) / (size + space)));
        var each = (length - (count - 1) * space) / count;

        var blocks = new Rect[count];
        for (var i = 0; i < count; i++)
        {
            var from = i * (each + space);
            blocks[i] = horizontal
                ? new Rect(track.X + from, track.Y, each, track.Height)
                : new Rect(track.X, track.Bottom - from - each, track.Width, each);
        }

        return blocks;
    }

    /// <summary>
    /// A block's corners: the look's, but never rounder than a quarter of the block's shorter side. A preset's corner is
    /// meant for a whole bar — Ember's 3 on a 6 px bar ends it in a half circle — and on a 5 px block the same corner
    /// turns every block into a dot and the meter into a dotted line.
    /// </summary>
    public static double BlockCorner(double corner, Rect block) =>
        Math.Min(Corner(corner, block), block.IsEmpty ? 0 : Math.Min(block.Width, block.Height) / 4);

    /// <summary>
    /// How many of <paramref name="count"/> blocks a reading lights, from where the bar fills. Each block stands for an
    /// equal share of the scale and lights once the reading passes the middle of it — so a reading is shown to the
    /// nearest block, and a block never lights for a reading that has barely entered it.
    /// </summary>
    public static int Lit(int count, double fraction) =>
        count <= 0 ? 0 : (int)Math.Clamp(Math.Floor(Share(fraction) * count + 0.5), 0, count);

    // ── The peak ─────────────────────────────────────────────────────────

    /// <summary>The least a peak's line is drawn along the bar: thinner, it is lost against the track's edge.</summary>
    public const double LeastPeak = 1.5;

    /// <summary>
    /// The line that holds a bar's highest recent reading: across the bar, reaching <paramref name="reach"/> past it
    /// either side as the marks do, and a third of the bar's thickness along it — never less than
    /// <see cref="LeastPeak"/>, never more than the track — centred on the peak's share and kept inside the track at
    /// its ends.
    /// </summary>
    public static Rect PeakLine(Rect track, double peak, double reach, Orientation orientation)
    {
        var horizontal = orientation == Orientation.Horizontal;
        var length = Math.Max(0, horizontal ? track.Width : track.Height);
        var across = Math.Max(0, horizontal ? track.Height : track.Width);
        var past = double.IsFinite(reach) ? Math.Max(0, reach) : 0;

        var along = Math.Min(length, Math.Max(LeastPeak, across / 3));
        var from = Math.Clamp(Share(peak) * length - along / 2, 0, length - along);

        return horizontal
            ? new Rect(track.X + from, track.Top - past, along, across + 2 * past)
            : new Rect(track.Left - past, track.Bottom - from - along, across + 2 * past, along);
    }

    /// <summary>The block a peak lights among <paramref name="count"/>: the highest it would light as a reading, -1
    /// for none.</summary>
    public static int PeakBlock(int count, double peak) => Lit(count, peak) - 1;

    /// <summary>Whether a mark falls on the track at all.</summary>
    public static bool IsMark(double mark) => mark is >= 0 and <= 1;

    /// <summary>A mark across the track at a share of its length — counted from where the fill starts — reaching
    /// <paramref name="reach"/> past the track on either side.</summary>
    public static (Point From, Point To) Mark(Rect track, double mark, double reach, Orientation orientation)
    {
        var past = Math.Max(0, double.IsNaN(reach) ? 0 : reach);

        if (orientation == Orientation.Horizontal)
        {
            var x = track.X + track.Width * mark;
            return (new Point(x, track.Top - past), new Point(x, track.Bottom + past));
        }

        var y = track.Bottom - track.Height * mark;
        return (new Point(track.Left - past, y), new Point(track.Right + past, y));
    }
}
