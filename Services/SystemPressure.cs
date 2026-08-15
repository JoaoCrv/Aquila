using Aquila.Models;

namespace Aquila.Services;

/// <summary>How hard the machine is being pushed, and by what.</summary>
/// <param name="Level">0–1, on the shared scale of <see cref="Thresholds.Level"/>.</param>
/// <param name="Source">Which reading produced it, e.g. "GPU temperature".</param>
/// <param name="Value">That reading's own value, in its own unit, for display.</param>
public readonly record struct PressureReading(double Level, string Source, double Value);

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
/// Which sensors count, and on what scale, is passed in rather than fixed here. The judgement is
/// configuration, not arithmetic, and a second surface may well want a different one — the same reason
/// thresholds are not baked into the colour profile.
/// </summary>
public sealed class SystemPressure(IReadOnlyList<PressureSource>? sources = null)
{
    private readonly IReadOnlyList<PressureSource> _sources = sources ?? Default;

    public static IReadOnlyList<PressureSource> Default { get; } =
    [
        new("CPU load",        h => h.Cpus.Count > 0 ? h.Cpus[0].Load.Total : null,           Thresholds.Percent),
        new("CPU temperature", h => h.Cpus.Count > 0 ? h.Cpus[0].Temperature.Primary : null,  Thresholds.Temperature),
        new("GPU load",        h => h.Gpus.Count > 0 ? h.Gpus[0].Load.Core : null,            Thresholds.Percent),
        new("GPU temperature", h => h.Gpus.Count > 0 ? h.Gpus[0].Temperature.Primary : null,  Thresholds.Temperature),
        new("Memory",          h => h.Memory.Load.Total,                                      Thresholds.Percent),
    ];

    public PressureReading Evaluate(HardwareNode hardware)
    {
        var worst = new PressureReading(0, string.Empty, 0);

        foreach (var source in _sources)
        {
            // A sensor the machine does not report is not a quiet one — it is absent, and must not be
            // read as zero pressure. Skipping keeps a missing GPU from looking like an idle one.
            if (source.Pick(hardware)?.Value is not float value) continue;

            var level = source.Scale.Level(value);
            if (level > worst.Level) worst = new PressureReading(level, source.Label, value);
        }

        return worst;
    }
}

/// <summary>One contributor: what to read, what to call it, and the scale it is judged on.</summary>
/// <param name="Label">Shown when explaining the pressure, so keep it readable.</param>
public readonly record struct PressureSource(
    string Label,
    Func<HardwareNode, SensorNode?> Pick,
    Thresholds Scale);
