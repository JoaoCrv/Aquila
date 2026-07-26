using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using static Aquila.Desktop.NativeMethods;

namespace Aquila.Desktop;

/// <summary>
/// The per-monitor "screen canvas" window that will host the dynamic background (layer 0) and the
/// widgets (layer 1, on <see cref="Surface"/>). Built in code (not XAML) so the whole
/// <see cref="Aquila.Desktop"/> folder stays self-contained and domain-free — ready to extract later.
///
/// Styles were brought up one at a time and each verified on screen (2026-07): per-screen positioning →
/// borderless → transparency → no taskbar entry → click-through. (An earlier "everything at once" version
/// looked broken, but that turned out to be a disabled setting, not the styles.)
/// </summary>
internal sealed class ScreenCanvasWindow : Window
{
    private readonly System.Drawing.Rectangle _deviceBounds;
    private readonly string _screenLabel;
    private readonly bool _clickThrough;
    private UIElement? _infoTile;

    /// <summary>Layer 1: where widget controls get added (as plain WPF children). Empty for now.</summary>
    public Canvas Surface { get; } = new();

    public ScreenCanvasWindow(System.Drawing.Rectangle deviceBounds, string screenLabel,
        bool showScreenInfo, bool clickThrough)
    {
        _deviceBounds = deviceBounds;
        _screenLabel = screenLabel;
        _clickThrough = clickThrough;
        Title = "Aquila Desktop Surface";

        WindowStartupLocation = WindowStartupLocation.Manual;
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        ShowInTaskbar = false;

        Content = Surface;

        SetScreenInfoVisible(showScreenInfo);
    }

    /// <summary>Shows/hides this screen's info label (see <see cref="ScreenInfoTile"/>) — the seed of the
    /// "identify screen" overlay in #24. Idempotent, so callers can toggle freely.</summary>
    public void SetScreenInfoVisible(bool visible)
    {
        if (visible == (_infoTile is not null)) return;

        if (visible)
        {
            _infoTile = ScreenInfoTile.Create(_screenLabel, _deviceBounds);
            Surface.Children.Add(_infoTile);
        }
        else
        {
            Surface.Children.Remove(_infoTile);
            _infoTile = null;
        }
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        // Cover the whole target screen. Screen.Bounds is physical pixels; convert to the DIPs WPF's
        // Left/Top/Width/Height expect using this window's DPI. Mixed-DPI multi-monitor is a known
        // follow-up (#24) — correct for the common uniform-DPI case.
        var source = PresentationSource.FromVisual(this);
        if (source?.CompositionTarget is not null)
        {
            var fromDevice = source.CompositionTarget.TransformFromDevice;
            var topLeft = fromDevice.Transform(new Point(_deviceBounds.Left, _deviceBounds.Top));
            var bottomRight = fromDevice.Transform(new Point(_deviceBounds.Right, _deviceBounds.Bottom));
            Left = topLeft.X;
            Top = topLeft.Y;
            Width = bottomRight.X - topLeft.X;
            Height = bottomRight.Y - topLeft.Y;
        }

        if (_clickThrough)
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            var exStyle = GetWindowLongPtr(hwnd, GwlExStyle).ToInt64();
            SetWindowLongPtr(hwnd, GwlExStyle, new IntPtr(exStyle | WsExTransparent));
        }
    }

}
