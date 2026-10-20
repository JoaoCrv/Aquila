using Aquila.Models;

namespace Aquila.Services;

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
        var kind = WidgetCatalog.For(definition.Kind);
        var (width, height) = kind.Reference?.Invoke(definition) ?? (kind.DefaultWidth, kind.DefaultHeight);
        var tighter = Math.Min(definition.Width / width, definition.Height / height);

        // A size that is not a number — only a hand can write one — is read as the kind's own, rather than handed
        // to Clamp, which gives NaN back.
        return double.IsNaN(tighter) ? 1 : Math.Clamp(tighter, Least, Most);
    }
}
