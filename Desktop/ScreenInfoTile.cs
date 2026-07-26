using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Aquila.Desktop;

/// <summary>
/// An on-surface label showing which screen a canvas is on, plus its resolution/origin. Born as the
/// bring-up diagnostic, kept as a real feature: it's the seed of the "identify screen" overlay #24 calls
/// for (a big number drawn on each physical monitor, like Windows' own Identify button in Display
/// Settings) — needed because `Screen.AllScreens` order is documented as NOT matching the numbering
/// Windows shows the user, so a screen picker can never just trust an index.
///
/// Invoke via <see cref="DesktopSurfaceService.SetScreenInfoVisible"/>.
/// </summary>
internal static class ScreenInfoTile
{
    public static UIElement Create(string label, System.Drawing.Rectangle bounds)
    {
        var panel = new StackPanel { Margin = new Thickness(16, 12, 16, 12) };

        panel.Children.Add(new TextBlock
        {
            Text = label,
            Foreground = Brushes.White,
            FontSize = 22,
            FontWeight = FontWeights.SemiBold,
        });

        panel.Children.Add(new TextBlock
        {
            Text = $"{bounds.Width}×{bounds.Height} @ ({bounds.X},{bounds.Y})",
            Foreground = new SolidColorBrush(Color.FromArgb(0xCC, 0xFF, 0xFF, 0xFF)),
            FontSize = 13,
        });

        var tile = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(0xC8, 0x21, 0x96, 0xF3)),
            CornerRadius = new CornerRadius(10),
            Child = panel,
        };

        Canvas.SetLeft(tile, 32);
        Canvas.SetTop(tile, 32);
        return tile;
    }
}
