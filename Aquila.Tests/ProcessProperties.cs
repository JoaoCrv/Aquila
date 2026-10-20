using Aquila.Models.Nodes;
using Aquila.Services.Processes;
using FsCheck.Xunit;
using Xunit;

namespace Aquila.Tests;

/// <summary>
/// The process lists (#26). What is worked out between two reads of the process table is easy to get backwards —
/// add the processes up by application first and take the difference after, and a browser tab closing shows its
/// browser at a negative share of the CPU — so it is held here to the rule, for any births, exits and reused IDs.
/// </summary>
public sealed class ProcessProperties
{
    private static readonly string[] Apps = ["vivaldi", "Code", "System"];

    /// <summary>
    /// Between any two reads — whatever exits, whatever starts, and whatever new process is handed an old one's ID
    /// — no application is ever given a negative share of the CPU, or negative I/O. Every process before starts with
    /// a large total, so a process that left, or a new one compared with the old holder of its ID, would show up
    /// here as a large negative number.
    /// </summary>
    [Property(MaxTest = 300)]
    public bool No_application_is_ever_given_negative_usage(byte[] fates, byte[] amounts)
    {
        fates ??= [];
        amounts ??= [];
        int count = Math.Min(fates.Length, 30);

        var before = new List<ProcessSample>();
        var now = new List<ProcessSample>();
        for (int i = 0; i < count; i++)
        {
            var name = Apps[i % Apps.Length];
            long amount = i < amounts.Length ? amounts[i] : 0;
            var old = new ProcessSample(Id: i, Created: 1_000 + i, name, CpuTime: 5_000_000 + i, PrivateBytes: 1 << 20,
                IoBytes: 9_000_000 + i);
            before.Add(old);

            switch (fates[i] % 4)
            {
                case 0: // exited
                    break;
                case 1: // still running, having done a little more
                    now.Add(old with { CpuTime = old.CpuTime + amount, IoBytes = old.IoBytes + amount * 10 });
                    break;
                case 2: // exited, and a new process was given its ID
                    now.Add(new ProcessSample(i, Created: 50_000 + i, Apps[(i + 1) % Apps.Length], amount, 1 << 20,
                        amount * 10));
                    break;
                default: // still running, and a new process of the same application started beside it
                    now.Add(old);
                    now.Add(new ProcessSample(100 + i, Created: 60_000 + i, name, amount, 1 << 20, amount));
                    break;
            }
        }

        var usage = ProcessUsage.Between(before, now, seconds: 1, cores: 16);

        return usage.All(app => app.Cpu >= 0 && app.IoPerSecond >= 0);
    }

    /// <summary>
    /// The list is never longer than asked, comes largest first, and nothing left out is larger than anything kept
    /// — for any applications and any count. Applications whose figure is not known yet are never listed.
    /// </summary>
    [Property(MaxTest = 300)]
    public bool The_top_is_the_top(int[] values, byte count)
    {
        values ??= [];
        var apps = values.Select((v, i) => new AppUsage($"app{i}", v % 5 == 0 ? null : Math.Abs((double)v), 0, null))
            .ToList();

        var top = ProcessUsage.Top(apps, app => app.Cpu, count % 25);
        var kept = top.Select(row => row.Name).ToHashSet();
        var leftOut = apps.Where(app => app.Cpu is not null && !kept.Contains(app.Name)).Select(app => app.Cpu!.Value);
        float smallestKept = top.Count == 0 ? float.MaxValue : top[^1].Value;

        return top.Count <= count % 25
            && top.Zip(top.Skip(1)).All(pair => pair.First.Value >= pair.Second.Value)
            && top.All(row => apps.Single(app => app.Name == row.Name).Cpu is not null)
            && (top.Count < count % 25 ? !leftOut.Any() : leftOut.All(value => (float)value <= smallestKept));
    }

