using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
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
    private readonly string _screenKey;
    private readonly bool _clickThrough;

    private bool _editing;
    private UIElement? _dragTarget;
    private Vector _dragGrabOffset;
    private readonly List<EditModeAdorner> _adorners = [];

    /// <summary>Layer 1: where widget controls get added (as plain WPF children).</summary>
    public Canvas Surface { get; } = new();

    /// <summary>Raised after a widget is dragged to a new position (canvas coordinates, DIPs). The seam
    /// for persisting layout — this window deliberately doesn't know what a widget "is" or where its
    /// position gets stored.</summary>
    public event Action<UIElement, double, double>? WidgetMoved;

    /// <summary>Raised when a widget is right-clicked in edit mode, so the owner can offer
    /// actions that need to know about the OTHER screens (this window only knows its own).</summary>
    public event Action<ScreenCanvasWindow, UIElement>? WidgetRightClicked;

    /// <summary>The screen this canvas covers, in physical pixels.</summary>
    public System.Drawing.Rectangle ScreenBounds => _deviceBounds;

    public string ScreenLabel => _screenLabel;

    /// <summary>Stable monitor identity for saved layouts — see <see cref="ScreenIdentity"/>.</summary>
    public string ScreenKey => _screenKey;

    public ScreenCanvasWindow(System.Drawing.Rectangle deviceBounds, string screenLabel, string screenKey,
        bool clickThrough)
    {
        _deviceBounds = deviceBounds;
        _screenLabel = screenLabel;
        _screenKey = screenKey;
        _clickThrough = clickThrough;
        Title = "Aquila Desktop Surface";

        WindowStartupLocation = WindowStartupLocation.Manual;
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        ShowInTaskbar = false;

        // AdornerDecorator so edit mode has an adorner layer to draw the dashed outlines in.
        Content = new AdornerDecorator { Child = Surface };

        Surface.MouseLeftButtonDown += OnSurfaceMouseLeftButtonDown;
        Surface.MouseMove += OnSurfaceMouseMove;
        Surface.MouseLeftButtonUp += OnSurfaceMouseLeftButtonUp;
        Surface.MouseRightButtonUp += OnSurfaceMouseRightButtonUp;

    }

    /// <summary>
    /// Enters/leaves edit mode (#30). Canvas level: click-through is lifted so widgets can be
    /// grabbed, and each one gets a dashed outline. Widget level (the dragging below) is plain WPF —
    /// no Win32 at all. Always driven from the app, never from the widget itself, which is why there's no
    /// in-surface affordance to turn it on.
    /// </summary>
    public void SetEditing(bool editing)
    {
        if (_editing == editing) return;
        _editing = editing;

        // Only the click-through bit is touched. WS_EX_NOACTIVATE stays on: mouse messages arrive without
        // it, so dragging works while the surface still never steals focus.
        if (_clickThrough) SetClickThrough(!editing);

        if (editing) AddAdorners(); else RemoveAdorners();
    }

    private void AddAdorners()
    {
        var layer = AdornerLayer.GetAdornerLayer(Surface);
        if (layer is null) return;

        foreach (UIElement child in Surface.Children)
        {

            var adorner = new EditModeAdorner(child);
            layer.Add(adorner);
            _adorners.Add(adorner);
        }
    }

    private void RemoveAdorners()
    {
        var layer = AdornerLayer.GetAdornerLayer(Surface);
        foreach (var adorner in _adorners)
            layer?.Remove(adorner);
        _adorners.Clear();
    }

    private void OnSurfaceMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (!_editing) return;

        var child = FindWidgetUnder(e.OriginalSource as DependencyObject);
        if (child is null) return;

        _dragTarget = child;
        var pointer = e.GetPosition(Surface);
        _dragGrabOffset = pointer - new Point(GetLeft(child), GetTop(child));
        Surface.CaptureMouse();
        e.Handled = true;
    }

    private void OnSurfaceMouseMove(object sender, MouseEventArgs e)
    {
        if (_dragTarget is null) return;

        var pointer = e.GetPosition(Surface);
        var position = pointer - _dragGrabOffset;

        // Keep the widget on its own screen — a canvas is exactly one monitor, so letting it be dragged
        // past the edge would just hide it.
        var size = _dragTarget.RenderSize;
        Canvas.SetLeft(_dragTarget, Math.Clamp(position.X, 0, Math.Max(0, Surface.ActualWidth - size.Width)));
        Canvas.SetTop(_dragTarget, Math.Clamp(position.Y, 0, Math.Max(0, Surface.ActualHeight - size.Height)));
    }

    private void OnSurfaceMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_dragTarget is null) return;

        Surface.ReleaseMouseCapture();
        WidgetMoved?.Invoke(_dragTarget, GetLeft(_dragTarget), GetTop(_dragTarget));
        _dragTarget = null;
    }

    private void OnSurfaceMouseRightButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_editing) return;

        var child = FindWidgetUnder(e.OriginalSource as DependencyObject);
        if (child is null) return;

        WidgetRightClicked?.Invoke(this, child);
        e.Handled = true;
    }

    /// <summary>Adds a widget that came from another screen's canvas, clamped into this one (screens
    /// differ in size, so the old position may not exist here).</summary>
    public void AdoptWidget(UIElement widget, double x, double y)
    {
        Surface.Children.Add(widget);

        var size = widget.RenderSize;
        Canvas.SetLeft(widget, Math.Clamp(x, 0, Math.Max(0, Surface.ActualWidth - size.Width)));
        Canvas.SetTop(widget, Math.Clamp(y, 0, Math.Max(0, Surface.ActualHeight - size.Height)));

        RefreshEditModeAdorners();
    }

    public void ReleaseWidget(UIElement widget)
    {
        Surface.Children.Remove(widget);
        RefreshEditModeAdorners();
    }

    /// <summary>Re-syncs the dashed outlines with the current children — adorners are per-element, so they
    /// have to be rebuilt whenever the set of widgets on this canvas changes.</summary>
    public void RefreshEditModeAdorners()
    {
        if (!_editing) return;
        RemoveAdorners();
        AddAdorners();
    }

    /// <summary>Walks up from whatever was hit to the direct child of the canvas — the widget as a whole,
    /// not the gauge/text inside it.</summary>
    private UIElement? FindWidgetUnder(DependencyObject? hit)
    {
        while (hit is not null && hit != Surface)
        {
            if (VisualTreeHelper.GetParent(hit) == Surface) return hit as UIElement;
            hit = VisualTreeHelper.GetParent(hit);
        }
        return null;
    }

    /// <summary>Canvas.Left/Top are NaN until set; treat that as 0 so arithmetic stays sane.</summary>
    private static double GetLeft(UIElement e) => double.IsNaN(Canvas.GetLeft(e)) ? 0 : Canvas.GetLeft(e);
    private static double GetTop(UIElement e) => double.IsNaN(Canvas.GetTop(e)) ? 0 : Canvas.GetTop(e);

    private void SetClickThrough(bool enabled)
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        if (hwnd == IntPtr.Zero) return;

        var exStyle = GetWindowLongPtr(hwnd, GwlExStyle).ToInt64();
        exStyle = enabled ? exStyle | WsExTransparent : exStyle & ~WsExTransparent;
        SetWindowLongPtr(hwnd, GwlExStyle, new IntPtr(exStyle));
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

        if (_clickThrough) SetClickThrough(true);
    }

}
