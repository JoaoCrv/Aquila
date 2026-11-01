using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using Aquila.Models;

namespace Aquila.Controls;

/// <summary>
/// Wraps any content (a gauge, sparkline, ...) with a title placed on one side. Solves the cramped
/// label inside the gauge by moving it outside. How the title is drawn comes from the preset, so every
/// widget wearing one agrees about what a label looks like.
/// </summary>
public partial class LabeledTile : UserControl, IScaledFigure
{
    public LabeledTile()
    {
        InitializeComponent();
        ShowAside();
        Apply();
    }

    private double _figureScale = 1;

    /// <summary>The widget's scale, for the gap between the title and what it labels — the title's size arrives
    /// already scaled, and a heading twice the size sitting four pixels from its dial reads as touching it.</summary>
    public double FigureScale
    {
        get => _figureScale;
        set { _figureScale = value; Apply(); }
    }

    public static readonly DependencyProperty TitleProperty =
        DependencyProperty.Register(nameof(Title), typeof(string), typeof(LabeledTile),
            new PropertyMetadata(string.Empty, (d, _) => ((LabeledTile)d).Apply()));

    public static readonly DependencyProperty TitlePlacementProperty =
        DependencyProperty.Register(nameof(TitlePlacement), typeof(TitlePlacement), typeof(LabeledTile),
            new PropertyMetadata(TitlePlacement.Top, (d, _) => ((LabeledTile)d).Apply()));

    public static readonly DependencyProperty TitleBrushProperty =
        DependencyProperty.Register(nameof(TitleBrush), typeof(Brush), typeof(LabeledTile),
            new PropertyMetadata(null, (d, _) => ((LabeledTile)d).Apply()));

    /// <summary>The label's own colour. Deliberately not Foreground, which is what the content inherits:
    /// a title and the reading under it are allowed to differ, and usually should.</summary>
    public Brush? TitleBrush
    {
        get => (Brush?)GetValue(TitleBrushProperty);
        set => SetValue(TitleBrushProperty, value);
    }

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

    public static readonly DependencyProperty TitleAlignProperty =
        DependencyProperty.Register(nameof(TitleAlign), typeof(TextAlign), typeof(LabeledTile),
            new PropertyMetadata(TextAlign.Center, (d, _) => ((LabeledTile)d).Apply()));

