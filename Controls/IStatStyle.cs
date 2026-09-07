namespace Aquila.Controls;

/// <summary>
/// A piece that is just a number, and what is worth letting the user change about it. Fourth and last of
/// the structural style interfaces, beside <see cref="IChartStyle"/>, <see cref="IGaugeStyle"/> and
/// <see cref="IMeterStyle"/>.
/// </summary>
public interface IStatStyle : IValueStyle, IUnitStyle
{
    /// <summary>How big the unit is drawn beside the number. Only the stat has this: it is the one piece
    /// that draws the unit as a run of its own, so it is the one piece where a second size can mean
    /// anything. The others write value and unit into a single string at a single size.</summary>
    double UnitSize { get; set; }

    /// <summary>Whether the piece draws its own rounded fill. A desktop widget already has a styled panel
    /// of its own, so a second one inside it reads as a box in a box.</summary>
    bool ShowPanel { get; set; }
}
