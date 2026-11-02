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
    private readonly Dictionary<UIElement, Adorner> _adorners = [];

    // The widget being edited, drawn with a SelectionAdorner instead of the dashed one; and what a handle drag needs
    // to remember from the moment it began.
    private UIElement? _selected;
    private Brush _selectionAccent = Brushes.White;
    private Brush _selectionText = Brushes.Black;
    private bool _keepShape;
    private Rect _resizeFrom;
    private Point _resizePointer;

    // A widget pointed at from the edit bar's list, drawn over its other outline; never more than one.
    private HighlightAdorner? _highlight;

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

        if (editing) RefreshEditModeAdorners(); else RemoveAdorners();
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

    private SelectionAdorner Selection(UIElement widget)
    {
        var adorner = new SelectionAdorner(widget, _selectionAccent, _selectionText) { ShapeKept = _keepShape };
        adorner.ResizeStarted += _ => OnResizeStarted();
        adorner.Resizing += corner => OnResizing(adorner, corner);
        adorner.ResizeCompleted += _ => adorner.ShapeKept = _keepShape;
        return adorner;
    }

    /// <summary>
    /// Draws one widget of this screen as the one being edited, or none. The colours, and whether the editor's lock
    /// keeps its shape, come from the caller: this window knows neither the theme nor the editor.
    /// </summary>
    public void SetSelected(UIElement? widget, Brush accent, Brush onAccent, bool keepShape)
    {
        _selected = widget is not null && Surface.Children.Contains(widget) ? widget : null;
        _selectionAccent = accent;
        _selectionText = onAccent;
        _keepShape = keepShape;

        // Made again rather than kept, so it wears the accent in force now — the theme may have changed since.
        foreach (var (adorned, adorner) in _adorners.Where(pair => pair.Value is SelectionAdorner).ToList())
        {
            AdornerLayer.GetAdornerLayer(Surface)?.Remove(adorner);
            _adorners.Remove(adorned);
        }
        RefreshEditModeAdorners();
    }

    /// <summary>Draws one widget of this screen as pointed at, or none — see <see cref="HighlightAdorner"/>.</summary>
    public void SetHighlighted(UIElement? widget, Brush accent, Brush onAccent, string label)
    {
        var layer = AdornerLayer.GetAdornerLayer(Surface);
        if (_highlight is not null) layer?.Remove(_highlight);
        _highlight = null;

        if (!_editing || widget is null || layer is null || !Surface.Children.Contains(widget)) return;
        _highlight = new HighlightAdorner(widget, accent, onAccent, label);
        layer.Add(_highlight);
    }

    private void OnResizeStarted()
    {
        if (_selected is not FrameworkElement widget) return;

        _resizeFrom = new Rect(GetLeft(widget), GetTop(widget), widget.ActualWidth, widget.ActualHeight);
        _resizePointer = Mouse.GetPosition(Surface);
    }

    /// <summary>
    /// A corner handle pulled: the widget resized live, from where the drag began, so a corner that hit the edge or
    /// the smallest size and came back is where the pointer is, not where the handle's own deltas add up to.
    ///
    /// Free, for every kind alike: the shape it keeps is the one it has, while the editor's lock is on or Shift is
    /// held. A rule per kind — dials kept, the rest free, Shift swapping them — had two widgets answering the same
    /// hand in opposite ways (João, 2026-11-02); a dial in a wide box simply has room beside it, which a background
    /// may want. The owner hears every step, because a new size is a new scale, and the text and strokes inside
    /// should follow the corner rather than jump when it is let go.
    /// </summary>
    private void OnResizing(SelectionAdorner adorner, Corner corner)
    {
        if (_selected is not FrameworkElement widget) return;

        var keepShape = _keepShape || Keyboard.Modifiers.HasFlag(ModifierKeys.Shift);
        adorner.ShapeKept = keepShape;
        var to = EditGeometry.Resize(_resizeFrom, corner, Mouse.GetPosition(Surface) - _resizePointer, keepShape,
            _screen.Size);

        widget.Width = to.Width;
        widget.Height = to.Height;
        if (to.X != GetLeft(widget) || to.Y != GetTop(widget))
        {
            Canvas.SetLeft(widget, to.X);
            Canvas.SetTop(widget, to.Y);
            WidgetMoved?.Invoke(widget, to.X, to.Y);
        }
        WidgetResized?.Invoke(widget, to.Width, to.Height);
    }

    /// <summary>Moves a widget by the arrow keys, held on its screen as a drag is.</summary>
    public void Nudge(UIElement widget, double dx, double dy)
    {
        var at = EditGeometry.Nudge(new Rect(new Point(GetLeft(widget), GetTop(widget)), SizeOf(widget)),
            new Vector(dx, dy), _screen.Size);

        Canvas.SetLeft(widget, at.X);
        Canvas.SetTop(widget, at.Y);
        WidgetMoved?.Invoke(widget, at.X, at.Y);
    }

    /// <summary>The size a widget asked for, else the size it was given: one just built has not been laid out, and
    /// its RenderSize is still nothing.</summary>
    private static Size SizeOf(UIElement widget) =>
        widget is FrameworkElement { Width: var width, Height: var height } && double.IsFinite(width) && double.IsFinite(height)
            ? new Size(width, height)
            : widget.RenderSize;

    private void RemoveAdorners()
    {
        var layer = AdornerLayer.GetAdornerLayer(Surface);
        foreach (var adorner in _adorners.Values)
            layer?.Remove(adorner);
        _adorners.Clear();

        if (_highlight is not null) layer?.Remove(_highlight);
        _highlight = null;
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
        var at = EditGeometry.ClampPosition(position, _dragTarget.RenderSize, _screen.Size);
        Canvas.SetLeft(_dragTarget, at.X);
        Canvas.SetTop(_dragTarget, at.Y);
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
    /// Wheel over a widget resizes it. Both dimensions scale by one factor, held at the ends of the range so the
    /// shape survives them: a widget keeps the shape it has — its kind's, or one the user chose with a handle. The
    /// corner handles are the precise method; this is the quick one.
    /// </summary>
    private void OnSurfaceMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (!_editing) return;

        var child = FindWidgetUnder(e.OriginalSource as DependencyObject);
        if (child is not FrameworkElement widget) return;

        var scale = e.Delta > 0 ? 1.08 : 1 / 1.08;
        var size = EditGeometry.Scale(new Size(widget.ActualWidth, widget.ActualHeight), scale, _screen.Size);

        widget.Width = size.Width;
        widget.Height = size.Height;

        // Growing near an edge would push the widget off-screen, so keep it inside as it changes size — and say so,
        // or the owner keeps the old position and the widget comes back over the edge the next time it is placed.
        var from = new Point(GetLeft(widget), GetTop(widget));
        var at = EditGeometry.ClampPosition(from, size, _screen.Size);
        if (at != from)
        {
            Canvas.SetLeft(widget, at.X);
            Canvas.SetTop(widget, at.Y);
            WidgetMoved?.Invoke(widget, at.X, at.Y);
        }

        WidgetResized?.Invoke(widget, size.Width, size.Height);
        e.Handled = true;
    }

    /// <summary>Adds a widget that came from another screen's canvas, clamped into this one (screens
    /// differ in size, so the old position may not exist here).</summary>
    public void AdoptWidget(UIElement widget, double x, double y)
    {
        Surface.Children.Add(widget);

        var at = EditGeometry.ClampPosition(new Point(x, y), widget.RenderSize, _screen.Size);
        Canvas.SetLeft(widget, at.X);
        Canvas.SetTop(widget, at.Y);

        RefreshEditModeAdorners();
    }

    public void ReleaseWidget(UIElement widget)
    {
        Surface.Children.Remove(widget);
        if (widget == _selected) _selected = null;
        RefreshEditModeAdorners();
    }

    /// <summary>
    /// Re-syncs the outlines with the current children — dashed on each, solid with handles on the selected one.
    /// Adorners are per element, so a widget added or rebuilt needs its own. One that is still right is kept and
    /// only measured again: a refresh usually means a widget changed size, and while a handle is being pulled it
    /// is asked for at every step — making that handle again would drop it from under the pointer.
    /// </summary>
    public void RefreshEditModeAdorners()
    {
        if (!_editing) return;
        var layer = AdornerLayer.GetAdornerLayer(Surface);
        if (layer is null) return;

        foreach (var (widget, adorner) in _adorners.ToList())
        {
            if (Surface.Children.Contains(widget) && adorner is SelectionAdorner == (widget == _selected))
            {
                adorner.InvalidateMeasure();
                continue;
            }
            layer.Remove(adorner);
            _adorners.Remove(widget);
        }

        foreach (UIElement widget in Surface.Children)
        {
            if (_adorners.ContainsKey(widget)) continue;
            Adorner adorner = widget == _selected ? Selection(widget) : new EditModeAdorner(widget);
            layer.Add(adorner);
            _adorners[widget] = adorner;
        }
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
