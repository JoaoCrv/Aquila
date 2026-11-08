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
    /// The track in a room: the whole length, <paramref name="thickness"/> across, centred across the room.
    ///
    /// Centred rather than at the start, so a bar given more room than it needs — a column of Bars wider than its bar —
    /// sits in the middle of it, and one given exactly its thickness and its marks' reach either side sits between them.
    /// </summary>
    public static Rect Track(Size room, double thickness, Orientation orientation)
    {
        var across = Math.Max(0, double.IsNaN(thickness) ? 0 : thickness);
        var width = Math.Max(0, room.Width);
        var height = Math.Max(0, room.Height);

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
