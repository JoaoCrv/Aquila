using System.Collections;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Aquila.Controls.Primitives;

namespace Aquila.Controls;

/// <summary>One bar in a <see cref="BarGroup"/> (e.g. one CPU core's load).</summary>
public sealed record BarGroupItem(string Label, double Value, double Max = 100)
{
    public string ValueText => $"{Value:F0}%";
}

/// <summary>
/// N independent vertical bars side by side (e.g. CPU per-core load) — sibling of <see cref="SensorBar"/>/
/// <see cref="StackedBar"/>, but for a row of separately-scaled values instead of one sensor or a shared whole. Bind
/// <see cref="Items"/> and <see cref="Accent"/>; <see cref="BarHeight"/> is each bar's height.
///
/// Each bar is a <see cref="BarFill"/> as wide as its column, kept from reading to reading: the card hands over new
/// items every tick, and the columns are matched to them by position and updated in place — so a core's bar travels
/// to its new load at the pace instead of being made again at it, and nothing is built after the first reading but
/// for a change in the number of cores.
/// </summary>
public partial class BarGroup : UserControl, IFollowsSchemeTrack
{
    /// <summary>A bar's ends, as round as the cards' core bars always were.</summary>
    private const double Corner = 4;

    /// <summary>A bar a little lighter than its accent, so sixteen of them side by side read as a texture, not a wall.
    /// </summary>
    private const double FillOpacity = 0.85;

    private sealed record Column(StackPanel Root, TextBlock Label, BarFill Bar, TextBlock Value);

    private readonly List<Column> _columns = [];
    private Brush? _fill;

    public BarGroup()
    {
        InitializeComponent();
        SchemeTrack.Follow(this);
    }

    public static readonly DependencyProperty ItemsProperty =
        DependencyProperty.Register(nameof(Items), typeof(IEnumerable), typeof(BarGroup),
            new PropertyMetadata(null, (group, _) => ((BarGroup)group).Sync()));

    public static readonly DependencyProperty AccentProperty =
        DependencyProperty.Register(nameof(Accent), typeof(Brush), typeof(BarGroup),
            new PropertyMetadata(null, (group, _) => ((BarGroup)group).Paint()));

    public static readonly DependencyProperty BarHeightProperty =
        DependencyProperty.Register(nameof(BarHeight), typeof(double), typeof(BarGroup),
            new PropertyMetadata(92.0, (group, _) => ((BarGroup)group).Dress()));

    /// <summary>The bars to render, left to right.</summary>
    public IEnumerable? Items
    {
        get => (IEnumerable?)GetValue(ItemsProperty);
        set => SetValue(ItemsProperty, value);
    }

    /// <summary>Fill colour for every bar.</summary>
    public Brush? Accent
    {
        get => (Brush?)GetValue(AccentProperty);
        set => SetValue(AccentProperty, value);
    }

    /// <summary>The pixel height of each bar's track. Default 92 (matches the CPU core-load look).</summary>
    public double BarHeight
    {
        get => (double)GetValue(BarHeightProperty);
        set => SetValue(BarHeightProperty, value);
    }

    void IFollowsSchemeTrack.SchemeTrackChanged() => Dress();

    /// <summary>
    /// The columns matched to the items by position: added or removed only when the count changes, and every one
    /// updated in place. A column just made is put at its reading; the rest travel there.
    /// </summary>
    private void Sync()
    {
        var items = Items?.OfType<BarGroupItem>().ToList() ?? [];

        while (_columns.Count > items.Count)
        {
            Columns.Children.Remove(_columns[^1].Root);
            _columns.RemoveAt(_columns.Count - 1);
        }

        while (_columns.Count < items.Count)
        {
            var column = Make();
            _columns.Add(column);
            Columns.Children.Add(column.Root);
        }

        for (var i = 0; i < items.Count; i++)
        {
            var (column, item) = (_columns[i], items[i]);
            column.Label.Text = item.Label;
            column.Value.Text = item.ValueText;
            column.Bar.MoveTo(item.Max > 0 ? item.Value / item.Max : 0, IsLoaded);
        }
    }

    private Column Make()
    {
        var label = new TextBlock
        {
            FontSize = 10, Opacity = 0.45, HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 0, 0, 2),
        };

        var bar = new BarFill
        {
            Orientation = Orientation.Vertical,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Margin = new Thickness(4, 0, 4, 0),
            Height = BarHeight,
            Look = Look(),
            Fill = _fill,
        };

        var value = new TextBlock
        {
            FontSize = 9, Opacity = 0.50, HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 2, 0, 0),
        };

        var root = new StackPanel();
        root.Children.Add(label);
        root.Children.Add(bar);
        root.Children.Add(value);
        return new Column(root, label, bar, value);
    }

    /// <summary>As wide as its column, round at the ends, over the dashboard's track.</summary>
    private BarLook Look() => SchemeTrack.Resolve(this, null, new BarLook(double.NaN, Corner, null));

    private void Dress()
    {
        var look = Look();
        foreach (var column in _columns)
        {
            column.Bar.Look = look;
            column.Bar.Height = BarHeight;
        }
    }

    /// <summary>The accent, a little lighter. Made once per accent, not per bar or per reading.</summary>
    private void Paint()
    {
        if (Accent is { } accent)
        {
            var lighter = accent.CloneCurrentValue();
            lighter.Opacity = accent.Opacity * FillOpacity;
            if (lighter.CanFreeze) lighter.Freeze();
            _fill = lighter;
        }
        else
        {
            _fill = null;
        }

        foreach (var column in _columns) column.Bar.Fill = _fill;
    }
}
