namespace Aquila.Controls;

/// <summary>
/// A piece that draws a reading, at a size the preset decides.
///
/// Extracted because the same property was declared separately on the gauge, the meter and the stat, and a
/// fourth kind that draws a number — the mini sparkline — was simply left out: nothing failed to compile,
/// the setting just did nothing there. One declaration means a kind either carries the size or visibly does
/// not, which is the same reason the style interfaces exist at all.
///
/// Only the size. Whether the reading is shown belongs to the two kinds whose graphic can stand on its own;
/// a Text widget with its words hidden is an empty widget rather than a cleaner one.
/// </summary>
public interface IValueStyle
{
    double ValueSize { get; set; }

    /// <summary>The face, or null for the app's own. A string rather than a FontFamily because the gauge
    /// draws its number through SkiaSharp, which has never heard of WPF's type.</summary>
    string? ValueFont { get; set; }

    Aquila.Models.TextWeight ValueWeight { get; set; }
}
