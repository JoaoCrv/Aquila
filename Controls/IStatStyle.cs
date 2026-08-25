namespace Aquila.Controls;

/// <summary>
/// A piece that is just a number, and what is worth letting the user change about it. Fourth and last of
/// the structural style interfaces, beside <see cref="IChartStyle"/>, <see cref="IGaugeStyle"/> and
/// <see cref="IMeterStyle"/>.
/// </summary>
public interface IStatStyle
{
    double ValueSize { get; set; }
    bool ShowUnit { get; set; }
    double UnitSize { get; set; }

    /// <summary>Whether the piece draws its own rounded fill. A desktop widget already has a styled panel
    /// of its own, so a second one inside it reads as a box in a box.</summary>
    bool ShowPanel { get; set; }
}
