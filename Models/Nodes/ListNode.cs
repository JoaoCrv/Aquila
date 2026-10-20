using System.ComponentModel;

namespace Aquila.Models.Nodes;

/// <summary>
/// Readings that come as a list — one per item, already in the order they are to be drawn.
///
/// The order is the SOURCE's, never the widget's: the applications using the most CPU come largest first, and a
/// future watchlist would come in the order its owner chose. A widget that sorted would have to know which of the
/// two it was drawing, and the day a third kind of list arrived it would be wrong about it.
///
/// Each row is a <see cref="SensorNode"/>, so a row is written, coloured and judged by exactly the rules every
/// other reading is — there is no second set of them to drift. And an item keeps its node for as long as it stays
/// in the list, so a row carries its own history: a per-row trend later, or, for a price, whether it went up.
/// </summary>
public sealed class ListNode(string identifier, string name) : INotifyPropertyChanged
{
    /// <summary>The most rows a list keeps, and so the most a widget can ask to show.</summary>
    public const int MaxRows = 20;

    /// <summary>What a widget stores to name this list — <c>/processes/cpu</c>.</summary>
    public string Identifier { get; } = identifier;

    /// <summary>What the list is OF, as the editor offers it — "CPU".</summary>
    public string Name { get; } = name;

    /// <summary>
    /// What each row is judged as, or null for a list whose figures have no scale to be judged on.
    ///
    /// Set on the CPU list alone. An application's share of the processor is measured exactly as the
    /// processor's own load is, so the same limits mean the same thing there — an application at 85% has the
    /// machine at 85% at least. Megabytes and bytes per second have no such scale, and their rows rest on the
    /// ramp's Normal, as a sensor in watts does.
    /// </summary>
    public MetricKey? Metric { get; init; }

    /// <summary>
    /// The rows, in order. Replaced whole on every read with ONE change notification, never edited in place:
    /// a list re-ordered row by row would announce itself a dozen times a second, and anything drawing it would
    /// draw every half-sorted state in between.
    /// </summary>
    public IReadOnlyList<SensorNode> Rows { get; private set; } = [];

    /// <summary>
    /// Whether anything is showing this list. A source asks before it reads, so a list nobody draws costs
    /// nothing — reading every process on the machine is not free, and most people will never add one.
    /// </summary>
    public bool Watched => _watchers > 0;

    private int _watchers;

    /// <summary>Says this list is being shown, until the result is disposed. Called by whatever draws it, when it
    /// appears; the dispose is safe to repeat, so an unload that fires twice cannot undercount.</summary>
    public IDisposable Watch()
    {
        _watchers++;
        return new Release(this);
    }

    /// <summary>
    /// Shows these items, in this order. An item already shown keeps its node — and with it its history — so a
    /// row that moves up the list is the same row, not a new one that happens to carry the same name.
    /// </summary>
    public void Show(IReadOnlyList<(string Name, float Value)> items, string unit)
    {
        if (items.Count == 0 && Rows.Count == 0) return;

        var shown = new Dictionary<string, SensorNode>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in Rows) shown.TryAdd(row.Name ?? string.Empty, row);

        var rows = new List<SensorNode>(items.Count);
        foreach (var (itemName, value) in items)
        {
            if (!shown.Remove(itemName, out var node))
                node = new SensorNode { Name = itemName, Identifier = $"{Identifier}/{itemName}" };

            node.Unit = unit;
            node.Value = value;
            rows.Add(node);
        }

        Rows = rows;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Rows)));
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private sealed class Release(ListNode list) : IDisposable
    {
        private ListNode? _list = list;

        public void Dispose()
        {
            if (_list is null) return;
            _list._watchers--;
            _list = null;
        }
    }
}

/// <summary>
/// The applications using the machine, as three lists: by CPU, by memory and by I/O. One read of the processes
/// serves all three, so they are filled together and watched as one.
/// </summary>
public sealed class ProcessesNode
{
    /// <summary>What these are lists OF, as the editor groups them — so a List's reading reads
    /// "Processes — CPU".</summary>
    public string Name => "Processes";

    public ListNode Cpu { get; } = new("/processes/cpu", "CPU") { Metric = new(HardwareKind.Cpu, MetricKind.Load) };
    public ListNode Memory { get; } = new("/processes/memory", "Memory");

    /// <summary>Bytes read and written per second — files, network and devices together, which is what the
    /// kernel counts. Not "Disk": a browser streaming video reads megabytes a second and touches no disk.</summary>
    public ListNode Io { get; } = new("/processes/io", "I/O");

    public IReadOnlyList<ListNode> All => [Cpu, Memory, Io];

    public bool Watched => Cpu.Watched || Memory.Watched || Io.Watched;

    /// <summary>
    /// The list a widget names, or null for a name no list has — a list from a newer build, or a typo.
    ///
    /// The one place a stored identifier becomes a list, so the desktop and the Widgets page cannot match it
    /// two ways. Lists are kept out of <see cref="SensorCatalog"/> on purpose: the Explorer, the reannouncing of
    /// readings and <c>VitalMonitor.KeyFor</c> all walk it, and none of them wants rows that come and go.
    /// </summary>
    public ListNode? Find(string? identifier) => All.FirstOrDefault(list => list.Identifier == identifier);
}
