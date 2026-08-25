using Aquila.Models;

namespace Aquila.Controls;

/// <summary>
/// A piece drawn as a line, and the few things about that line worth putting in front of the user.
///
/// Matched structurally, the same way <see cref="ISensorPiece"/> is: a kind that is not a chart simply does
/// not implement this, so nothing anywhere has to keep a list of which kinds these settings apply to. The
/// dial, the number and the meter ignore them by not being able to receive them.
/// </summary>
public interface IChartStyle
{
    double LineThickness { get; set; }
    ChartFill Fill { get; set; }
    double Smoothness { get; set; }
    double PointSize { get; set; }

    /// <summary>How the vertical scale is decided, and its ends when the user decides them.</summary>
    ChartScale Scale { get; set; }
    double ScaleMin { get; set; }
    double ScaleMax { get; set; }
}
