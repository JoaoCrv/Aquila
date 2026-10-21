using System.Windows.Media;
using Aquila.Models;

namespace Aquila.Controls;

/// <summary>
/// A piece drawn as a dial, and what is worth letting the user change about it.
///
/// Matched structurally like <see cref="IChartStyle"/> and <see cref="ISensorPiece"/>: a sparkline is not a
/// dial and does not implement this, so the editor and the builder both find out by asking rather than by
/// consulting a list of kinds that someone has to remember to update.
///
/// The number's colour is not here. It had to be while Skia painted that number, which inherits nothing from the
/// tile; it is a TextBlock now, and takes the tile's foreground — the preset's value colour — as every other
/// piece's text does.
/// </summary>
public interface IGaugeStyle : IArcStyle, IValueStyle, IUnitStyle, IHideableValue
{
    GaugeSweep Sweep { get; set; }
}

/// <summary>
/// A piece drawn as arcs, and the preset's <c>gauge</c> section it takes them from: how thick, how round at the
/// ends, and the track under them.
///
/// Its own interface since the clock's rings, which are arcs and nothing else of a dial's. A clock is always a
/// full turn, so the sweep cannot apply; it writes no number of its own in the arc; and it has no unit and no
/// reading to hide. Putting the arcs here lets it take exactly what it draws, and the dial and the rings then cannot
/// come to disagree about what the same preset's thickness means.
/// </summary>
public interface IArcStyle
{
    double ArcThickness { get; set; }
    double ArcCorner { get; set; }

    /// <summary>The unfilled part of the arc. Null leaves <see cref="ArcStyle.DefaultTrack"/>, which is the right
    /// answer over most wallpapers and the wrong one over a pale desktop.</summary>
    Brush? TrackBrush { get; set; }
}

public static class ArcStyle
{
    /// <summary>The track where a preset names none: a faint white. One brush for the dial and the clock's rings,
    /// which draw the same arc.</summary>
    public static Brush DefaultTrack { get; } = Frozen(Color.FromArgb(20, 255, 255, 255));

    private static Brush Frozen(Color colour)
    {
        var brush = new SolidColorBrush(colour);
        brush.Freeze();
        return brush;
    }
}
