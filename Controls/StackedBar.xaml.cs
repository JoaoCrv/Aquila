using System.Collections;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Aquila.Controls;

/// <summary>One coloured share of a <see cref="StackedBar"/>.</summary>
public sealed record BarSegment(double Fraction, Brush Color, double Opacity = 1.0);

/// <summary>
/// A bar split into N proportional coloured segments (e.g. RAM's physical/virtual pool composition —
/// sibling of <see cref="SensorBar"/>, but for several shares of a whole instead of one sensor against
/// a scale). Segments always fill the full width; built with Grid star-sizing, so there is no custom
/// width math — supply <see cref="BarSegment"/>s whose fractions sum to ~1.
/// </summary>
public partial class StackedBar : UserControl
{
    public StackedBar() => InitializeComponent();

    public static readonly DependencyProperty SegmentsProperty =
        DependencyProperty.Register(nameof(Segments), typeof(IEnumerable), typeof(StackedBar),
            new PropertyMetadata(null, (d, _) => ((StackedBar)d).Rebuild()));

    /// <summary>The segments to render, left to right.</summary>
    public IEnumerable? Segments
    {
        get => (IEnumerable?)GetValue(SegmentsProperty);
        set => SetValue(SegmentsProperty, value);
    }

    private void Rebuild()
    {
        SegmentsHost.ColumnDefinitions.Clear();
        SegmentsHost.Children.Clear();

        if (Segments is null) return;

        foreach (var item in Segments)
        {
            if (item is not BarSegment segment) continue;

            int column = SegmentsHost.ColumnDefinitions.Count;
            SegmentsHost.ColumnDefinitions.Add(new ColumnDefinition
            {
                Width = new GridLength(System.Math.Max(segment.Fraction, 0), GridUnitType.Star)
            });

            var border = new Border { Background = segment.Color, Opacity = segment.Opacity };
            Grid.SetColumn(border, column);
            SegmentsHost.Children.Add(border);
        }
    }
}