    /// <summary>Which end of its line the label sits at.</summary>
    public TextAlign TitleAlign
    {
        get => (TextAlign)GetValue(TitleAlignProperty);
        set => SetValue(TitleAlignProperty, value);
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

    public static readonly DependencyProperty AsideProperty =
        DependencyProperty.Register(nameof(Aside), typeof(FrameworkElement), typeof(LabeledTile),
            new PropertyMetadata(null, (d, _) => ((LabeledTile)d).ShowAside()));

    /// <summary>
    /// Something of the piece's at the far end of the title's line — see <see cref="ITitleAside"/>. At the end of the
    /// line when the title is above or below, under the title when it is beside.
    ///
    /// Given room only while it is visible. Its visibility is the piece's to decide, so the tile follows it rather
    /// than being told: hidden, it takes nothing — not even the gap beside the title — and a widget whose piece has
    /// nothing to say there is drawn exactly as it was before there was an aside.
    ///
    /// While it shows, the title stands at the start of its line whatever its alignment says, since a title centred in
    /// what the aside leaves would sit nowhere in particular. Everything else about the line — its box, its distance
    /// from the content — is the same with an aside as without, so a list's heading is every widget's heading.
    /// </summary>
    public FrameworkElement? Aside
    {
        get => (FrameworkElement?)GetValue(AsideProperty);
        set => SetValue(AsideProperty, value);
    }

    /// <summary>The aside's own visibility, followed by binding — the way the tile hears the piece hide or show it.
    /// </summary>
    private static readonly DependencyProperty AsideShownProperty =
        DependencyProperty.Register("AsideShown", typeof(Visibility), typeof(LabeledTile),
            new PropertyMetadata(Visibility.Collapsed, (d, _) => ((LabeledTile)d).Apply()));

    private bool HasAside => (Visibility)GetValue(AsideShownProperty) == Visibility.Visible;

    private void ShowAside()
    {
        AsideHost.Content = Aside;

        if (Aside is null) BindingOperations.ClearBinding(this, AsideShownProperty);
        else SetBinding(AsideShownProperty, new Binding(nameof(Visibility)) { Source = Aside });

        Apply();
    }

    private (Brush? Background, Brush? Border, double Thickness, double Corner, double Padding, double Gap) _box =
        (null, null, 0, 0, 0, 4);

    /// <summary>
    /// The title line's own box — a background and an outline round the title and its aside, and the room inside
    /// them — and its distance from the content. See <see cref="PresetTitle.Background"/>.
    ///
    /// Unscaled, like the margins here: the tile puts all of it through the widget's scale, so the box grows with
    /// the title it holds. A size below nought or not a number is nought — the preset is a file anyone can write,
    /// and WPF refuses a negative border outright, which would take the widget down with it.
    /// </summary>
    public void Box(Brush? background, Brush? border, double thickness, double corner, double padding, double gap)
    {
        _box = (background, border, Size(thickness), Size(corner), Size(padding), Size(gap));
        Apply();
    }

    private (double Inset, double Corner)? _edges;

    /// <summary>
    /// Takes the box out to the panel's edge — see <see cref="PresetTitle.Bleed"/>: how far in the panel's padding sets
    /// the tile, and how round the panel's corners are on the inside. Null keeps the box within the padding, as every
    /// title was. Unscaled, like <see cref="Box"/>.
    ///
    /// The tile cannot see the panel, so it is told; and it reaches the edge with negative margins, which WPF draws
    /// past the tile's own bounds — nothing between the tile and the panel clips.
    /// </summary>
    public void Edges(double inset, double corner)
    {
        _edges = (Size(inset), Size(corner));
        Apply();
    }

    /// <summary>Back within the panel's padding.</summary>
    public void NoEdges()
    {
        _edges = null;
        Apply();
    }

    private static double Size(double value) => double.IsFinite(value) && value > 0 ? value : 0;

    private void Apply()
    {
        TitleText.Text = Title;
        TitleText.FontSize = TitleSize;
        TitleText.Wear(TitleFont, TitleWeight);

        // Cleared rather than left holding the last brush, so a tile told nothing falls back to what it
        // inherits instead of keeping a colour from the preset before it.
        if (TitleBrush is null) TitleText.ClearValue(TextBlock.ForegroundProperty);
        else TitleText.Foreground = TitleBrush;
        TitleText.Opacity = TitleOpacity;

        // Does nothing while the title is docked to a side: there the TextBlock is only as wide as its
        // own text, so there is no room to move it in. Left as it is rather than hidden from the editor,
        // because a setting that appears and disappears with an unrelated one is harder to trust than a
        // setting that visibly has no effect yet.
        TitleText.HorizontalAlignment = TitleAlign switch
        {
            _ when HasAside => HorizontalAlignment.Left,
            TextAlign.Left  => HorizontalAlignment.Left,
            TextAlign.Right => HorizontalAlignment.Right,
            _               => HorizontalAlignment.Center,
        };
        Body.Content = Tile;

        // An empty title still occupies a line, which a piece with nothing above it would wear as a gap.
        // Matters most to the kinds that draw no reading: a backdrop is a plain rectangle unless it is
        // given a title, and it should not be a rectangle with a stripe missing from the top. The box goes
        // with what is in it: with no title and no aside there is nothing to draw a box round, nor a distance
        // to keep from the content.
        var titled = !string.IsNullOrEmpty(Title);
        TitleText.Visibility = titled ? Visibility.Visible : Visibility.Collapsed;
        TitleBox.Visibility = titled || HasAside ? Visibility.Visible : Visibility.Collapsed;

        DockPanel.SetDock(TitleBox, TitlePlacement switch
        {
            TitlePlacement.Bottom => Dock.Bottom,
            TitlePlacement.Left   => Dock.Left,
            TitlePlacement.Right  => Dock.Right,
            _                     => Dock.Top,
        });

        var (background, border, thickness, corner, padding, gap) = _box;
        TitleBox.Background = background;
        TitleBox.BorderBrush = border;
        TitleBox.BorderThickness = new Thickness(thickness).Scaled(_figureScale);
        TitleBox.Padding = new Thickness(padding).Scaled(_figureScale);

        // Out to the panel's edge, the three outer sides go back by the panel's padding and their corners become the
        // panel's; the side facing the content keeps the title's own. Within the padding, every corner is the title's.
        var (inset, outer) = _edges ?? (0d, corner);

        TitleBox.CornerRadius = (TitlePlacement switch
        {
            TitlePlacement.Bottom => new CornerRadius(corner, corner, outer, outer),
            TitlePlacement.Left   => new CornerRadius(outer, corner, corner, outer),
            TitlePlacement.Right  => new CornerRadius(corner, outer, outer, corner),
            _                     => new CornerRadius(outer, outer, corner, corner),
        }).Scaled(_figureScale);

        // The distance from the content, outside the box, on the docked side — so a box with a background stands
        // clear of what it heads instead of touching it.
        TitleBox.Margin = (TitlePlacement switch
        {
            TitlePlacement.Bottom => new Thickness(-inset, gap, -inset, -inset),
            TitlePlacement.Left   => new Thickness(-inset, -inset, gap, -inset),
            TitlePlacement.Right  => new Thickness(gap, -inset, -inset, -inset),
            _                     => new Thickness(-inset, -inset, -inset, gap),
        }).Scaled(_figureScale);

        // The aside keeps a gap from the title's end so a long title's ellipsis never runs into it. Above or below,
        // the two share a line and their bottoms meet, which is as near as two sizes of text get to sharing a
        // baseline; beside, the aside goes under the title and the box is centred as the title alone was.
        var sideways = TitlePlacement is TitlePlacement.Left or TitlePlacement.Right;

        DockPanel.SetDock(AsideHost, sideways ? Dock.Bottom : Dock.Right);
        TitleBox.VerticalAlignment = sideways ? VerticalAlignment.Center : VerticalAlignment.Stretch;
        TitleText.VerticalAlignment = sideways ? VerticalAlignment.Center : VerticalAlignment.Bottom;

        AsideHost.Margin = (sideways ? new Thickness(0, 2, 0, 0) : new Thickness(8, 0, 0, 0)).Scaled(_figureScale);
        AsideHost.Visibility = HasAside ? Visibility.Visible : Visibility.Collapsed;
    }
}
