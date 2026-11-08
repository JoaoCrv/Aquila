using System.Collections;
using System.Windows;
using System.Windows.Media;
using Aquila.Controls.Primitives;

namespace Aquila.Controls;

/// <summary>One coloured share of a <see cref="StackedBar"/>.</summary>
public sealed record BarSegment(double Fraction, Brush Color, double Opacity = 1.0);

/// <summary>
/// A bar split into N proportional coloured segments (e.g. RAM's physical/virtual pool composition — sibling of
/// <see cref="SensorBar"/>, but for several shares of a whole instead of one sensor against a scale). Supply
/// <see cref="BarSegment"/>s whose fractions sum to about one; parts past the whole are cut where the bar ends.
///
/// Drawn in one visual by <see cref="Draw.Segments"/> — a piece that draws several shapes calls the drawing itself,
/// as the drawing framework allows, rather than standing an element up per share. The shares travel to new ones at
/// the <see cref="Pace"/>, from wherever they are drawn; a change in how many there are is a different bar, and
/// jumps. It was a Grid with a star column per share, rebuilt on every reading.
/// </summary>
public sealed class StackedBar : FrameworkElement
{
    /// <summary>Its ends, round enough to make the cards' 8-pixel bar a pill.</summary>
    private const double Corner = 4;

    public static readonly DependencyProperty SegmentsProperty =
        DependencyProperty.Register(nameof(Segments), typeof(IEnumerable), typeof(StackedBar),
            new PropertyMetadata(null, (bar, _) => ((StackedBar)bar).Retarget()));

    /// <summary>The segments to render, left to right.</summary>
    public IEnumerable? Segments
    {
        get => (IEnumerable?)GetValue(SegmentsProperty);
        set => SetValue(SegmentsProperty, value);
    }

    /// <summary>The bar under the shares: the card's own fill, as it was — followed through the resource, so a
    /// change of theme reaches it.</summary>
    private static readonly DependencyProperty GroundProperty =
        DependencyProperty.Register("Ground", typeof(Brush), typeof(StackedBar),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    /// <summary>How far the shares are through their travel, 0 to 1; 1, its resting value, is arrived.</summary>
    private static readonly DependencyProperty TravelProperty =
        DependencyProperty.Register("Travel", typeof(double), typeof(StackedBar),
            new FrameworkPropertyMetadata(1d, FrameworkPropertyMetadataOptions.AffectsRender));

    private BarSegment[] _parts = [];
    private double[] _from = [];
    private double[] _to = [];

    public StackedBar()
    {
        SetResourceReference(GroundProperty, "CardBackgroundFillColorDefaultBrush");
    }

    /// <summary>The shares as drawn now: arrived, or part way from where they were.</summary>
    private double[] Shown()
    {
        var progress = (double)GetValue(TravelProperty);
        if (!(progress < 1)) return _to;

        var shown = new double[_to.Length];
        for (var i = 0; i < shown.Length; i++) shown[i] = _from[i] + (_to[i] - _from[i]) * progress;
        return shown;
    }

    private void Retarget()
    {
        var parts = Segments?.OfType<BarSegment>().ToArray() ?? [];
        var to = parts.Select(part => part.Fraction).ToArray();

        // The same shares again — a reading that changed nothing here — leave the travel alone.
        if (parts.Length == _parts.Length && to.SequenceEqual(_to))
        {
            _parts = parts;
            InvalidateVisual();
            return;
        }

        var travel = IsLoaded && Pace.Moves && parts.Length == _parts.Length && _parts.Length > 0;
        (_from, _to, _parts) = (travel ? Shown() : to, to, parts);

        if (travel)
        {
            BeginAnimation(TravelProperty, Pace.Progress());
        }
        else
        {
            BeginAnimation(TravelProperty, null);
            SetValue(TravelProperty, 1d);
            InvalidateVisual();
        }
    }

    protected override void OnRender(DrawingContext drawing)
    {
        if (_parts.Length == 0) return;

        var shown = Shown();
        var parts = new (double Share, Brush Fill, double Opacity)[_parts.Length];
        for (var i = 0; i < parts.Length; i++) parts[i] = (shown[i], _parts[i].Color, _parts[i].Opacity);

        Draw.Segments(drawing, new Rect(RenderSize), parts,
            new BarLook(double.NaN, Corner, (Brush?)GetValue(GroundProperty)));
    }
}
