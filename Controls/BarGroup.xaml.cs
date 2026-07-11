using System.Collections;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Aquila.Controls;

/// <summary>One bar in a <see cref="BarGroup"/> (e.g. one CPU core's load).</summary>
public sealed record BarGroupItem(string Label, double Value, double Max = 100)
{
    public string ValueText => $"{Value:F0}%";
}

/// <summary>
/// N independent vertical bars side by side (e.g. CPU per-core load) — sibling of
/// <see cref="SensorBar"/>/<see cref="StackedBar"/>, but for a row of separately-scaled values
/// instead of one sensor or a shared whole. Bind <see cref="Items"/> and <see cref="Accent"/>;
/// <see cref="BarHeight"/> caps the bars' pixel height.
/// </summary>
public partial class BarGroup : UserControl
{
    public BarGroup() => InitializeComponent();

    public static readonly DependencyProperty ItemsProperty =
        DependencyProperty.Register(nameof(Items), typeof(IEnumerable), typeof(BarGroup),
            new PropertyMetadata(null));

    public static readonly DependencyProperty AccentProperty =
        DependencyProperty.Register(nameof(Accent), typeof(Brush), typeof(BarGroup),
            new PropertyMetadata(null));

    public static readonly DependencyProperty BarHeightProperty =
        DependencyProperty.Register(nameof(BarHeight), typeof(double), typeof(BarGroup),
            new PropertyMetadata(92.0));

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

    /// <summary>Max pixel height of each bar's track. Default 92 (matches the CPU core-load look).</summary>
    public double BarHeight
    {
        get => (double)GetValue(BarHeightProperty);
        set => SetValue(BarHeightProperty, value);
    }
}
