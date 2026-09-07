namespace Aquila.Controls;

/// <summary>
/// A piece that draws a reading, at a size the preset decides.
///
/// Extracted because the same property was declared separately on the gauge, the meter and the stat, and a
/// fourth kind that draws a number — the mini sparkline — was simply left out: nothing failed to compile,
/// the setting just did nothing there. One declaration means a kind either carries the size or visibly does
/// not, which is the same reason the style interfaces exist at all.
///
/// Whether the reading is SHOWN is not here: that belongs to the two kinds whose graphic can stand on its
/// own, and a Text widget with its words hidden is an empty widget rather than a cleaner one. Nor is the
/// unit — see <see cref="IUnitStyle"/>, which a clock has no use for.
/// </summary>
public interface IValueStyle
{
    double ValueSize { get; set; }

    /// <summary>The face, or null for the app's own. A string rather than a FontFamily because the gauge
    /// draws its number through SkiaSharp, which has never heard of WPF's type.</summary>
    string? ValueFont { get; set; }

    Aquila.Models.TextWeight ValueWeight { get; set; }
}

/// <summary>
/// A piece whose reading comes with a unit, and can be asked not to show it.
///
/// Its own interface rather than a member of <see cref="IValueStyle"/>, because a clock draws a reading and
/// has no unit: putting it there would have forced a property onto the one kind that can never mean it.
/// Matched structurally like the rest, so the answer to "does this show a unit" is the type and not a list
/// someone maintains.
///
/// It was declared twice before this, on <see cref="ICaptionStyle"/> and <see cref="IStatStyle"/>. Only the
/// stat's was ever assigned — the Text piece had the property and nothing set it, so its unit was always
/// on, and the meter, the sparkline and the dial had no say at all.
/// </summary>
public interface IUnitStyle
{
    bool ShowUnit { get; set; }
}

/// <summary>
/// A piece that draws a graphic as well as a number, and so can be asked for the graphic alone.
///
/// The criterion is whether anything is left. A dial without its number is a ring, a bar is a bar and a
/// sparkline is a trend — all still readable. A stat without its number is an empty box, and a Text widget
/// with its words hidden is an empty widget, so neither implements this and the editor cannot offer it.
///
/// Extracted for the same reason <see cref="IValueStyle"/> was: the property was declared separately on the
/// gauge and the meter, and the sparkline — which draws a number over a graphic that stands perfectly well
/// without it — was simply left out. Nothing failed to compile; the option just was not there to choose.
/// </summary>
public interface IHideableValue
{
    bool ShowValue { get; set; }
}
