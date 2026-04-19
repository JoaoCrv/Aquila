using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Aquila.Models.Nodes;

public class StorageNode
{
    public string? Name { get; set; }
    
    public StorageLoadNode Load { get; } = new();
    public StorageDataNode Data { get; } = new();
    public StorageTemperatureNode Temperature { get; } = new();
    public StorageThroughputNode Throughput { get; } = new();
    public StorageLevelNode Level { get; } = new();
    public StorageFactorNode Factor { get; } = new();
}

public class StorageLoadNode
{
    public SensorNode UsedSpace { get; } = new();
    public SensorNode Read { get; } = new();
    public SensorNode Write { get; } = new();
    public SensorNode Total { get; } = new();
}

public class StorageDataNode
{
    public SensorNode Read { get; } = new();
    public SensorNode Written { get; } = new();
    public SensorNode FreeSpace { get; } = new();
    public SensorNode TotalSpace { get; } = new();
}

public class StorageTemperatureNode
{
    public SensorNode Primary { get; } = new();
    public SensorNode Warning { get; } = new();
    public SensorNode Critical { get; } = new();
}

public class StorageLevelNode
{
    public SensorNode Life { get; } = new();
    public SensorNode AvailableSpare { get; } = new();
    public SensorNode AvailableSpareThreshold { get; } = new();
    public SensorNode PercentageUsed { get; } = new();
}

public class StorageFactorNode
{
    public SensorNode PowerOnCount { get; } = new();
    public SensorNode PowerOnHours { get; } = new();
}

public class StorageThroughputNode
{
    public SensorNode ReadRate { get; } = new();
    public SensorNode WriteRate { get; } = new();
}