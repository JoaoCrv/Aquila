using System.Diagnostics;
using Aquila.Controls;
using Aquila.Models;
using Aquila.Models.Nodes;

namespace Aquila.Services.Processes;

/// <summary>
/// The applications using the machine — the second source beside LibreHardwareMonitor, and the first that is not
/// hardware (#26).
///
/// It reads only while a list of processes is on screen. Whether one is, is a question about the state, and
/// <see cref="Read"/> may not touch the state — so <see cref="Apply"/>, which may, asks it and leaves the answer
/// for the next read. A list therefore starts filling a poll after it appears, and CPU and I/O a poll after
/// that, since a rate needs two reads to measure between. When the last list goes, the earlier read is dropped
/// with it: measured against a read from minutes ago, the first figures back would be an average over minutes.
/// </summary>
internal sealed class ProcessDriver : IHardwareDriver
{
    private readonly ProcessTable _table = new();

    // Written by Apply, read by Read, never at the same time: a read finishes before its Apply begins.
    private bool _wanted;

    private List<ProcessSample>? _previous;
    private long _previousAt;
    private List<AppUsage>? _usage;

    public string Name => "Processes";
    public string Version => "1.0";
    public bool IsAvailable => true;
    public bool RequiresElevation => false;

    public void Initialize() { }

    public void Read()
    {
        if (!_wanted)
        {
            _previous = null;
            _usage = null;
            return;
        }

        var now = _table.Read();
        long at = Stopwatch.GetTimestamp();

        _usage = ProcessUsage.Between(_previous, now, Stopwatch.GetElapsedTime(_previousAt, at).TotalSeconds,
            Environment.ProcessorCount);

        _previous = now;
        _previousAt = at;
    }

    public void Apply(AquilaState state)
    {
        var lists = state.Processes;
        _wanted = lists.Watched;

        if (!_wanted)
        {
            // Nobody is looking, so nothing is kept: a list shown again later starts from what is true then.
            foreach (var list in lists.All) list.Show([], string.Empty);
            return;
        }

        if (_usage is not { } usage) return;
        _usage = null;

        lists.Cpu.Show(ProcessUsage.Top(usage, app => app.Cpu, ListNode.MaxRows), "%");
        lists.Memory.Show(ProcessUsage.Top(usage, app => app.MemoryMb, ListNode.MaxRows), "MB");
        lists.Io.Show(ProcessUsage.Top(usage, app => app.IoPerSecond, ListNode.MaxRows), SensorFormat.BytesPerSecond);
    }

    public void Shutdown() => _table.Dispose();
}
