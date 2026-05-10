namespace Aquila.Models.Nodes;

public class NetworkNode
{
    public string? Name { get; set; }

    public NetworkThroughputNode Throughput { get; } = new();
    public NetworkDataNode Data { get; } = new();
}

public class NetworkThroughputNode
{
    public SensorNode Upload { get; } = new();
    public SensorNode Download { get; } = new();
}

public class NetworkDataNode
{
    public SensorNode Uploaded { get; } = new();
    public SensorNode Downloaded { get; } = new();
}
