using Aquila.Models;

namespace Aquila.Controls;

/// <summary>
/// A piece drawn as a dial, and what is worth letting the user change about it.
///
/// Matched structurally like <see cref="IChartStyle"/> and <see cref="ISensorPiece"/>: a sparkline is not a
/// dial and does not implement this, so the editor and the builder both find out by asking rather than by
/// consulting a list of kinds that someone has to remember to update.
/// </summary>
public interface IGaugeStyle : IArcStyle, IValueStyle, IUnitStyle, IHideableValue
{
    GaugeSweep Sweep { get; set; }

    /// <summary>The centre number's colour. Explicit for the same reason ValueFont is: SkiaSharp paints
    /// that number, so unlike every other piece's text it inherits no Foreground from the tile.</summary>
    System.Windows.Media.Brush? ValueBrush { get; set; }
}

/// <summary>
/// A piece drawn as arcs, and the preset's <c>gauge</c> section it takes them from: how thick, how round at the
/// ends, and the track under them.
///
/// Its own interface since the clock's rings, which are arcs and nothing else of a dial's. A clock is always a
/// full turn, so the sweep cannot apply; it writes no number of its own in the arc, so neither can the label's
/// colour; and it has no unit and no reading to hide. Putting the arcs here lets it take exactly what it draws,
/// and the dial and the rings then cannot come to disagree about what the same preset's thickness means.
/// </summary>
public interface IArcStyle
{
    double ArcThickness { get; set; }
    double ArcCorner { get; set; }

    /// <summary>The unfilled part of the arc. Null leaves the built-in faint white, which is the right
    /// answer over most wallpapers and the wrong one over a pale desktop.</summary>
    System.Windows.Media.Brush? TrackBrush { get; set; }
}
