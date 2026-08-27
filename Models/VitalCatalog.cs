namespace Aquila.Models;

/// <summary>
/// One reading the app watches on the user's behalf: what to call it, where to find it, and the scale it
/// is judged on.
/// </summary>
/// <param name="Key">Stable identity, for settings and for picking a subset. Never shown.</param>
/// <param name="Short">Three or four characters for the title bar's strip, where space is contested.</param>
/// <param name="Label">Readable name, used when explaining a pressure reading — "GPU temperature".</param>
/// <param name="Pick">Where the reading comes from. Returns null when the machine has no such part.</param>
/// <param name="Family">Which family of limits decides whether this reading is ordinary — the same names
/// XAML passes to IntensityBrushConverter. A NAME, not a copy of the numbers: limits the user changes are
/// stored per family, so a vital holding its own values could not follow them.</param>
public sealed record Vital(
    string Key,
    string Short,
    string Label,
    Func<HardwareNode, SensorNode?> Pick,
    string Family);

/// <summary>
/// The readings the app watches, declared once.
///
/// They were declared twice: <c>SystemPressure.Default</c> and the title bar's own table held the same
/// picks against the same scales, differing only in how they spelled the labels. Four of the five rows
/// overlapped, so changing where the CPU temperature comes from in one left the other quietly disagreeing —
/// the same failure <see cref="Thresholds"/> exists to prevent, one level up.
///
/// The pressure reading takes all of them; the title bar's strip takes the subset that fits. Which subset
/// is a layout decision and stays with the surface that has the layout problem.
/// </summary>
public static class VitalCatalog
{
    public static IReadOnlyList<Vital> All { get; } =
    [
        new("cpu.load", "CPU", "CPU load",
            h => h.Cpus.Count > 0 ? h.Cpus[0].Load.Total : null, nameof(Thresholds.Percent)),

        new("gpu.load", "GPU", "GPU load",
            h => h.PrimaryGpu?.Load.Core, nameof(Thresholds.Percent)),

        new("memory.load", "RAM", "Memory",
            h => h.Memory.Load.Total, nameof(Thresholds.Percent)),

        new("cpu.temp", "PKG", "CPU temperature",
            h => h.Cpus.Count > 0 ? h.Cpus[0].Temperature.Primary : null, nameof(Thresholds.Temperature)),

        new("gpu.temp", "GPU°", "GPU temperature",
            h => h.PrimaryGpu?.Temperature.Primary, nameof(Thresholds.Temperature)),
    ];

    /// <summary>The named readings, in the order asked for. An unknown key is skipped rather than throwing:
    /// this reads a list a surface chose, and a stale name should cost one pill, not the window.</summary>
    public static IReadOnlyList<Vital> Pick(params string[] keys) =>
        [.. keys.Select(key => All.FirstOrDefault(v => v.Key == key)).OfType<Vital>()];
}
