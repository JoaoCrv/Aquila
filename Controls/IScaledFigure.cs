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
}
