namespace Aquila.Controls;

/// <summary>
/// A piece with sizes of its own in its drawing — a gap, a margin, a column for the number — that have to grow with
/// the widget as the preset's sizes do, or a dial made twice as big draws its number at twice the size in a column
/// that stayed the width it was.
///
/// The preset's sizes are multiplied before they arrive; these the piece multiplies itself, because only the piece
/// knows them. Matched structurally like the style interfaces, so a piece with nothing of its own to scale simply
/// does not implement it. At 1 every piece draws exactly what it drew before there was a scale — which is also what
/// the dashboard's cards, which never set one, go on drawing.
///
/// Not called Scale: a chart already has a Scale, the way its vertical axis is decided.
/// </summary>
public interface IScaledFigure
{
    double FigureScale { get; set; }
}

/// <summary>
/// A piece whose figure can be turned — a dial's arc, a clock's rings, a chart's plot, the bars — by the widget's
/// <c>Angle</c>, in degrees clockwise. Only the figure: whatever the piece writes outside it (a title's numbers, a
/// legend) stays upright, and the widget's box never turns. Matched structurally, like <see cref="IScaledFigure"/>.
/// </summary>
public interface ITurnedFigure
{
    double Angle { get; set; }
}

/// <summary>The figure's own sizes, at a scale. Each keeps its kind: a star or automatic column is a share of
/// the room, not a size, so it is left as it is.</summary>
public static class FigureScaling
{
    public static Thickness Scaled(this Thickness t, double scale) =>
        new(t.Left * scale, t.Top * scale, t.Right * scale, t.Bottom * scale);

    public static CornerRadius Scaled(this CornerRadius r, double scale) =>
        new(r.TopLeft * scale, r.TopRight * scale, r.BottomRight * scale, r.BottomLeft * scale);

    public static System.Windows.GridLength Scaled(this System.Windows.GridLength length, double scale) =>
        length.IsAbsolute ? new System.Windows.GridLength(length.Value * scale) : length;

    /// <summary>
    /// How much a figure <paramref name="size"/> big shrinks when turned by <paramref name="angle"/> degrees, so the
    /// turned figure still fits the room it was laid out in — as a picture turned in its frame: the same shape, smaller
    /// until its corners clear the frame. 1 upright and upside down, and never more than 1.
    /// </summary>
    public static double TurnedScale(Size size, double angle)
    {
        // Upright or upside down it has the same box — said outright, since a sine of 2π is not quite nought.
        if (!(size.Width > 0) || !(size.Height > 0) || !double.IsFinite(angle) || angle % 180 == 0) return 1;

        var radians = angle * Math.PI / 180;
        var (cos, sin) = (Math.Abs(Math.Cos(radians)), Math.Abs(Math.Sin(radians)));
        var across = size.Width * cos + size.Height * sin;
        var down = size.Width * sin + size.Height * cos;

        return Math.Min(1, Math.Min(size.Width / across, size.Height / down));
    }

    /// <summary>Turns a figure about its middle by <paramref name="angle"/> degrees, shrunk to fit where it was laid out
    /// (<see cref="TurnedScale"/>). Drawing only — the figure's place and size are what they were, so nothing around it
    /// moves, and an upright figure draws exactly as it always did.</summary>
    public static void Turn(FrameworkElement figure, double angle)
    {
        if (!double.IsFinite(angle) || angle % 360 == 0)
        {
            figure.RenderTransform = System.Windows.Media.Transform.Identity;
            return;
        }

        var scale = TurnedScale(figure.RenderSize, angle);
        figure.RenderTransformOrigin = new Point(0.5, 0.5);
        figure.RenderTransform = new System.Windows.Media.TransformGroup
        {
            Children =
            {
                new System.Windows.Media.ScaleTransform(scale, scale),
                new System.Windows.Media.RotateTransform(angle),
            },
        };
    }
}
