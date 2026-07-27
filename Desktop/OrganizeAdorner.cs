using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;

namespace Aquila.Desktop;

/// <summary>
/// The dashed outline drawn around each widget while organization mode is on — the app-wide "this is
/// editable now" vocabulary (#30), shared with the card editor's missing-sensor placeholder and screen
/// recovery. An adorner rather than a border on the widget itself, so the widgets stay untouched: they're
/// arbitrary elements supplied by the domain side, and organization mode must not depend on their type.
/// </summary>
internal sealed class OrganizeAdorner(UIElement adornedElement) : Adorner(adornedElement)
{
    private static readonly Pen _pen = CreatePen();

    private static Pen CreatePen()
    {
        var pen = new Pen(new SolidColorBrush(Color.FromArgb(0xDD, 0xFF, 0xFF, 0xFF)), 1)
        {
            DashStyle = DashStyles.Dash,
        };
        pen.Freeze();
        return pen;
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        // Inset by half the pen width so the stroke lands inside the widget's bounds instead of straddling
        // them (a straddling dashed line looks misaligned against the rounded backing panels).
        var rect = new Rect(AdornedElement.RenderSize);
        rect.Inflate(-0.5, -0.5);
        drawingContext.DrawRoundedRectangle(null, _pen, rect, 8, 8);
    }
}
