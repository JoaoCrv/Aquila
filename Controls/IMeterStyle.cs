using Aquila.Models;

namespace Aquila.Controls;

/// <summary>
/// A piece drawn as a bar, and what is worth letting the user change about it. Third of the structural
/// style interfaces, beside <see cref="IChartStyle"/> and <see cref="IGaugeStyle"/>.
/// </summary>
public interface IMeterStyle : IValueStyle, IUnitStyle, IHideableValue
{
    double BarThickness { get; set; }
    double BarCorner { get; set; }
    MeterLayout Layout { get; set; }
}
