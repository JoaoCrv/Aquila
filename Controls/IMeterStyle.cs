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

    /// <summary>The unfilled part of the bar, from the widget's own preset. Unset, a bar resolves
    /// Aquila.Scheme.Track — the DASHBOARD'S track, which is right on a card and was wrong on the desktop:
    /// a widget wearing another preset drew its fill in its own colours over the dashboard's track, while
    /// a dial beside it, which has always taken its track from its own preset, did not.</summary>
    System.Windows.Media.Brush? TrackBrush { get; set; }
}
