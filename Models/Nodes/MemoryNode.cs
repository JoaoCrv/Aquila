using System.Collections.Generic;

namespace Aquila.Models.Nodes;

public class MemoryNode
{
    public MemoryLoadNode Load { get; } = new();
    public MemoryDataNode Data { get; } = new();
    public MemoryVirtualNode Virtual { get; } = new();
    public List<DimmNode> Dimms { get; } = new();

    public DimmNode GetOrCreateDimm(int index)
    {
        while (Dimms.Count <= index)
            Dimms.Add(new DimmNode());

        return Dimms[index];
    }
}

public class MemoryLoadNode
{
    public SensorNode Total { get; } = new();
}

public class MemoryDataNode
{
    public SensorNode Used { get; } = new();
    public SensorNode Available { get; } = new();
    public SensorNode Total { get; } = new();
}

public class MemoryVirtualNode
{
    public SensorNode Load { get; } = new();
    public SensorNode Used { get; } = new();
    public SensorNode Available { get; } = new();
}

public class DimmNode
{
    public string? Name { get; set; }
    public SensorNode Capacity { get; } = new();
    public SensorNode Temperature { get; } = new();
    public SensorNode WarningTemperature { get; } = new();
    public SensorNode CriticalTemperature { get; } = new();
}