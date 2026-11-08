using System.Windows.Media;

namespace Aquila.Controls;

/// <summary>
/// A piece that is a number, and what the preset says about how it looks. Beside <see cref="IChartStyle"/>,
/// <see cref="IGaugeStyle"/> and <see cref="IMeterStyle"/>; its bar is dressed through <see cref="IBarStyle"/>, as every
/// bar is.
///
/// It drew its own rounded fill once, the dashboard card's, which a preset could turn on — the theme's card colour
/// inside a panel the preset had already coloured, and a box in a box. Gone with the Number's own piece
/// (docs/widget-numero), and with it the last colour on a desktop widget that no preset could reach.
/// </summary>
public interface INumberStyle : IValueStyle, IUnitStyle
{
    /// <summary>How big the unit is drawn beside the number. Only the Number has this: it is the one piece that draws
    /// the unit as a run of its own, so it is the one piece where a second size can mean anything. The others write
    /// value and unit into a single string at a single size.</summary>
    double UnitSize { get; set; }

    /// <summary>How strongly the unit wears the number's colour, from 0 to 1. The unit is the same reading as the
    /// number, so it takes the number's colour — the theme's grey it wore before was the other colour no preset could
    /// reach — and stands back from it by this much.</summary>
    double UnitOpacity { get; set; }

    /// <summary>What the notes under the number are written in — the session's lowest and highest, the trend arrow and
    /// the bar's marks: the title's colour at the title's opacity. They say something ABOUT the reading, as a title
    /// does, so they wear the title's colour and not the ramp's.</summary>
    Brush? NoteBrush { get; set; }

    /// <summary>How big the notes are written: the title's size, in the number's face.</summary>
    double NoteSize { get; set; }
}

/// <summary>
/// The parts a Number draws besides its reading, set from the widget and never from the preset, as a list's are
/// (<see cref="IListStyle"/>): each is something to read, and trying on a preset must never take one away. How they
/// look is the preset's.
/// </summary>
public interface INumberParts
{
    /// <summary>Whether a bar under the number shows how far the reading is into its limits. A reading with none —
    /// watts, clocks, throughput — draws no bar whatever this says.</summary>
    bool ShowBar { get; set; }

    /// <summary>Whether the lowest and highest readings of this session are written under it.</summary>
    bool ShowMinMax { get; set; }

    /// <summary>Whether an arrow beside the number says it is rising or falling (<see cref="SensorTrend"/>).</summary>
    bool ShowTrend { get; set; }
}
