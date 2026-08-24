using Aquila.Models;

namespace Aquila.Controls;

/// <summary>
/// A piece drawn as a dial, and what is worth letting the user change about it.
///
/// Matched structurally like <see cref="IChartStyle"/> and <see cref="ISensorPiece"/>: a sparkline is not a
/// dial and does not implement this, so the editor and the builder both find out by asking rather than by
/// consulting a list of kinds that someone has to remember to update.
/// </summary>
public interface IGaugeStyle
{
    double ArcThickness { get; set; }
    double ArcCorner { get; set; }
    GaugeSweep Sweep { get; set; }
    double ValueSize { get; set; }
    bool ShowValue { get; set; }
}
