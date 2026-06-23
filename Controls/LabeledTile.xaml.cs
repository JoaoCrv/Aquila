using System.Windows;
using System.Windows.Controls;

namespace Aquila.Controls;

public enum TitlePlacement { Top, Bottom, Left, Right }

/// <summary>
/// Wraps any content (a gauge, sparkline, ...) with a title placed on one side. Solves the cramped
/// label inside the gauge by moving it outside. Title style inherits from the theme; only placement
/// is configurable.
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
        Body.Content = Tile;

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
