using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Aquila.Desktop;

/// <summary>
/// A big transient label drawn on one physical monitor — the same idea as the Identify button in Windows'
/// Display Settings. Needed because a screen picker can never rely on names or indexes to tell the user
/// WHICH monitor an entry means: <c>Screen.AllScreens</c> order doesn't match the numbering Windows
/// shows, so the only unambiguous answer is to light up the actual screen.
///
/// Topmost and shown WITHOUT activation: the menu that triggers it must keep focus, or it would close the
/// moment the overlay appeared.
/// </summary>
internal static class ScreenIdentifyOverlay
{
    private static Window? _current;

    public static void Show(System.Drawing.Rectangle screenBounds, string text)
    {
        Hide();

        var window = new Window
        {
            WindowStyle = WindowStyle.None,
            ResizeMode = ResizeMode.NoResize,
            AllowsTransparency = true,
            Background = Brushes.Transparent,
            ShowInTaskbar = false,
            ShowActivated = false, // keeps the caller's menu open
            Topmost = true,
            WindowStartupLocation = WindowStartupLocation.Manual,
            SizeToContent = SizeToContent.WidthAndHeight,
            Content = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(0xE0, 0x10, 0x10, 0x10)),
                CornerRadius = new CornerRadius(16),
                Padding = new Thickness(48, 28, 48, 28),
                Child = new TextBlock
                {
                    Text = text,
                    Foreground = Brushes.White,
                    FontSize = 48,
                    FontWeight = FontWeights.Bold,
                    TextAlignment = TextAlignment.Center,
                },
            },
        };

        // Centre on the target screen. Bounds are physical pixels, Left/Top are DIPs — convert once the
        // handle exists, which is also when the final size is known.
        window.SourceInitialized += (_, _) =>
        {
            var source = PresentationSource.FromVisual(window);
            if (source?.CompositionTarget is null) return;

            var fromDevice = source.CompositionTarget.TransformFromDevice;
            var topLeft = fromDevice.Transform(new Point(screenBounds.Left, screenBounds.Top));
            var bottomRight = fromDevice.Transform(new Point(screenBounds.Right, screenBounds.Bottom));

            window.UpdateLayout();
            window.Left = topLeft.X + ((bottomRight.X - topLeft.X) - window.ActualWidth) / 2;
            window.Top = topLeft.Y + ((bottomRight.Y - topLeft.Y) - window.ActualHeight) / 2;
        };

        window.Show();
        _current = window;
    }

    public static void Hide()
    {
        _current?.Close();
        _current = null;
    }
}
