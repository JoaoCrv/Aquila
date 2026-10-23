using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using static Aquila.DesktopSurface.NativeMethods;

namespace Aquila.DesktopSurface;

/// <summary>
/// The per-monitor "screen canvas" window that will host the dynamic background (layer 0) and the
/// widgets (layer 1, on <see cref="Surface"/>). Built in code (not XAML) so the whole
/// <see cref="Aquila.DesktopSurface"/> folder stays self-contained and domain-free — ready to extract later.
///
/// Styles were brought up one at a time and each verified on screen (2026-07): per-screen positioning →
/// borderless → transparency → no taskbar entry → click-through. (An earlier "everything at once" version
/// looked broken, but that turned out to be a disabled setting, not the styles.)
///
/// The window covers the screen only while editing. Otherwise it covers what its widgets cover, and a screen with
/// none has no window up at all (#50): a transparent window is kept as a bitmap of its whole size, four bytes a
/// pixel, almost all of them clear. Canvas coordinates stay the SCREEN's — the content is shifted, not moved — so
/// nothing outside this class knows the window changed size.
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

    private readonly ExtentCanvas _surface = new();
    private readonly TranslateTransform _shift = new();
    private Rect _screen = Rect.Empty; // the whole screen, in DIPs
    private double _dipsPerPixel = 1;

    /// <summary>Room left around the widgets, so nothing drawn at an edge is cut by the window's.</summary>
    private const double FitMargin = 8;

    /// <summary>Layer 1: where widget controls get added (as plain WPF children).</summary>
    public Canvas Surface => _surface;

    /// <summary>Raised after a widget is dragged to a new position (canvas coordinates, DIPs). The seam
    /// for persisting layout — this window deliberately doesn't know what a widget "is" or where its
    /// position gets stored.</summary>
    public event Action<UIElement, double, double>? WidgetMoved;

    /// <summary>Raised when a widget is right-clicked in edit mode, so the owner can offer
    /// actions that need to know about the OTHER screens (this window only knows its own).</summary>
    public event Action<ScreenCanvasWindow, UIElement>? WidgetRightClicked;

    /// <summary>Raised when a widget is pressed in edit mode, before any drag begins. Selection and
    /// dragging share the gesture on purpose: you reach for a widget to move it or to change it, and
    /// making those two different gestures would mean explaining which is which.</summary>
    public event Action<ScreenCanvasWindow, UIElement>? WidgetSelected;

    /// <summary>Raised after a widget is resized with the wheel, with its new size in DIPs.</summary>
    public event Action<UIElement, double, double>? WidgetResized;

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
        ShowActivated = false; // Fit shows it again whenever a widget arrives, and must never take the focus

        // AdornerDecorator so edit mode has an adorner layer to draw the dashed outlines in.
        Content = new AdornerDecorator { Child = Surface };

        _surface.RenderTransform = _shift;
        // Not from inside the canvas's arrange: resizing the window there would lay it out again mid-pass.
        _surface.ExtentChanged += () => Dispatcher.BeginInvoke(Fit);

        Surface.MouseLeftButtonDown += OnSurfaceMouseLeftButtonDown;
        Surface.MouseMove += OnSurfaceMouseMove;
        Surface.MouseLeftButtonUp += OnSurfaceMouseLeftButtonUp;
        Surface.MouseRightButtonUp += OnSurfaceMouseRightButtonUp;
        Surface.MouseWheel += OnSurfaceMouseWheel;

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
        Fit();
    }

    /// <summary>The whole screen while editing, so a widget can be dragged anywhere on it or sent to an empty one;
    /// otherwise the widgets' extent, and nothing on screen when there are none.</summary>
    private void Fit()
    {
        if (_screen.IsEmpty) return; // before OnSourceInitialized, which calls it again

        var area = _editing ? new Rect(_screen.Size) : Around(_surface.Extent);
        if (area.IsEmpty)
        {
            if (IsVisible) Hide();
            return;
        }

        _shift.X = -area.X;
        _shift.Y = -area.Y;
        Left = _screen.X + area.X;
        Top = _screen.Y + area.Y;
        Width = area.Width;
        Height = area.Height;
        if (!IsVisible) Show();
    }

    /// <summary>The extent with its margin, inside the screen, and on whole device pixels: a window at a fraction
    /// of one would draw every widget in it a fraction off, and blurred.</summary>
    private Rect Around(Rect extent)
    {
        if (extent.IsEmpty) return extent;

        extent.Inflate(FitMargin, FitMargin);
        extent.Intersect(new Rect(_screen.Size));
        if (extent.IsEmpty) return extent;

        var d = _dipsPerPixel;
        return new Rect(
            new Point(Math.Floor(extent.Left / d) * d, Math.Floor(extent.Top / d) * d),
            new Point(Math.Ceiling(extent.Right / d) * d, Math.Ceiling(extent.Bottom / d) * d));
    }

    /// <summary>A canvas that says when the area its children cover changes — on arrange, which is where a widget
    /// added, moved or resized always ends up, whoever did it.</summary>
    private sealed class ExtentCanvas : Canvas
    {
        public Rect Extent { get; private set; } = Rect.Empty;
        public event Action? ExtentChanged;

        protected override Size ArrangeOverride(Size arrangeSize)
        {
            var size = base.ArrangeOverride(arrangeSize);

            var extent = Rect.Empty;
            foreach (UIElement child in InternalChildren)
            {
                if (child.Visibility == Visibility.Collapsed) continue;
                extent.Union(new Rect(new Point(ScreenCanvasWindow.GetLeft(child), ScreenCanvasWindow.GetTop(child)),
                    child.DesiredSize));
            }

            if (extent != Extent)
            {
                Extent = extent;
                ExtentChanged?.Invoke();
            }
            return size;
        }
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

        WidgetSelected?.Invoke(this, child);

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
        Canvas.SetLeft(_dragTarget, Math.Clamp(position.X, 0, Math.Max(0, _screen.Width - size.Width)));
        Canvas.SetTop(_dragTarget, Math.Clamp(position.Y, 0, Math.Max(0, _screen.Height - size.Height)));
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

    /// <summary>
    /// Wheel over a widget resizes it. Both dimensions scale together, which keeps most shapes as they were. A
    /// widget whose figure has a fixed shape is held to it by whoever owns it, on hearing
    /// <see cref="WidgetResized"/>: this window knows nothing of kinds, and its bounds below are per side. The size
    /// boxes in the editor remain the precise method; this is the quick one.
    /// </summary>
    private void OnSurfaceMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (!_editing) return;

        var child = FindWidgetUnder(e.OriginalSource as DependencyObject);
        if (child is not FrameworkElement widget) return;

        var scale = e.Delta > 0 ? 1.08 : 1 / 1.08;
        var width = Math.Clamp(widget.ActualWidth * scale, 80, 600);
        var height = Math.Clamp(widget.ActualHeight * scale, 60, 500);

        widget.Width = width;
        widget.Height = height;

        // Growing near an edge would push the widget off-screen, so keep it inside as it changes size.
        Canvas.SetLeft(widget, Math.Clamp(GetLeft(widget), 0, Math.Max(0, _screen.Width - width)));
        Canvas.SetTop(widget, Math.Clamp(GetTop(widget), 0, Math.Max(0, _screen.Height - height)));

        WidgetResized?.Invoke(widget, width, height);
        e.Handled = true;
    }

    /// <summary>Adds a widget that came from another screen's canvas, clamped into this one (screens
    /// differ in size, so the old position may not exist here).</summary>
    public void AdoptWidget(UIElement widget, double x, double y)
    {
        Surface.Children.Add(widget);

        var size = widget.RenderSize;
        Canvas.SetLeft(widget, Math.Clamp(x, 0, Math.Max(0, _screen.Width - size.Width)));
        Canvas.SetTop(widget, Math.Clamp(y, 0, Math.Max(0, _screen.Height - size.Height)));

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
            _screen = new Rect(topLeft, bottomRight);
            _dipsPerPixel = fromDevice.M11;
        }

        if (_clickThrough) SetClickThrough(true);

        // After whoever showed it has put its widgets on (the surface service populates right after Show): a canvas
        // that stays empty never re-arranges to a different extent, so nothing else would ever take it down.
        Dispatcher.BeginInvoke(Fit, System.Windows.Threading.DispatcherPriority.Background);
    }

}
