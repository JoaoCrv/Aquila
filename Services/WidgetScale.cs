using Aquila.Models;

namespace Aquila.Services;

/// <summary>Which side leads a resize: the one the user set, which the other follows when the widget keeps its
/// proportions.</summary>
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
/// Multiplied into each size where it is applied, never laid over the content as a transform. LiveCharts paints
/// into a bitmap at the size it is laid out at, so a ScaleTransform would magnify those pixels and blur them, and a
/// widget stretched one way would stretch its text with it.
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

    /// <summary>The proportions a widget is held to — its reference size — or null when it may take any
    /// (<see cref="WidgetKindInfo.KeepsProportions"/>).</summary>
    public static (double Width, double Height)? ShapeOf(DesktopWidgetDefinition definition) =>
        WidgetCatalog.For(definition.Kind).KeepsProportions?.Invoke(definition) == true ? ReferenceOf(definition) : null;

    /// <summary>
    /// The size a resize to <paramref name="width"/> × <paramref name="height"/> lands on.
    ///
    /// The one place a widget is held to its proportions: every way of resizing one asks here — the wheel, the
    /// editor's boxes, and the moment a widget starts keeping them — so no two of them can come to disagree about
    /// what shape a dial is. A free kind gets exactly what it asked for.
    ///
    /// One that keeps them gets its reference's shape, led by <paramref name="lead"/>: type a width and the height
    /// follows, type a height and the width does. Within <paramref name="least"/> and <paramref name="most"/> on
    /// both sides when they are given, which can move the lead too — a dial cannot be the widest the editor allows
    /// and still be its height taller.
    ///
    /// Only a resize asks. A size read from widgets.json is drawn as it was written, and takes its proportions the
    /// next time it is resized.
    /// </summary>
    public static (double Width, double Height) Proportioned(DesktopWidgetDefinition definition, double width,
        double height, SizeLead lead, double least = 0, double most = double.PositiveInfinity)
    {
        if (ShapeOf(definition) is not { } shape) return (width, height);

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
