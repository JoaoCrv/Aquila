namespace Aquila.Models.Nodes;

public class MemoryNode
{
    public MemoryLoadNode Load { get; } = new();
    public MemoryDataNode Data { get; } = new();
    public MemoryVirtualNode Virtual { get; } = new();
    public List<DimmNode> Dimms { get; } = new();

    private readonly SortedDictionary<int, DimmNode> _dimmsById = new();

    public DimmNode GetOrCreateDimm(int index)
    {
        if (!_dimmsById.TryGetValue(index, out var dimm))
        {
            dimm = new DimmNode();
            _dimmsById[index] = dimm;
            Dimms.Clear();
            Dimms.AddRange(_dimmsById.Values);
        }
        return dimm;
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