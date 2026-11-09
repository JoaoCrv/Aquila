using System.Windows.Media;
using Aquila.Controls.Primitives;
using Aquila.Models;
using Aquila.Services;

namespace Aquila.Controls;

/// <summary>
/// How a chart's settings — a preset's line section, a widget's scale, a reading's ramp and limits — become what a
/// <see cref="LinePlot"/> draws: its look, its scale's ends, a line's bands and the guides.
///
/// One home, because two pieces translate them: the dashboard's <see cref="SparklineChart"/> and the Chart widget.
/// Written out in each, the two would come to disagree about what "fit to data" or a flat fill means.
/// </summary>
public static class PlotDress
{
    /// <summary>The look of a preset's line section.</summary>
    public static LineLook Look(double thickness, ChartFill fill, double opacity, double smoothness, double pointSize) =>
        new(thickness, fill switch
        {
            ChartFill.None => AreaFill.None,
            ChartFill.Flat => AreaFill.Flat,
            _ => AreaFill.Fade,
        }, opacity, smoothness, pointSize);

    /// <summary>
    /// The scale's ends: fitted at both ends; manual between its two numbers, but only while the top is above the
    /// bottom — the editor lets the two be typed in any order, and passing through the moment they cross must not
    /// collapse the chart — and otherwise from nought, up to <paramref name="top"/> or fitted when there is none (NaN).
    /// </summary>
    public static (double Min, double Max) Range(ChartScale scale, double scaleMin, double scaleMax, double top) =>
        scale switch
        {
            ChartScale.FitToData => (double.NaN, double.NaN),
            ChartScale.Manual when scaleMax > scaleMin => (scaleMin, scaleMax),
            _ => (0d, top),
        };

    /// <summary>A ramp's four colours, Normal to Critical — what a line's bands are painted in. A colour that does not
    /// read is transparent, as every stored colour is.</summary>
    public static Color[] Colours(Ramp ramp) =>
        [Colour(ramp.Normal), Colour(ramp.Elevated), Colour(ramp.Alert), Colour(ramp.Critical)];

    private static Color Colour(string hex) =>
        Helpers.HexBrush.From(hex, 1) is SolidColorBrush solid ? solid.Color : Colors.Transparent;

    /// <summary>
    /// A line's bands: its ramp's colour for each stretch between its reading's limits, Normal below Elevated and so
    /// on up. Null — one colour throughout — for a reading with no limits, or before the ramp is known.
    /// </summary>
    public static IReadOnlyList<(double From, Color Color)>? Bands(SensorNode? sensor, Color[]? ramp) =>
        ramp is { Length: 4 } && sensor is not null && VitalMonitor.Current?.LimitsOf(sensor) is { } limits
            ? [(double.NegativeInfinity, ramp[0]), (limits.Elevated, ramp[1]), (limits.Alert, ramp[2]),
                (limits.Critical, ramp[3])]
            : null;

    /// <summary>Whether two lists of bands are the same, so a line is not handed a new list — and its brush made
    /// again — for bands that have not moved.</summary>
    public static bool Same(IReadOnlyList<(double From, Color Color)>? a, IReadOnlyList<(double From, Color Color)>? b) =>
        a is null ? b is null : b is not null && a.SequenceEqual(b);
}
