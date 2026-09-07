using System.Windows;
using System.Windows.Controls;
using Aquila.Models;

namespace Aquila.Controls;

/// <summary>
/// Wraps any content (a gauge, sparkline, ...) with a title placed on one side. Solves the cramped
/// label inside the gauge by moving it outside. How the title is drawn comes from the preset, so every
/// widget wearing one agrees about what a label looks like.
/// </summary>
public partial class LabeledTile : UserControl
{
    public LabeledTile()
    {
        InitializeComponent();
        Apply();
    }

    public static readonly DependencyProperty TitleProperty =
        DependencyProperty.Register(nameof(Title), typeof(string), typeof(LabeledTile),
            new PropertyMetadata(string.Empty, (d, _) => ((LabeledTile)d).Apply()));

    public static readonly DependencyProperty TitlePlacementProperty =
        DependencyProperty.Register(nameof(TitlePlacement), typeof(TitlePlacement), typeof(LabeledTile),
            new PropertyMetadata(TitlePlacement.Top, (d, _) => ((LabeledTile)d).Apply()));

    public static readonly DependencyProperty TitleFontProperty =
        DependencyProperty.Register(nameof(TitleFont), typeof(string), typeof(LabeledTile),
            new PropertyMetadata(null, (d, _) => ((LabeledTile)d).Apply()));

    public static readonly DependencyProperty TitleWeightProperty =
        DependencyProperty.Register(nameof(TitleWeight), typeof(TextWeight), typeof(LabeledTile),
            new PropertyMetadata(TextWeight.Regular, (d, _) => ((LabeledTile)d).Apply()));

    public string? TitleFont
    {
        get => (string?)GetValue(TitleFontProperty);
        set => SetValue(TitleFontProperty, value);
    }

    public TextWeight TitleWeight
    {
        get => (TextWeight)GetValue(TitleWeightProperty);
        set => SetValue(TitleWeightProperty, value);
    }

    public static readonly DependencyProperty TitleSizeProperty =
        DependencyProperty.Register(nameof(TitleSize), typeof(double), typeof(LabeledTile),
            new PropertyMetadata(11d, (d, _) => ((LabeledTile)d).Apply()));

    public static readonly DependencyProperty TitleOpacityProperty =
        DependencyProperty.Register(nameof(TitleOpacity), typeof(double), typeof(LabeledTile),
            new PropertyMetadata(0.6, (d, _) => ((LabeledTile)d).Apply()));

    public double TitleSize
    {
        get => (double)GetValue(TitleSizeProperty);
        set => SetValue(TitleSizeProperty, value);
    }

    public double TitleOpacity
    {
        get => (double)GetValue(TitleOpacityProperty);
        set => SetValue(TitleOpacityProperty, value);
    }

    public static readonly DependencyProperty TileProperty =
        DependencyProperty.Register(nameof(Tile), typeof(object), typeof(LabeledTile),
            new PropertyMetadata(null, (d, _) => ((LabeledTile)d).Apply()));

    /// <summary>The title text shown beside the content.</summary>
    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    /// <summary>Which side the title sits on.</summary>
    public TitlePlacement TitlePlacement
    {
        get => (TitlePlacement)GetValue(TitlePlacementProperty);
        set => SetValue(TitlePlacementProperty, value);
    }

    /// <summary>The content to label (e.g. a RadialGauge).</summary>
    public object? Tile
    {
        get => GetValue(TileProperty);
        set => SetValue(TileProperty, value);
    }

    private void Apply()
    {
        TitleText.Text = Title;
        TitleText.FontSize = TitleSize;
        TitleText.Wear(TitleFont, TitleWeight);
        TitleText.Opacity = TitleOpacity;
        Body.Content = Tile;

        // An empty title still occupies a line, which a piece with nothing above it would wear as a gap.
        // Matters most to the kinds that draw no reading: a backdrop is a plain rectangle unless it is
        // given a title, and it should not be a rectangle with a stripe missing from the top.
        TitleText.Visibility = string.IsNullOrEmpty(Title) ? Visibility.Collapsed : Visibility.Visible;

        DockPanel.SetDock(TitleText, TitlePlacement switch
        {
            TitlePlacement.Bottom => Dock.Bottom,
            TitlePlacement.Left   => Dock.Left,
            TitlePlacement.Right  => Dock.Right,
            _                     => Dock.Top,
        });

        // A little breathing room between title and content, on the docked side.
        TitleText.Margin = TitlePlacement switch
        {
            TitlePlacement.Bottom => new Thickness(0, 4, 0, 0),
            TitlePlacement.Left   => new Thickness(0, 0, 6, 0),
            TitlePlacement.Right  => new Thickness(6, 0, 0, 0),
            _                     => new Thickness(0, 0, 0, 4),
        };
    }
}
