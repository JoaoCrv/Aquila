using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Aquila.Models.Nodes;

public class NetworkNode
{
    public string? Name { get; set; }
    public SensorNode Load { get; } = new();
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