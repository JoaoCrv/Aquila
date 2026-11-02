using System.Windows;

namespace Aquila.Services.EditMode;

/// <summary>
/// Where the editor panel stands. Pure — rectangles in DIPs in, a rectangle out — so the rule can be held to by the
/// tests rather than checked by dragging windows about.
///
/// Docked, it stands on the primary screen, always (João, 2026-11-02): one place to look, whichever screen the
/// widget is on. On the left by default — where the Windows icons are, so where widgets are least likely — and on
/// the right when the left would cover the widget being edited and the right would not. Dragged somewhere by hand,
/// it stays there: a panel placed by hand never jumps on its own.
/// </summary>
public static class PanelPlacement
{
    /// <summary>The margin between a docked panel and its screen's edges.</summary>
    public const double Inset = 16;

    /// <param name="primary">The primary screen's working area.</param>
    /// <param name="widget">The selected widget's box, when it is on the primary screen.</param>
    /// <param name="saved">Where the panel was dragged to, and the working area of the screen that holds it — null
    /// when it is docked, or when that screen is gone.</param>
    /// <param name="width">The panel's width.</param>
    public static Rect Choose(Rect primary, Rect? widget, (Point At, Rect Area)? saved, double width)
    {
        if (saved is { } hand)
        {
            var area = hand.Area;
            var height = Height(area);
            return new Rect(
                Math.Clamp(hand.At.X, area.Left, Math.Max(area.Left, area.Right - width)),
                Math.Clamp(hand.At.Y, area.Top, Math.Max(area.Top, area.Bottom - height)),
                width, height);
        }

        var top = primary.Top + Inset;
        var left = new Rect(primary.Left + Inset, top, width, Height(primary));
        var right = new Rect(Math.Max(primary.Left, primary.Right - Inset - width), top, width, Height(primary));

        return widget is { } box && Covers(left, box) && !Covers(right, box) ? right : left;
    }

    /// <summary>The height of a panel on this screen: all of it but the inset above and below.</summary>
    private static double Height(Rect area) => Math.Max(0, area.Height - Inset * 2);

    private static bool Covers(Rect panel, Rect widget) =>
        !widget.IsEmpty
        && panel.Left < widget.Right && widget.Left < panel.Right && panel.Top < widget.Bottom && widget.Top < panel.Bottom;
}
