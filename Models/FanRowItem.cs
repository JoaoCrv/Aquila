namespace Aquila.Models;

/// <summary>
/// Pairs a physical Fan sensor with its duty-cycle (Control) sensor, for one fan row in the Fans
/// card. Shared model so both the ViewModel and the FansCard control can reference it.
/// </summary>
public sealed class FanRowItem(SensorNode fan, SensorNode? control)
{
    public SensorNode  Fan        => fan;
    public SensorNode? Control    => control;
    public double      BarValue   => control?.Value ?? fan.Value ?? 0;
    public double      BarMaximum => control != null ? 100 : (fan.Max ?? 3000);

    /// <summary>
    /// How full the bar is, 0–100, whatever it is measuring.
    ///
    /// Exists so colour can mean the same thing on every row. A fan reporting a control signal fills the
    /// bar with a percentage; one reporting only RPM fills it with a speed against its own observed
    /// ceiling. Colouring the raw <see cref="BarValue"/> would read 1256 rpm as a percentage and call
    /// every such fan critical.
    /// </summary>
    public double BarPercent => BarMaximum > 0 ? BarValue / BarMaximum * 100 : 0;

    public string? DutyText
    {
        get
        {
            if (control?.Value is float cv) return $"{cv:F0}%";
            if (fan.Value is float v && fan.Max is float max && max > 0)
                return $"{v / max * 100:F0}%";
            return null;
        }
    }
}
