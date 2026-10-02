using System.Windows.Media;

namespace Aquila.Helpers;

/// <summary>
/// A stored "#RRGGBB" as a brush, with its opacity kept beside it rather than inside it — the way presets
/// and widgets.json store a colour.
///
/// Frozen, because nothing changes these after they are made: an unfrozen brush carries change
/// notification for its whole life, and the desktop asks for a fresh one on every repaint. Transparent
/// rather than throwing for a colour that does not parse, so a hand-edited preset cannot break the desktop.
///
/// Written once. There were three: one frozen, one not, and the editor's preview with no fallback at all,
/// which threw on the first bad colour.
/// </summary>
public static class HexBrush
{
    public static SolidColorBrush From(string? hex, double opacity = 1)
    {
        try
        {
            var colour = (Color)ColorConverter.ConvertFromString(hex);
            colour.A = (byte)Math.Clamp(opacity * 255, 0, 255);

            var brush = new SolidColorBrush(colour);
            brush.Freeze();
            return brush;
        }
        catch
        {
            return Brushes.Transparent;
        }
    }
}
