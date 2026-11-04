using System.Windows;

namespace Aquila.Services.EditMode;

/// <summary>
/// Where a new widget goes: the first place on the screen where it covers nothing — no widget, not the docked
/// editor panel, not the edit bar. It used to start at (32, 150), which with the panel docked on the left is under
/// the panel. Pure, rectangles in DIPs, so the tests hold it to its word.
/// </summary>
public static class FreeSpot
{
    /// <summary>The grid the search walks, the margin it keeps from the screen's edges, and the room it leaves
    /// round anything already there.</summary>
    public const double Step = 16;
    public const double Margin = 16;
    public const double Slack = 8;

    /// <summary>The offset of each new widget from the last when there is no room left — a cascade, so they can
    /// still be told apart and dragged off one another.</summary>
    public const double Cascade = 24;

    /// <summary>
    /// The top-left of the first free place, row by row from the top-left of <paramref name="area"/> (the screen's
    /// working area), on a 16-DIP grid, 16 in from the edges and 8 clear of everything in <paramref name="taken"/>.
    /// With no room anywhere, 24 down and right of <paramref name="last"/> (the last widget placed), or of the
    /// first spot when there is none — held on the screen either way.
    /// </summary>
    public static Point Find(Rect area, IReadOnlyList<Rect> taken, Size size, Rect? last = null)
    {
        for (var y = area.Top + Margin; y + size.Height <= area.Bottom - Margin; y += Step)
            for (var x = area.Left + Margin; x + size.Width <= area.Right - Margin; x += Step)
            {
                var spot = new Rect(x, y, size.Width, size.Height);
                if (!taken.Any(t => Overlap(t, spot))) return spot.TopLeft;
            }

        var after = last is { IsEmpty: false } box
            ? new Point(box.Left + Cascade, box.Top + Cascade)
            : new Point(area.Left + Margin, area.Top + Margin);
        return new Point(
            Math.Clamp(after.X, area.Left, Math.Max(area.Left, area.Right - size.Width)),
            Math.Clamp(after.Y, area.Top, Math.Max(area.Top, area.Bottom - size.Height)));
    }

    /// <summary>Whether a spot comes within the slack of something already there.</summary>
    private static bool Overlap(Rect taken, Rect spot) =>
        !taken.IsEmpty
        && spot.Left < taken.Right + Slack && taken.Left - Slack < spot.Right
        && spot.Top < taken.Bottom + Slack && taken.Top - Slack < spot.Bottom;
}
