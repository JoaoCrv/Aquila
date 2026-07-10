using System.Collections;
using System.Windows;
using System.Windows.Controls;
using Aquila.Models;

namespace Aquila.Controls.Cards;

/// <summary>
/// Self-contained fans card: a row per fan (name, duty-cycle bar, RPM) plus a CPU-fan trend
/// sparkline. Bind <see cref="FanRows"/> (the <see cref="FanRowItem"/>s to list) and
/// <see cref="CpuFan"/> (the sensor to chart). Pure bindings — no computed state or ViewModel
/// dependency; it can be dropped into the dashboard or a future widget alike.
/// </summary>
public partial class FansCard : UserControl
{
    public FansCard() => InitializeComponent();

    public static readonly DependencyProperty FanRowsProperty =
        DependencyProperty.Register(nameof(FanRows), typeof(IEnumerable), typeof(FansCard),
            new PropertyMetadata(null));

    public static readonly DependencyProperty CpuFanProperty =
        DependencyProperty.Register(nameof(CpuFan), typeof(SensorNode), typeof(FansCard),
            new PropertyMetadata(null));

    /// <summary>The fan rows to list (<see cref="FanRowItem"/> instances).</summary>
    public IEnumerable? FanRows
    {
        get => (IEnumerable?)GetValue(FanRowsProperty);
        set => SetValue(FanRowsProperty, value);
    }

    /// <summary>The CPU fan sensor whose 60s trend is charted below.</summary>
    public SensorNode? CpuFan
    {
        get => (SensorNode?)GetValue(CpuFanProperty);
        set => SetValue(CpuFanProperty, value);
    }
}
