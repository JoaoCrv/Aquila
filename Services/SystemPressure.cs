using Aquila.Models;

namespace Aquila.Services;

/// <summary>How hard the machine is being pushed, and by what.</summary>
/// <param name="Level">0–1, on the shared scale of <see cref="Thresholds.Level"/>.</param>
/// <param name="Source">Which reading produced it, e.g. "GPU temperature".</param>
/// <param name="Value">That reading's own value, for display.</param>
/// <param name="Unit">The unit that value is in, so an explanation can be written without guessing it.</param>
public readonly record struct PressureReading(double Level, string Source, double Value, string Unit);

/// <summary>
/// One number for the state of the machine, derived from several sensors.
///
/// It is the LARGEST normalised reading, never an average. That choice is the whole design: an average —
/// or a weighted score — cannot be explained. If the answer is 0.7 and the user asks why, a blend has no
/// reply, while a maximum always names the sensor responsible. Hence <see cref="PressureReading.Source"/>:
/// anything showing this number can also say what caused it.
///
/// The contributors are the readings shown beside it in the title bar, on purpose. A signal fed by
/// sensors the user cannot see becomes an oracle — it rises and nothing on screen accounts for it.
///
/// Which sensors count is passed in rather than fixed here — a second surface may well want a different
/// set. The LIMITS are not passed in: they come from <see cref="VitalMonitor"/>, so a ribbon and the pills
/// beside it cannot disagree about whether the same reading is hot, and so a limit the user changes takes
/// effect in both at once.
/// </summary>
public sealed class SystemPressure(VitalMonitor monitor, IReadOnlyList<Vital>? sources = null)
{
    private readonly IReadOnlyList<Vital> _sources = sources ?? VitalCatalog.All;

    public PressureReading Evaluate(HardwareNode hardware)
    {
        var worst = new PressureReading(0, string.Empty, 0, string.Empty);

        foreach (var source in _sources)
        {
            var reading = monitor.Read(source, hardware);

            // A sensor the machine does not report is not a quiet one — it is absent, and must not be
            // read as zero pressure. Skipping keeps a missing GPU from looking like an idle one.
            if (!reading.HasValue) continue;

            if (reading.Level > worst.Level)
                worst = new PressureReading(reading.Level, source.Label, reading.Value, reading.Unit);
        }

        return worst;
    }
}
