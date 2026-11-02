using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;

namespace Aquila.DesktopSurface;

/// <summary>
/// The widget being edited, drawn as such: a solid outline just outside it, a handle on each corner to resize it
/// by, and its size under the bottom-left corner — with a padlock while it keeps its shape. It takes the place of
/// the dashed <see cref="EditModeAdorner"/> on that one widget, so the desktop says which of them the panel is about.
///
/// The colours are handed in — the accent and the text that reads on it — because this folder knows no theme; the
/// caller reads them when it selects. Only the handles take the mouse: the outline and the label are drawn, not hit,
/// so pressing on a selected widget still reaches the widget and drags it.
/// </summary>
internal sealed class SelectionAdorner : Adorner
{
    private const double Gap = 2;       // between the widget and the outline
    private const double Stroke = 2;
    private const double Radius = 10;
    private const double HandleSize = 10;

    private const string LockGlyph = ""; // Lock, in both Windows icon fonts
    private static readonly FontFamily Icons = new("Segoe Fluent Icons, Segoe MDL2 Assets");

    private readonly VisualCollection _handles;
    private readonly Pen _outline;
    private readonly Brush _accent;
    private readonly Brush _onAccent;
    private bool _shapeKept;

    /// <summary>Whether the widget is keeping its shape — the lock is on, or Shift is held while a corner is pulled.
    /// The label then wears a padlock, so the reason a corner will not go where the pointer is shows where the eye
    /// already is.</summary>
    public bool ShapeKept
    {
        get => _shapeKept;
        set
        {
            if (_shapeKept == value) return;
            _shapeKept = value;
            InvalidateVisual();
        }
    }

    /// <summary>A handle was pressed, moved or let go. The window does the resizing: it knows the screen and the
    /// canvas, and reads the pointer against them rather than trusting the handle's own deltas, which shift as
    /// the handle moves with the corner it is on.</summary>
    public event Action<Corner>? ResizeStarted;
    public event Action<Corner>? Resizing;
    public event Action<Corner>? ResizeCompleted;

    public SelectionAdorner(UIElement widget, Brush accent, Brush onAccent) : base(widget)
    {
        _accent = accent;
        _onAccent = onAccent;
        _outline = new Pen(accent, Stroke);
        _outline.Freeze();

        _handles = new VisualCollection(this);
        var template = HandleTemplate(accent);
        foreach (var corner in Enum.GetValues<Corner>())
        {
            var handle = new Thumb
            {
                Width = HandleSize,
                Height = HandleSize,
                Template = template,
                Tag = corner,
                Cursor = corner is Corner.TopLeft or Corner.BottomRight ? Cursors.SizeNWSE : Cursors.SizeNESW,
            };
            handle.DragStarted += (_, _) => ResizeStarted?.Invoke(corner);
            handle.DragDelta += (_, _) => Resizing?.Invoke(corner);
            handle.DragCompleted += (_, _) => ResizeCompleted?.Invoke(corner);
            _handles.Add(handle);
        }
    }

    private static ControlTemplate HandleTemplate(Brush accent)
    {
        var face = new FrameworkElementFactory(typeof(Border));
        face.SetValue(Border.BackgroundProperty, Brushes.White);
        face.SetValue(Border.BorderBrushProperty, accent);
        face.SetValue(Border.BorderThicknessProperty, new Thickness(1.5));
        face.SetValue(Border.CornerRadiusProperty, new CornerRadius(2));
        return new ControlTemplate(typeof(Thumb)) { VisualTree = face };
    }

    protected override int VisualChildrenCount => _handles.Count;

    protected override Visual GetVisualChild(int index) => _handles[index];

    protected override Size MeasureOverride(Size constraint)
    {
        foreach (UIElement handle in _handles) handle.Measure(new Size(HandleSize, HandleSize));
        return AdornedElement.RenderSize;
    }

    /// <summary>Each handle centred on its corner of the outline. The adorner is arranged at the widget's size, so a
    /// widget that changes size is drawn again — label included — with nothing asked for here.</summary>
    protected override Size ArrangeOverride(Size finalSize)
    {
        var outline = OutlineOf(AdornedElement.RenderSize);
        foreach (Thumb handle in _handles)
        {
            var corner = (Corner)handle.Tag;
            var x = corner is Corner.TopLeft or Corner.BottomLeft ? outline.Left : outline.Right;
            var y = corner is Corner.TopLeft or Corner.TopRight ? outline.Top : outline.Bottom;
            handle.Arrange(new Rect(x - HandleSize / 2, y - HandleSize / 2, HandleSize, HandleSize));
        }
        return finalSize;
    }

    /// <summary>The outline's centre line: the gap, then half the stroke, outside the widget.</summary>
    private static Rect OutlineOf(Size widget)
    {
        var rect = new Rect(widget);
        rect.Inflate(Gap + Stroke / 2, Gap + Stroke / 2);
        return rect;
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        var size = AdornedElement.RenderSize;
        var outline = OutlineOf(size);
        drawingContext.DrawRoundedRectangle(null, _outline, outline, Radius, Radius);

        var dip = VisualTreeHelper.GetDpi(this).PixelsPerDip;
        var text = new FormattedText(
            string.Create(CultureInfo.InvariantCulture, $"{Math.Round(size.Width)} × {Math.Round(size.Height)}"),
            CultureInfo.CurrentUICulture, FlowDirection.LeftToRight,
            new Typeface(SystemFonts.MessageFontFamily, FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal),
            11, _onAccent, dip);
        var padlock = _shapeKept
            ? new FormattedText(LockGlyph, CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                new Typeface(Icons, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal), 11, _onAccent, dip)
            : null;
        var before = padlock is null ? 0 : padlock.Width + 4;

        var label = new Rect(outline.Left, outline.Bottom + Stroke / 2 + 6, before + text.Width + 12, text.Height + 4);
        drawingContext.DrawRoundedRectangle(_accent, null, label, 4, 4);
        if (padlock is not null)
            drawingContext.DrawText(padlock, new Point(label.Left + 6, label.Top + (label.Height - padlock.Height) / 2));
        drawingContext.DrawText(text, new Point(label.Left + 6 + before, label.Top + 2));
    }

    /// <summary>Nothing of its own is hit, so the outline never takes a press meant for the widget under it. The
    /// handles are tested before this, as children, and still take theirs.</summary>
    protected override HitTestResult? HitTestCore(PointHitTestParameters hitTestParameters) => null;
}
