using Aquila.Models.Nodes;

namespace Aquila.Models;

public class AquilaState
{
    public HardwareNode Hardware { get; } = new();

    /// <summary>The applications using the machine. Filled only while a list of them is on screen.</summary>
    public ProcessesNode Processes { get; } = new();

    /// <summary>The time, for every clock at once. Written each second by ClockService, not by the poll.</summary>
    public ClockNode Clock { get; } = new();
}
