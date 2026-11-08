using System.Windows;
using System.Windows.Media;

namespace Aquila.Controls.Primitives;

/// <summary>
/// The pens and fills one element draws with, kept between frames and made again only when what they are made of
/// changes.
///
/// An element is drawn again every frame something in it moves, and almost nothing a pen is made of changes then: the
/// colour, the thickness and the ends stay while the value travels. So each is asked for every frame and made once.
///
/// A pen is frozen when its brush already is — that is what makes it cheap to draw with — and never otherwise: freezing
/// a pen freezes its brush with it, and the brush belongs to whoever handed it over. A fill made here is always frozen,
/// since nobody else holds it.
///
/// Each element keeps its own, as it keeps its own geometry. A handful at most are in use at once, so a short list
/// looked through in order is all it needs, and one emptied when it fills — by then the old entries are a preset that
/// was swapped out.
/// </summary>
public sealed class Inks
{
    /// <summary>How many of each are kept before the list starts again. More than any element draws with at once.
    /// </summary>
    private const int Kept = 8;

    private readonly record struct PenKey(Brush Brush, double Thickness, PenLineCap Caps, PenLineJoin Join,
        DashStyle? Dash);

    private readonly record struct FillKey(Brush Stroke, double Opacity, AreaFill Area);

    private readonly List<(PenKey Key, Pen Pen)> _pens = [];
    private readonly List<(FillKey Key, Brush Fill)> _fills = [];

    /// <summary>The pen for a brush, a thickness, its ends, its corners and — for a line that is not a reading, such as
    /// a limit — its dashes.</summary>
    public Pen Pen(Brush brush, double thickness, PenLineCap caps = PenLineCap.Flat,
        PenLineJoin join = PenLineJoin.Miter, DashStyle? dash = null)
    {
        var key = new PenKey(brush, thickness, caps, join, dash);
        foreach (var (made, pen) in _pens)
            if (made == key)
                return pen;

        var fresh = new Pen(brush, thickness) { StartLineCap = caps, EndLineCap = caps, LineJoin = join };
        if (dash is not null) fresh.DashStyle = dash;
        if (brush.IsFrozen && (dash is null || dash.IsFrozen)) fresh.Freeze();

        if (_pens.Count == Kept) _pens.Clear();
        _pens.Add((key, fresh));
        return fresh;
    }

    /// <summary>
    /// The fill under a line drawn in <paramref name="stroke"/>: its colour at <paramref name="opacity"/>, evenly or
    /// fading to nothing at the bottom (<see cref="AreaFill"/>). Null when there is no area.
    ///
    /// The fade runs over the area's own bounds — from its highest point to the bottom it is closed to — so it is
    /// strongest where the line is highest, whatever the scale. A stroke that is not one colour is taken at the opacity
    /// as it is, evenly: a fade of a fade has no single colour to start from.
    /// </summary>
    public Brush? Area(Brush stroke, double opacity, AreaFill area)
    {
        if (area == AreaFill.None || !(opacity > 0)) return null;

        var key = new FillKey(stroke, opacity, area);
        foreach (var (made, fill) in _fills)
            if (made == key)
                return fill;

        var fresh = FillFor(stroke, Math.Min(opacity, 1), area);

        if (_fills.Count == Kept) _fills.Clear();
        _fills.Add((key, fresh));
        return fresh;
    }

    private readonly List<((double Opacity, AreaFill Area) Key, Brush Mask)> _masks = [];

    /// <summary>The area's opacity on its own, as a mask — even, or fading to nothing at the bottom — for an area whose
    /// colours vary with height and so cannot be a single colour at an opacity. Null when there is no area.</summary>
    public Brush? AreaMask(double opacity, AreaFill area)
    {
        if (area == AreaFill.None || !(opacity > 0)) return null;

        var key = (opacity, area);
        foreach (var (made, mask) in _masks)
            if (made == key)
                return mask;

        var fresh = FillFor(Brushes.Black, Math.Min(opacity, 1), area);

        if (_masks.Count == Kept) _masks.Clear();
        _masks.Add((key, fresh));
        return fresh;
    }

    private static Brush FillFor(Brush stroke, double opacity, AreaFill area)
    {
        Brush fill;
        if (stroke is SolidColorBrush solid)
        {
            var colour = solid.Color;
            var strongest = Color.FromArgb((byte)Math.Round(colour.A * solid.Opacity * opacity), colour.R, colour.G,
                colour.B);

            // The fade ends in the same colour with no alpha, not in Transparent: Transparent is WHITE with none, and the
            // fade would grey on its way down.
            fill = area == AreaFill.Fade
                ? new LinearGradientBrush(strongest, Color.FromArgb(0, colour.R, colour.G, colour.B),
                    new Point(0.5, 0), new Point(0.5, 1))
                : new SolidColorBrush(strongest);
        }
        else
        {
            fill = stroke.CloneCurrentValue();
            fill.Opacity = stroke.Opacity * opacity;
        }

        if (fill.CanFreeze) fill.Freeze();
        return fill;
    }
}
