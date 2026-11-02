using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace Aquila.DesktopSurface;

/// <summary>
/// A widget pointed at from somewhere else — a row of the edit bar's widget list — drawn so it can be found: a
/// dashed outline with a faint wash inside, and a label naming it above. Dashed, where the selection is solid,
/// because it is a question ("this one?") and not a choice; the wash is what finds a widget hidden behind another.
///
/// Drawn on top of the widget's other outlines and never hit, so it changes nothing under the pointer. The colours
/// and the words are handed in: this folder knows no theme and no widget.
/// </summary>
internal sealed class HighlightAdorner : Adorner
{
    private const double Gap = 2;
    private const double Stroke = 2;
    private const double Radius = 10;

    private readonly Brush _accent;
    private readonly Brush _onAccent;
    private readonly Brush _wash;
    private readonly Pen _outline;
    private readonly string _label;

    public HighlightAdorner(UIElement widget, Brush accent, Brush onAccent, string label) : base(widget)
    {
        _accent = accent;
        _onAccent = onAccent;
        _label = label;
        IsHitTestVisible = false;

        _wash = accent.CloneCurrentValue();
        _wash.Opacity = 0.07;
        _wash.Freeze();

        _outline = new Pen(accent, Stroke) { DashStyle = new DashStyle([3, 2], 0) };
        _outline.Freeze();
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        var outline = new Rect(AdornedElement.RenderSize);
        outline.Inflate(Gap + Stroke / 2, Gap + Stroke / 2);
        drawingContext.DrawRoundedRectangle(_wash, _outline, outline, Radius, Radius);

        if (string.IsNullOrEmpty(_label)) return;

        var text = new FormattedText(_label, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight,
            new Typeface(SystemFonts.MessageFontFamily, FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal),
            11, _onAccent, VisualTreeHelper.GetDpi(this).PixelsPerDip);
        var size = new Size(text.Width + 12, text.Height + 4);

        // Above the widget, unless that is off the top of its screen — then just inside its top edge.
        var top = Canvas.GetTop(AdornedElement);
        var y = double.IsNaN(top) || top >= size.Height + 8
            ? outline.Top - Stroke / 2 - 6 - size.Height
            : outline.Top + Stroke / 2 + 6;

        var label = new Rect(new Point(outline.Left, y), size);
        drawingContext.DrawRoundedRectangle(_accent, null, label, 4, 4);
        drawingContext.DrawText(text, new Point(label.Left + 6, label.Top + 2));
    }

    protected override HitTestResult? HitTestCore(PointHitTestParameters hitTestParameters) => null;
}