    /// <summary>
    /// The real table, read on this machine, finds this very process — its name, its creation time to the tick, and
    /// some CPU time and memory. The kernel's structure is declared by hand, field by field; a field out of place
    /// would shift every one after it, and this is where that would show.
    /// </summary>
    [Fact]
    public void The_table_finds_this_process()
    {
        using var table = new ProcessTable();
        using var me = System.Diagnostics.Process.GetCurrentProcess();

        var self = Assert.Single(table.Read(), process => process.Id == me.Id);

        Assert.Equal(me.ProcessName, self.Name, ignoreCase: true);
        Assert.Equal(me.StartTime.ToFileTime(), self.Created);
        Assert.InRange(self.CpuTime, 1, me.TotalProcessorTime.Ticks);
        Assert.True(self.PrivateBytes > 0);
    }

    /// <summary>One application in several processes is one row, with their usage added up.</summary>
    [Fact]
    public void An_application_in_several_processes_is_one_row()
    {
        ProcessSample[] before =
        [
            new(1, 10, "vivaldi", CpuTime: 0, PrivateBytes: 100 << 20, IoBytes: 0),
            new(2, 20, "vivaldi", CpuTime: 0, PrivateBytes: 200 << 20, IoBytes: 0),
        ];
        // Each did half a second of CPU over one second on a two-core machine: a quarter of the machine each.
        ProcessSample[] now =
        [
            before[0] with { CpuTime = 5_000_000, IoBytes = 1_000 },
            before[1] with { CpuTime = 5_000_000, IoBytes = 3_000 },
        ];

        var app = Assert.Single(ProcessUsage.Between(before, now, seconds: 1, cores: 2));

        Assert.Equal("vivaldi", app.Name);
        Assert.Equal(50, app.Cpu!.Value, precision: 6);
        Assert.Equal(300, app.MemoryMb, precision: 6);
        Assert.Equal(4_000, app.IoPerSecond!.Value, precision: 6);
    }

    /// <summary>The first read knows memory and nothing else: a rate needs two reads to measure between, and a
    /// figure that is not known is left out of the list rather than shown as nought.</summary>
    [Fact]
    public void The_first_read_lists_memory_only()
    {
        ProcessSample[] now = [new(1, 10, "Code", CpuTime: 9_000_000, PrivateBytes: 64 << 20, IoBytes: 5_000)];

        var usage = ProcessUsage.Between(null, now, seconds: 0, cores: 8);

        Assert.Empty(ProcessUsage.Top(usage, app => app.Cpu, ListNode.MaxRows));
        Assert.Empty(ProcessUsage.Top(usage, app => app.IoPerSecond, ListNode.MaxRows));
        Assert.Equal(("Code", 64f), Assert.Single(ProcessUsage.Top(usage, app => app.MemoryMb, ListNode.MaxRows)));
    }

    /// <summary>An item that stays in the list keeps its row — the same node, carrying its history — however it
    /// moves, and the rows come in the order they were given.</summary>
    [Fact]
    public void A_row_that_stays_is_the_same_row()
    {
        var list = new ListNode("/processes/cpu", "CPU");
        list.Show([("Code", 9f), ("vivaldi", 4f)], "%");
        var code = list.Rows[0];

        list.Show([("vivaldi", 12f), ("Code", 3f)], "%");

        Assert.Equal(new[] { "vivaldi", "Code" }, list.Rows.Select(row => row.Name!));
        Assert.Same(code, list.Rows[1]);
        Assert.Equal(new[] { 9d, 3d }, code.History);
    }

    /// <summary>
    /// Every list is found by its own identifier — the name a List widget stores — and any other text finds
    /// nothing. A widget naming a list this build does not know is reported as not drawing; it must never be handed
    /// some other list in its place.
    /// </summary>
    [Property(MaxTest = 300)]
    public bool A_list_is_found_by_its_own_name_and_by_nothing_else(string? identifier)
    {
        var lists = new ProcessesNode();

        return lists.All.All(list => ReferenceEquals(lists.Find(list.Identifier), list))
            && (lists.Find(identifier) is not { } found || found.Identifier == identifier);
    }

    /// <summary>The names are what widgets.json keeps, so they are pinned: renaming one would leave every List that
    /// names it with nothing to draw.</summary>
    [Theory]
    [InlineData("/processes/cpu")]
    [InlineData("/processes/memory")]
    [InlineData("/processes/io")]
    public void A_stored_name_still_finds_its_list(string identifier) =>
        Assert.Equal(identifier, new ProcessesNode().Find(identifier)?.Identifier);
}
