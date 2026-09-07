using Aquila.Models;

namespace Aquila.Controls;

/// <summary>
/// A piece drawn as a dial, and what is worth letting the user change about it.
///
/// Matched structurally like <see cref="IChartStyle"/> and <see cref="ISensorPiece"/>: a sparkline is not a
/// dial and does not implement this, so the editor and the builder both find out by asking rather than by
/// consulting a list of kinds that someone has to remember to update.
/// </summary>
public interface IGaugeStyle : IValueStyle, IUnitStyle, IHideableValue
{
    double ArcThickness { get; set; }
    double ArcCorner { get; set; }
    GaugeSweep Sweep { get; set; }

    /// <summary>The unfilled part of the arc. Null leaves the built-in faint white, which is the right
    /// answer over most wallpapers and the wrong one over a pale desktop.</summary>
    System.Windows.Media.Brush? TrackBrush { get; set; }

    /// <summary>The centre number's colour. Explicit for the same reason ValueFont is: SkiaSharp paints
    /// that number, so unlike every other piece's text it inherits no Foreground from the tile.</summary>
    System.Windows.Media.Brush? ValueBrush { get; set; }
}
