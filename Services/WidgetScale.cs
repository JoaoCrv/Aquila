using Aquila.Models;

namespace Aquila.Services;

/// <summary>Which side leads a resize: the one the user set, which the other follows when the proportions are kept.
/// </summary>
public enum SizeLead { Width, Height }

/// <summary>
/// How much bigger than its kind's default a widget is drawn — the one multiplier that every size the preset
/// gives is put through, and with them the sizes a piece fixes in its own drawing.
///
/// So a preset's sizes mean "at the kind's default size", which is the size the type picker shows each kind at.
/// Before this, a widget made bigger was a bigger box round the same drawing: an 18px number in a dial three
/// times the size, inside a ring as thin as before.
///
/// The tighter axis decides. A meter made only wider is a longer bar, not a bigger one — its number has no more
/// height to grow into — while a clock made three times bigger both ways is three times bigger.
///
/// Multiplied into each size where it is applied, never laid over the content as a transform: a widget stretched one
/// way would stretch its text with it. (While LiveCharts drew the charts there was a second reason — it painted into
/// a bitmap at its laid-out size, which a ScaleTransform would have magnified and blurred.)
/// </summary>
public static class WidgetScale
{
    /// <summary>The bounds, against sizes written by hand: widgets.json is the user's to edit, and a widget typed in
    /// at forty thousand pixels would ask for text no screen can show, one at nothing for none at all.</summary>
    public const double Least = 0.25;
    public const double Most = 8;

    public static double Of(DesktopWidgetDefinition definition)
    {
        var (width, height) = ReferenceOf(definition);
        var tighter = Math.Min(definition.Width / width, definition.Height / height);

        // A size that is not a number — only a hand can write one — is read as the kind's own, rather than handed
        // to Clamp, which gives NaN back.
        return double.IsNaN(tighter) ? 1 : Math.Clamp(tighter, Least, Most);
    }

    /// <summary>The size a widget's preset sizes are meant at: its kind's <see cref="WidgetKindInfo.Reference"/>,
    /// or the kind's default size where it has none.</summary>
    public static (double Width, double Height) ReferenceOf(DesktopWidgetDefinition definition)
    {
        var kind = WidgetCatalog.For(definition.Kind);
        return kind.Reference?.Invoke(definition) ?? (kind.DefaultWidth, kind.DefaultHeight);
    }

    /// <summary>The proportions a widget of a figure with its own shape is given when it becomes one — its reference
    /// size — or null for a kind with none (<see cref="WidgetKindInfo.HasShape"/>).</summary>
    public static (double Width, double Height)? ShapeOf(DesktopWidgetDefinition definition) =>
        WidgetCatalog.For(definition.Kind).HasShape?.Invoke(definition) == true ? ReferenceOf(definition) : null;

    /// <summary>The size a resize lands on for a widget given its kind's shape (<see cref="ShapeOf"/>); a kind with
    /// none gets exactly what it asked for.</summary>
    public static (double Width, double Height) Proportioned(DesktopWidgetDefinition definition, double width,
        double height, SizeLead lead, double least = 0, double most = double.PositiveInfinity) =>
        ShapeOf(definition) is { } shape ? Proportioned(shape, width, height, lead, least, most) : (width, height);

    /// <summary>
    /// The size a resize to <paramref name="width"/> × <paramref name="height"/> lands on, held to the proportions
    /// of <paramref name="shape"/> — a kind's own, or whatever a widget has while the editor's lock is on.
    ///
    /// Led by <paramref name="lead"/>: type a width and the height follows, type a height and the width does. Within
    /// <paramref name="least"/> and <paramref name="most"/> on both sides when they are given, which can move the
    /// lead too — a dial cannot be the widest the editor allows and still be its height taller.
    /// </summary>
    public static (double Width, double Height) Proportioned((double Width, double Height) shape, double width,
        double height, SizeLead lead, double least = 0, double most = double.PositiveInfinity)
    {
        var (across, down) = shape;
        var led = lead == SizeLead.Width ? width : height;
        if (!(across > 0) || !(down > 0) || double.IsNaN(led)) return (width, height);

        // Multiplied before it is divided, so a widget given its reference width gets its reference height to the
        // last bit, and a box that shows whole numbers is not handed a 189.99999999999997.
        if (lead == SizeLead.Width)
        {
            var w = Within(width, Math.Max(least, least * across / down), Math.Min(most, most * across / down));
            return (w, w * down / across);
        }

        var h = Within(height, Math.Max(least, least * down / across), Math.Min(most, most * down / across));
        return (h * across / down, h);
    }

    /// <summary>Held between two bounds — unless no size between them has the shape, when it is left as it is
    /// rather than squeezed into one it was never meant to have.</summary>
    private static double Within(double value, double low, double high) =>
        low <= high ? Math.Clamp(value, low, high) : value;
}
