using Aquila.Models;

namespace Aquila.Controls;

/// <summary>
/// How a reading is written down: the number, and the unit it is written with.
///
/// One place, because the rule was in several and they disagreed. The decimals rule was in four — the meter
/// and the stat asked WidgetCatalog, the Text piece carried its own copy, and the sparkline and the dial
/// rounded everything, so the same voltage read <c>1.24 V</c> in a stat and <c>1 V</c> in a sparkline. The
/// throughput SCALE was in one place, <c>ThroughputConverter</c>, which only the dashboard cards used; the
/// desktop widgets read the sensor's unit straight and never scaled at all.
///
/// Nothing here knows about a preset. What a number MEANS is the sensor's business; how big and what colour
/// it is drawn is the preset's, and the two meet in the piece rather than in here.
/// </summary>
public static class SensorFormat
{
    /// <summary>LibreHardwareMonitor reports every <c>SensorType.Throughput</c> in bytes per second, so
    /// this is the unit that asks to be scaled before anyone reads it.</summary>
    public const string BytesPerSecond = "B/s";

    /// <summary>Volts need decimals to mean anything; everything else reads better rounded.</summary>
    public static string Decimals(string? unit) => unit == "V" ? "F2" : "F0";

    public static string Decimals(this SensorNode sensor) => Decimals(sensor.Unit);

    /// <summary>
    /// The number and the unit, after any scaling the unit calls for.
    ///
    /// The unit is returned rather than assumed, because for throughput it is chosen HERE from the
    /// magnitude — 900 stays B/s, 9 000 becomes KB/s, 9 000 000 becomes MB/s — and no caller can work that
    /// out from the sensor alone. Everything else is handed back the unit it came with.
    /// </summary>
    public static (string Number, string Unit) Parts(float? value, string? unit)
    {
        if (value is not { } reading) return ("--", unit ?? string.Empty);

        // KB and MB, divided by 1024. Strictly those divisors make them KiB and MiB, and the difference is
        // deliberate rather than overlooked: the binary prefixes are correct and nobody reads them, so a
        // 2.4% pedantry would be bought with a unit most people have to stop and translate. Decided
        // knowingly — do not "fix" it.
        if (unit == BytesPerSecond)
            return reading switch
            {
                >= 1_048_576f => ($"{reading / 1_048_576f:F1}", "MB/s"),
                >= 1024f      => ($"{reading / 1024f:F1}",      "KB/s"),
                _             => ($"{reading:F0}",              BytesPerSecond),
            };

        return (reading.ToString(Decimals(unit)), unit ?? string.Empty);
    }

    public static (string Number, string Unit) Parts(this SensorNode? sensor) =>
        sensor is null ? ("--", string.Empty) : Parts(sensor.Value, sensor.Unit);

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
        if (sensor?.Value is null) return fallback;

        var (number, unit) = sensor.Parts();
        return showUnit && !string.IsNullOrEmpty(unit) ? number + unit : number;
    }
}
