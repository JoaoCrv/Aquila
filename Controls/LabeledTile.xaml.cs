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
    /// While it shows, the line is a header rather than a label, laid out as the list's design lays its own: the title
    /// at the start of the line whatever its alignment says, since a title centred in what the aside leaves would sit
    /// nowhere in particular; and the header twelve pixels from what it heads, where a label sits four.
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

    /// <summary>A header's distance from what it heads, beyond a label's: the design's 12 against the 4 every title
    /// keeps.</summary>
    private const double HeaderGap = 8;

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
        // given a title, and it should not be a rectangle with a stripe missing from the top.
        TitleText.Visibility = string.IsNullOrEmpty(Title) ? Visibility.Collapsed : Visibility.Visible;

        DockPanel.SetDock(Header, TitlePlacement switch
        {
            TitlePlacement.Bottom => Dock.Bottom,
            TitlePlacement.Left   => Dock.Left,
            TitlePlacement.Right  => Dock.Right,
            _                     => Dock.Top,
        });

        // A little breathing room between title and content, on the docked side.
        TitleText.Margin = (TitlePlacement switch
        {
            TitlePlacement.Bottom => new Thickness(0, 4, 0, 0),
            TitlePlacement.Left   => new Thickness(0, 0, 6, 0),
            TitlePlacement.Right  => new Thickness(6, 0, 0, 0),
            _                     => new Thickness(0, 0, 0, 4),
        }).Scaled(_figureScale);

        // The aside keeps the same distance from the content as the title, and a gap from the title's end so a
        // long title's ellipsis never runs into it. Above or below, the two share a line and their bottoms meet,
        // which is as near as two sizes of text get to sharing a baseline; beside, the aside goes under the title
        // and the pair is centred as the title alone was.
        var sideways = TitlePlacement is TitlePlacement.Left or TitlePlacement.Right;

        DockPanel.SetDock(AsideHost, sideways ? Dock.Bottom : Dock.Right);
        Header.VerticalAlignment = sideways ? VerticalAlignment.Center : VerticalAlignment.Stretch;
        TitleText.VerticalAlignment = sideways ? VerticalAlignment.Center : VerticalAlignment.Bottom;

        AsideHost.Margin = (TitlePlacement switch
        {
            TitlePlacement.Bottom => new Thickness(8, 4, 0, 0),
            TitlePlacement.Left   => new Thickness(0, 2, 6, 0),
            TitlePlacement.Right  => new Thickness(6, 2, 0, 0),
            _                     => new Thickness(8, 0, 0, 4),
        }).Scaled(_figureScale);

        AsideHost.Visibility = HasAside ? Visibility.Visible : Visibility.Collapsed;

        // A header stands further off than a label, on the side it heads. Beside the content there is no line to
        // speak of, so nothing changes there.
        Header.Margin = (HasAside ? TitlePlacement switch
        {
            TitlePlacement.Top    => new Thickness(0, 0, 0, HeaderGap),
            TitlePlacement.Bottom => new Thickness(0, HeaderGap, 0, 0),
            _                     => new Thickness(0),
        } : new Thickness(0)).Scaled(_figureScale);
    }
}
