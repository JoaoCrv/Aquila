using System.Windows;
using System.Windows.Controls;
using Aquila.Models;

namespace Aquila.Controls.Cards;

/// <summary>
/// Self-contained power card: total system power, per-source rows (CPU / GPU / GPU 2) and a
/// total-power trend sparkline. Bind <see cref="TotalPower"/>, <see cref="CpuPower"/>,
/// <see cref="Gpu1Power"/> and (optionally) <see cref="Gpu2Power"/> — the GPU 2 row collapses when
/// unset. Pure bindings — no computed state or ViewModel dependency.
/// </summary>
public partial class PowerCard : UserControl
{
    public PowerCard() => InitializeComponent();

    public static readonly DependencyProperty TotalPowerProperty =
        DependencyProperty.Register(nameof(TotalPower), typeof(SensorNode), typeof(PowerCard),
            new PropertyMetadata(null));

    public static readonly DependencyProperty CpuPowerProperty =
        DependencyProperty.Register(nameof(CpuPower), typeof(SensorNode), typeof(PowerCard),
            new PropertyMetadata(null));

    public static readonly DependencyProperty Gpu1PowerProperty =
        DependencyProperty.Register(nameof(Gpu1Power), typeof(SensorNode), typeof(PowerCard),
            new PropertyMetadata(null));

    public static readonly DependencyProperty Gpu2PowerProperty =
        DependencyProperty.Register(nameof(Gpu2Power), typeof(SensorNode), typeof(PowerCard),
            new PropertyMetadata(null));

    /// <summary>Total system power sensor, shown big and trended below.</summary>
    public SensorNode? TotalPower
    {
        get => (SensorNode?)GetValue(TotalPowerProperty);
        set => SetValue(TotalPowerProperty, value);
    }

    /// <summary>CPU package power sensor.</summary>
    public SensorNode? CpuPower
    {
        get => (SensorNode?)GetValue(CpuPowerProperty);
        set => SetValue(CpuPowerProperty, value);
    }

    /// <summary>Primary GPU package power sensor.</summary>
    public SensorNode? Gpu1Power
    {
        get => (SensorNode?)GetValue(Gpu1PowerProperty);
        set => SetValue(Gpu1PowerProperty, value);
    }

    /// <summary>Secondary GPU package power sensor. The GPU 2 row collapses when unset.</summary>
    public SensorNode? Gpu2Power
    {
        get => (SensorNode?)GetValue(Gpu2PowerProperty);
        set => SetValue(Gpu2PowerProperty, value);
    }
}
