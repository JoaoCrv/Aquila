namespace Aquila.Models.Nodes;


public class CpuNode
{
    public string? Name { get; set; }

    public CpuLoadNode Load { get;} = new();
    public CpuTemperatureNode Temperature { get; } = new();
    public CpuPowerNode Power { get; } = new();
    public CpuClockNode Clock { get; } = new();
}

public class CpuLoadNode 
{
    public SensorNode Total { get; } = new();
    public SensorNode CoreMax { get; }= new();
    public SensorCollection Cores { get; } = new();
}

public class CpuTemperatureNode
{
    public SensorNode Primary { get; } = new();
    public SensorNode Secondary { get; } = new();
}

public class CpuPowerNode
{
    public SensorNode Package { get; } = new();
}

public class CpuClockNode 
{
    public SensorNode BusSpeed { get; } = new();
    public SensorNode CoresAverage { get; } = new();
    public SensorCollection Cores { get; } = new();
}