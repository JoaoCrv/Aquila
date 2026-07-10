using System.Collections;
using System.Windows;
using System.Windows.Controls;
using Aquila.Models;

namespace Aquila.Controls.Cards;

/// <summary>
/// Self-contained temperatures card: a grid of temperature stat boxes (reusing <see cref="StatBox"/>)
/// plus a 60s trend sparkline. Bind <see cref="Temperatures"/> (the sensors to list, e.g. the
/// motherboard temperatures) and <see cref="DieTemperature"/> (the sensor to chart, e.g. the CPU die).
/// Everything binds straight to the SensorNodes, so there is no computed state or ViewModel dependency.
/// </summary>
public partial class TemperaturesCard : UserControl
{
    public TemperaturesCard() => InitializeComponent();

    public static readonly DependencyProperty TemperaturesProperty =
        DependencyProperty.Register(nameof(Temperatures), typeof(IEnumerable), typeof(TemperaturesCard),
            new PropertyMetadata(null));

    public static readonly DependencyProperty DieTemperatureProperty =
        DependencyProperty.Register(nameof(DieTemperature), typeof(SensorNode), typeof(TemperaturesCard),
            new PropertyMetadata(null));

    /// <summary>The temperature sensors to list (e.g. the motherboard temperatures).</summary>
    public IEnumerable? Temperatures
    {
        get => (IEnumerable?)GetValue(TemperaturesProperty);
        set => SetValue(TemperaturesProperty, value);
    }

    /// <summary>The sensor whose 60s trend is charted below (the CPU die temperature).</summary>
    public SensorNode? DieTemperature
    {
        get => (SensorNode?)GetValue(DieTemperatureProperty);
        set => SetValue(DieTemperatureProperty, value);
    }
}
