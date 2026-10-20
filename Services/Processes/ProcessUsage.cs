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
    /// The applications with the most of one measure, largest first. Ties go alphabetically, so two applications
    /// at the same value keep their places from one read to the next instead of trading them at random.
    /// Applications whose measure is not known yet are left out rather than shown as zero.
    /// </summary>
    public static List<(string Name, float Value)> Top(
        IEnumerable<AppUsage> apps, Func<AppUsage, double?> measure, int count) =>
        apps.Select(app => (app.Name, Value: measure(app)))
            .Where(app => app.Value is not null)
            .OrderByDescending(app => app.Value)
            .ThenBy(app => app.Name, StringComparer.OrdinalIgnoreCase)
            .Take(count)
            .Select(app => (app.Name, (float)app.Value!.Value))
            .ToList();
}
