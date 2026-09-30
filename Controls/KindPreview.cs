using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Aquila.Models;
using Aquila.Services;

namespace Aquila.Controls;

/// <summary>
/// A widget kind drawing itself, small, in the editor's type picker.
///
/// Built through <see cref="WidgetKindInfo.Create"/> — the same factory that draws the widgets on the desktop
/// — and that is the entire point. The Widgets page used to carry a gallery of these written by hand: four
/// cards, copied out one at a time, while the catalog grew to nine kinds. It went stale the way anything
/// hand-written against a list goes stale, and nothing told anyone. A preview built from the catalog cannot:
/// a kind added there arrives here already drawn.
///
/// Coloured through the kind's own <see cref="SeriesPaint"/>, the public contract for where each reading's
/// colour goes, so this never needs to know what a gauge's accent property happens to be called.
///
/// Built at the kind's own default size and left for a Viewbox to shrink. Each kind keeps its proportions —
/// a meter is a strip, a gauge is nearly square — which is most of what tells them apart at a glance.
///
/// Not hit-testable and not focusable: it is a picture of a choice, and the list item around it is the
/// thing you click.
/// </summary>
public sealed class KindPreview : ContentControl
{
    public static readonly DependencyProperty KindProperty = DependencyProperty.Register(
        nameof(Kind), typeof(WidgetKindInfo), typeof(KindPreview), new PropertyMetadata(null, OnChanged));

    public static readonly DependencyProperty SensorsProperty = DependencyProperty.Register(
        nameof(Sensors), typeof(IReadOnlyList<SensorNode>), typeof(KindPreview), new PropertyMetadata(null, OnChanged));

    public static readonly DependencyProperty AccentProperty = DependencyProperty.Register(
        nameof(Accent), typeof(Brush), typeof(KindPreview), new PropertyMetadata(null, OnAccentChanged));

    public WidgetKindInfo? Kind
    {
        get => (WidgetKindInfo?)GetValue(KindProperty);
        set => SetValue(KindProperty, value);
    }

    /// <summary>What every kind is shown reading. One list for all of them, each taking as many as it can
    /// draw — decided by the kind's own <see cref="WidgetKindInfo.MaxSeries"/>, never by asking which kind
    /// it is, so a new kind needs nothing here.</summary>
    public IReadOnlyList<SensorNode>? Sensors
    {
        get => (IReadOnlyList<SensorNode>?)GetValue(SensorsProperty);
        set => SetValue(SensorsProperty, value);
    }

    public Brush? Accent
    {
        get => (Brush?)GetValue(AccentProperty);
        set => SetValue(AccentProperty, value);
    }

    public KindPreview()
    {
        IsHitTestVisible = false;
        Focusable = false;
    }

    /// <summary>How many readings the current piece was built with, so a repaint reaches exactly those.</summary>
    private int _painted;

    private static void OnChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) =>
        ((KindPreview)d).Rebuild();

    /// <summary>
    /// A new colour repaints; it never rebuilds.
    ///
    /// The widget editor's own rule, and for the same reason: rebuilding a chart throws away its series and
    /// Skia paints and restarts the line from empty. Here it would do that nine times over for every notch
    /// of a colour being dragged in the preset below — the colour is the one thing that changes often, so it
    /// is the one thing that must be cheap.
    /// </summary>
    private static void OnAccentChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) =>
        ((KindPreview)d).Repaint();

    private void Rebuild()
    {
        _painted = 0;

        if (Kind is not { } kind)
        {
            Content = null;
            return;
        }

        var sample = Sensors ?? [];
        var count = Math.Min(kind.MaxSeries, sample.Count);

        // Create's own contract is that a kind needing a reading is never handed an empty list. With nothing
        // to read — the source down, say — the cell stays empty rather than throwing inside a list item,
        // where the exception would take the whole editor with it.
        if (kind.NeedsReading && count == 0)
        {
            Content = null;
            return;
        }

        var piece = kind.Create([.. sample.Take(count)]);
        piece.Width = kind.DefaultWidth;
        piece.Height = kind.DefaultHeight;

        Content = piece;
        _painted = count;
        Repaint();
    }

    private void Repaint()
    {
        if (Kind is not { } kind || Content is not FrameworkElement piece || Accent is not { } accent) return;

        for (var i = 0; i < _painted; i++)
            kind.Accent.For(piece, i)(accent);
    }
}
