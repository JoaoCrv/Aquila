using System.Windows;

namespace Aquila.DesktopSurface;

/// <summary>The corner of a widget a resize handle sits on.</summary>
public enum Corner { TopLeft, TopRight, BottomLeft, BottomRight }

/// <summary>
/// The geometry of editing a widget on its screen: how small it may be made, where it may stand, how a corner drag
/// resizes it. Pure — numbers and rectangles in, numbers and rectangles out — so the wheel, the handles, the arrows
/// and the drag all ask the same rules, and the tests can hold them to it. Coordinates are the screen's own DIPs,
/// from its top-left corner, as on the canvas.
/// </summary>
public static class EditGeometry
{
    /// <summary>The smallest a widget may be made, on either side. The wheel stopped at 80 × 60 while the editor's
    /// boxes went down to 40, so the same widget could be made smaller one way than the other.</summary>
    public const double MinSide = 40;

    /// <summary>A size held between <see cref="MinSide"/> and the screen, side by side. Anything that is not a
    /// number becomes the smallest.</summary>
    public static Size ClampSize(double width, double height, Size screen) =>
        new(Side(width, screen.Width), Side(height, screen.Height));

    private static double Side(double wanted, double room)
    {
        var most = Math.Max(MinSide, double.IsFinite(room) ? room : MinSide);
        return double.IsFinite(wanted) ? Math.Clamp(wanted, MinSide, most) : MinSide;
    }

    /// <summary>A size grown or shrunk by one factor on both sides — the wheel. The factor is held so that neither side
    /// passes the smallest or the screen, so the shape a widget has is the shape it keeps, at the ends of its range
    /// too: held side by side instead, one side stopped while the other went on.</summary>
    public static Size Scale(Size size, double factor, Size screen)
    {
        if (size.Width <= 0 || size.Height <= 0 || !double.IsFinite(factor))
            return ClampSize(size.Width, size.Height, screen);

        var most = Math.Min(screen.Width / size.Width, screen.Height / size.Height);
        var least = Math.Min(Math.Max(MinSide / size.Width, MinSide / size.Height), most);
        var scale = Math.Clamp(factor, least, most);
        return new Size(size.Width * scale, size.Height * scale);
    }

    /// <summary>Where a widget of this size may stand: wholly on its screen when it fits, against the top-left edges
    /// when it does not — a widget past the edge would simply be hidden.</summary>
    public static Point ClampPosition(Point at, Size size, Size screen) =>
        new(Math.Clamp(Finite(at.X), 0, Math.Max(0, screen.Width - size.Width)),
            Math.Clamp(Finite(at.Y), 0, Math.Max(0, screen.Height - size.Height)));

    /// <summary>A widget moved by the arrow keys, held on its screen as a drag is.</summary>
    public static Point Nudge(Rect widget, Vector by, Size screen) =>
        ClampPosition(widget.TopLeft + by, widget.Size, screen);

    /// <summary>
    /// A widget resized by one corner, the pointer having moved by <paramref name="moved"/> since the drag began.
    /// The opposite corner stays where it was; no side goes below <see cref="MinSide"/> or past the screen; and with
    /// <paramref name="keepShape"/> both sides take one scale, led by whichever the pointer changed more — held, as a
    /// whole, to what both sides allow, so the shape survives reaching an edge.
    /// </summary>
    public static Rect Resize(Rect start, Corner corner, Vector moved, bool keepShape, Size screen)
    {
        var fromLeft = corner is Corner.TopLeft or Corner.BottomLeft;
        var fromTop = corner is Corner.TopLeft or Corner.TopRight;

        // The corner that stays, and the room from it to the screen's edge on the side the widget grows towards.
        var anchorX = fromLeft ? start.Right : start.Left;
        var anchorY = fromTop ? start.Bottom : start.Top;
        var roomX = Math.Max(0, fromLeft ? anchorX : screen.Width - anchorX);
        var roomY = Math.Max(0, fromTop ? anchorY : screen.Height - anchorY);

        var width = start.Width + (fromLeft ? -moved.X : moved.X);
        var height = start.Height + (fromTop ? -moved.Y : moved.Y);

        if (keepShape && start.Width > 0 && start.Height > 0)
        {
            var scale = Math.Abs(width / start.Width - 1) >= Math.Abs(height / start.Height - 1)
                ? width / start.Width
                : height / start.Height;
            var most = Math.Min(roomX / start.Width, roomY / start.Height);
            var least = Math.Min(Math.Max(MinSide / start.Width, MinSide / start.Height), most);
            scale = Math.Clamp(Finite(scale), least, most);
            width = start.Width * scale;
            height = start.Height * scale;
        }
        else
        {
            width = Math.Clamp(Finite(width), Math.Min(MinSide, roomX), roomX);
            height = Math.Clamp(Finite(height), Math.Min(MinSide, roomY), roomY);
        }

        return new Rect(fromLeft ? anchorX - width : anchorX, fromTop ? anchorY - height : anchorY, width, height);
    }

    private static double Finite(double value) => double.IsFinite(value) ? value : 0;
}
