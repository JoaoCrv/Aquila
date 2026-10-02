using Aquila.Models;

namespace Aquila.Controls;

/// <summary>
/// The range a bar or a dial draws a reading against, when the widget has not said.
///
/// One place because the rule was written twice, in SensorBar and RadialGauge, which is how the same
/// sensor could end up drawn against two different scales in two widgets sitting side by side.
///
/// It used to be <c>Sensor.Max</c> — the highest value seen since the app started. That only ever grows,
/// so one throughput spike raised the ceiling permanently and the bar spent the rest of the day pinned
/// near zero: measured in a log, a drive's Read Rate scale climbed 100 → 19 719 → 66 507 → 751 814 MB/s
/// in four seconds, after which a change of 28 766 moved the bar by thirteen pixels out of three hundred.
/// A scale that only ratchets up is a scale that stops answering.
///
/// The recent history answers instead: <see cref="SensorNode.History"/> is a rolling window the sensor
/// already keeps, so the reading is drawn against what this sensor has been doing lately. The cost is
/// honest and worth naming — the scale MOVES, including downwards when a spike ages out of the window, so
/// a steady value can shift on screen without the reading having changed. A widget that needs a fixed
/// ceiling should say so, and both pieces already take an explicit Minimum and Maximum for that.
/// </summary>
public static class SensorScale
{
    /// <summary>
    /// The top of the scale. An explicit maximum wins; a percentage and a temperature are 0..100; anything
    /// else is measured against its own recent peak.
    ///
    /// The recent peak is the right answer for the reading it was invented for — throughput, unbounded and
    /// spiky — and the wrong one for any reading that holds steady, because a steady reading IS its recent
    /// peak. A CPU sitting at 62 °C drew as 62 out of 63: a dial permanently full, saying "at the limit"
    /// about a machine that was idling. Temperature is bounded the way a percentage is — hardware throttles
    /// before 100 °C and nothing in a PC is meant to pass it — so it gets the same fixed range, and 62 °C
    /// draws as a little over half, which is what it is.
    /// </summary>
    public static double Ceiling(SensorNode sensor, double declared)
    {
        if (!double.IsNaN(declared)) return declared;
        if (sensor.Unit is "%" or "°C") return 100;

        var peak = 0d;
        foreach (var point in sensor.History)
            if (point > peak) peak = point;

        // The current reading is included even when the history has not caught up with it — a widget
        // built this instant has an empty window and would otherwise draw its first frame against zero.
        if (sensor.Value is { } value && value > peak) peak = value;

        // Never zero: a sensor sitting at nothing still needs a range to be nothing WITHIN.
        return peak > 0 ? peak : sensor.Max ?? 100;
    }
}
