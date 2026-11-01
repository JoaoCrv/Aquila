using Aquila.Controls;

namespace Aquila.Services.Processes;

/// <summary>One process as one read found it. <see cref="Created"/> is part of who it is: Windows hands a
/// finished process's ID to the next one, and only the creation time tells the two apart.</summary>
public readonly record struct ProcessSample(long Id, long Created, string Name, long CpuTime, long PrivateBytes, long IoBytes);

/// <summary>One application — every process carrying its name, added together. CPU and I/O are rates, so they
/// are unknown (null) until there have been two reads to measure between.</summary>
public sealed record AppUsage(string Name, double? Cpu, double MemoryMb, double? IoPerSecond);

/// <summary>
/// Turns reads of the process table into what the lists show. Pure and file-free, so the tests can hold it to
/// the one rule that is easy to get backwards.
/// </summary>
public static class ProcessUsage
{
    /// <summary>
    /// What each application did between two reads.
    ///
    /// The difference is taken PER PROCESS and only then added up by name — never the other way round. Grouped
    /// first, a browser tab closing between two reads takes its whole CPU time out of its application's total,
    /// and the application shows a negative share. A process is matched by its ID AND its creation time, so an ID
    /// reused by a new process starts from zero instead of being subtracted from the old one's total.
    ///
    /// A process absent from the earlier read started since, so everything it has done, it did in between.
    /// </summary>
    /// <param name="before">The previous read, or null when there is none yet.</param>
    /// <param name="seconds">The time that actually passed between the two — measured, never assumed to be the
    /// poll interval, because a read can be skipped.</param>
    /// <param name="cores">CPU time is counted on every core, so a share of the machine divides by all of them.</param>
    public static List<AppUsage> Between(
        IReadOnlyList<ProcessSample>? before, IReadOnlyList<ProcessSample> now, double seconds, int cores)
    {
        Dictionary<(long, long), ProcessSample>? earlier = null;
        if (before is not null)
        {
            earlier = new(before.Count);
            foreach (var process in before) earlier[(process.Id, process.Created)] = process;
        }

        var apps = new Dictionary<string, (long Cpu, long Memory, long Io)>(StringComparer.OrdinalIgnoreCase);
        foreach (var process in now)
        {
            long cpu = process.CpuTime, io = process.IoBytes;
            if (earlier is not null && earlier.TryGetValue((process.Id, process.Created), out var was))
            {
                cpu -= was.CpuTime;
                io -= was.IoBytes;
            }

            apps.TryGetValue(process.Name, out var sum);
            apps[process.Name] = (sum.Cpu + cpu, sum.Memory + process.PrivateBytes, sum.Io + io);
        }

        // CPU time is kept in 100-nanosecond ticks.
        double cpuTicks = seconds * cores * 10_000_000;
        bool measured = earlier is not null && seconds > 0;

        return apps.Select(app => new AppUsage(
                app.Key,
                measured ? Math.Min(100, app.Value.Cpu / cpuTicks * 100) : null,
                app.Value.Memory / 1_048_576.0,
                measured ? app.Value.Io / seconds : null))
            .ToList();
    }

    /// <summary>
    /// The applications with the most of one measure, largest first AS SHOWN. Applications whose measure is not known
    /// yet are left out rather than shown as zero.
    ///
    /// Figures that read the same — two applications at "1%", which may be 1.4 and 0.6 — keep the order they had in
    /// <paramref name="before"/>, so a row changes place only when a number on screen says why. Ordered by the exact
    /// figure, two applications idling near the same share traded places nearly every read (measured on a recording,
    /// 2026-11-01: almost every swap was between two rows showing the same number), and the list was always moving to
    /// tell the viewer nothing. "Reads the same" is <see cref="SensorFormat.Reading(float?, string?, bool, string)"/>,
    /// the text the row will show, so the rule follows the format — throughput's units included — and cannot drift
    /// from it. Applications new to the list come after those that were there, by figure and then alphabetically, so
    /// two at the same value never trade at random either.
    /// </summary>
    /// <param name="unit">The unit the rows are shown in, which decides what reads the same.</param>
    /// <param name="before">The order the list was last shown in; without one, simply largest first.</param>
    public static List<(string Name, float Value)> Top(
        IEnumerable<AppUsage> apps, Func<AppUsage, double?> measure, int count,
        string? unit = null, IReadOnlyList<string>? before = null)
    {
        var place = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        if (before is not null)
            for (var i = 0; i < before.Count; i++) place.TryAdd(before[i], i);

        var largest = apps.Select(app => (app.Name, Value: measure(app)))
            .Where(app => app.Value is not null)
            .OrderByDescending(app => app.Value)
            .ThenBy(app => app.Name, StringComparer.OrdinalIgnoreCase)
            .Select(app => (app.Name, Value: (float)app.Value!.Value));

        // Numbered runs of figures that read the same, as far as the run the last row kept falls in: formatting every
        // application on the machine would be most of the work and change nothing.
        var runs = new List<(string Name, float Value, int Run)>();
        string? reading = null;
        var run = 0;
        foreach (var app in largest)
        {
            var shown = SensorFormat.Reading(app.Value, unit, showUnit: true);
            if (shown != reading)
            {
                if (runs.Count >= count) break;
                reading = shown;
                run++;
            }
            runs.Add((app.Name, app.Value, run));
        }

        return runs
            .OrderBy(app => app.Run)
            .ThenBy(app => place.GetValueOrDefault(app.Name, int.MaxValue))
            .Take(count)
            .Select(app => (app.Name, app.Value))
            .ToList();
    }

    /// <summary>
    /// One measure added up over every application — all of them, not the few a list keeps — or null while no
    /// application's figure is known yet, which is the whole of the first read for a rate. Unknown figures are left
    /// out as <see cref="Top"/> leaves them, rather than counted as nought.
    /// </summary>
    public static float? Total(IEnumerable<AppUsage> apps, Func<AppUsage, double?> measure)
    {
        double? sum = null;
        foreach (var app in apps)
            if (measure(app) is { } value)
                sum = (sum ?? 0) + value;

        return (float?)sum;
    }
}
