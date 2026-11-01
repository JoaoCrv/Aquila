using Aquila.Models;

namespace Aquila.Controls;

/// <summary>
/// A piece drawn as a bar, and what is worth letting the user change about it. Third of the structural
/// style interfaces, beside <see cref="IChartStyle"/> and <see cref="IGaugeStyle"/>.
/// </summary>
public interface IMeterStyle : IBarStyle, IValueStyle, IUnitStyle, IHideableValue
{
    MeterLayout Layout { get; set; }
}

/// <summary>
/// A piece drawn with bars, and the preset's <c>bar</c> section it takes them from: how thick, how round at the
/// ends, and the track under them.
///
/// Its own interface since the list's rows, which carry a bar each and nothing else of a meter's: no reading is laid
/// out beside them, and there is no reading to hide, since a row's number is the row. The split
/// <see cref="IArcStyle"/> made for the clock's rings, for the same reason — a piece takes exactly what it draws, and
/// two pieces drawing the same bar cannot come to disagree about what the preset's thickness means.
/// </summary>
public interface IBarStyle
{
    double BarThickness { get; set; }
    double BarCorner { get; set; }

    /// <summary>The unfilled part of the bar, from the widget's own preset. Unset, a bar resolves
    /// Aquila.Scheme.Track — the DASHBOARD'S track, which is right on a card and was wrong on the desktop:
    /// a widget wearing another preset drew its fill in its own colours over the dashboard's track, while
    /// a dial beside it, which has always taken its track from its own preset, did not.</summary>
    System.Windows.Media.Brush? TrackBrush { get; set; }
}
