using Aquila.Models;

namespace Aquila.Controls;

/// <summary>
/// How a reading is written down. One place, because the rule was in four and only one of them was
/// complete: the meter and the stat asked <c>WidgetCatalog.Decimals</c>, the Text piece carried its own
/// copy of the same conditional, and the sparkline and the dial simply rounded everything — so the same
/// voltage read <c>1.24 V</c> in a stat and <c>1 V</c> in a sparkline of the same sensor.
///
/// Nothing here knows about a preset. What a number MEANS is the sensor's business; how big and what colour
/// it is drawn is the preset's, and the two meet in the piece rather than in here.
/// </summary>
public static class SensorFormat
{
    /// <summary>Volts need decimals to mean anything; everything else reads better rounded.</summary>
    public static string Decimals(string? unit) => unit == "V" ? "F2" : "F0";

    public static string Decimals(this SensorNode sensor) => Decimals(sensor.Unit);

    /// <summary>
    /// The value and, if it is wanted, its unit — closed up rather than spaced.
    ///
    /// One convention for every piece that writes the two into a single string. It used to be spaced in the
    /// sparkline and closed everywhere else, which showed up as <c>62 %</c> beside <c>62%</c> on the same
    /// desktop. Closed won because most units here are degrees and percent, where a space reads as a
    /// mistake; the stat is unaffected either way, since it draws the unit as a piece of its own so it can
    /// have its own size.
    /// </summary>
    public static string Reading(this SensorNode? sensor, bool showUnit, string fallback = "--")
    {
        if (sensor?.Value is not { } value) return fallback;

        var text = value.ToString(sensor.Decimals());
        var unit = showUnit ? sensor.Unit : null;

        return string.IsNullOrEmpty(unit) ? text : text + unit;
    }
}
