using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using static Aquila.DesktopSurface.NativeMethods;

namespace Aquila.DesktopSurface;

/// <summary>
/// Big transient labels drawn on the physical monitors — the same idea as the Identify button in Windows'
/// Display Settings. Needed because neither a screen's name nor its index tells the user WHICH monitor is
/// meant: <c>Screen.AllScreens</c> order doesn't match Windows' own numbering, so the only unambiguous
/// answer is to light up the actual screen.
///
/// Two triggers, one mechanism: held while hovering a "send to screen" menu entry, or flashed on every
/// screen at once from the Identify action.
///
/// Topmost, click-through, and shown WITHOUT activation — it must never take focus (that would close the
/// menu that opened it) nor swallow a click meant for what's underneath.
/// </summary>
internal static class ScreenIdentifyOverlay
{
    private static readonly List<Window> _windows = [];
    private static DispatcherTimer? _autoHide;

    /// <summary>Shows one label, replacing anything on screen. Used while hovering a menu entry.</summary>
    public static void Show(System.Drawing.Rectangle screenBounds, string text) =>
        ShowMany([(screenBounds, text)], autoHideAfter: null);

    /// <summary>Flashes a label on every screen for a moment — the Identify action.</summary>
    public static void ShowAll(IEnumerable<(System.Drawing.Rectangle Bounds, string Text)> screens) =>
        ShowMany(screens, autoHideAfter: TimeSpan.FromSeconds(2.5));

    private static void ShowMany(IEnumerable<(System.Drawing.Rectangle Bounds, string Text)> screens,
        TimeSpan? autoHideAfter)
    {
        Hide();

        foreach (var (bounds, text) in screens)
        {
            var window = Create(bounds, text);
            window.Show();
            _windows.Add(window);
        }

        if (autoHideAfter is not { } delay) return;

        _autoHide = new DispatcherTimer { Interval = delay };
        _autoHide.Tick += (_, _) => Hide();
        _autoHide.Start();
    }

    private static Window Create(System.Drawing.Rectangle screenBounds, string text)
    {
        var window = new Window
        {
            WindowStyle = WindowStyle.None,
            ResizeMode = ResizeMode.NoResize,
            AllowsTransparency = true,
            Background = Brushes.Transparent,
            ShowInTaskbar = false,
            ShowActivated = false, // keeps any caller's menu open
            Topmost = true,
            WindowStartupLocation = WindowStartupLocation.Manual,
            SizeToContent = SizeToContent.WidthAndHeight,
            Content = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(0xE6, 0x10, 0x10, 0x10)),
                CornerRadius = new CornerRadius(16),
                Padding = new Thickness(52, 30, 52, 30),
                Child = new TextBlock
                {
                    Text = text,
                    Foreground = Brushes.White,
                    FontSize = 44,
                    FontWeight = FontWeights.Bold,
                    TextAlignment = TextAlignment.Center,
                },
            },
        };

        window.SourceInitialized += (_, _) =>
        {
            var hwnd = new WindowInteropHelper(window).Handle;
            var exStyle = GetWindowLongPtr(hwnd, GwlExStyle).ToInt64();
            SetWindowLongPtr(hwnd, GwlExStyle, new IntPtr(exStyle | WsExTransparent | WsExToolWindow));

            // Centre on the target screen. Bounds are physical pixels, Left/Top are DIPs.
            var source = PresentationSource.FromVisual(window);
            if (source?.CompositionTarget is null) return;

            var fromDevice = source.CompositionTarget.TransformFromDevice;
            var topLeft = fromDevice.Transform(new Point(screenBounds.Left, screenBounds.Top));
            var bottomRight = fromDevice.Transform(new Point(screenBounds.Right, screenBounds.Bottom));

            window.UpdateLayout();
            window.Left = topLeft.X + ((bottomRight.X - topLeft.X) - window.ActualWidth) / 2;
            window.Top = topLeft.Y + ((bottomRight.Y - topLeft.Y) - window.ActualHeight) / 2;
        };

        return window;
    }

    public static void Hide()
    {
        _autoHide?.Stop();
        _autoHide = null;

        foreach (var window in _windows) window.Close();
        _windows.Clear();
    }
}
