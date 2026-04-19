using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Aquila.Models.Nodes;

public class GpuNode
{
    public string? Name { get; set; }

    public GpuLoadNode Load { get;} = new();
    public GpuTemperatureNode Temperature { get; } = new();
    public GpuClockNode Clock { get; } = new();
    public GpuDataNode Data { get; } = new();
    public GpuPowerNode Power { get; } = new();
    public GpuFanNode Fan { get; } = new();
    public GpuFactorNode Factor { get; } = new();
}
public class GpuLoadNode
{
    public SensorNode Core { get; } = new();
    public SensorNode Memory { get; } = new();
    public SensorNode D3D { get; } = new();
}

public class GpuTemperatureNode
{
    public SensorNode Primary { get; } = new();
    public SensorNode Secondary { get; } = new();
}

public class GpuClockNode
{
    public SensorNode Core { get; } = new();
    public SensorNode Soc { get; } = new();
    public SensorNode Memory { get; } = new();
}

public class GpuDataNode
{
    public SensorNode Used { get; } = new();
    public SensorNode Free { get; } = new();
    public SensorNode Total { get; } = new();
    public SensorNode DedicatedUsed { get; } = new();
    public SensorNode DedicatedFree { get; } = new();
    public SensorNode DedicatedTotal { get; } = new();
    public SensorNode SharedUsed { get; } = new();
    public SensorNode SharedFree { get; } = new();
    public SensorNode SharedTotal { get; } = new();
}

public class GpuPowerNode
{
    public SensorNode Package { get; } = new();
    public SensorNode Core { get; } = new();
    public SensorNode Soc { get; } = new();
}

public class GpuFanNode
{
    public SensorNode Primary { get; } = new();
    public SensorNode Secondary { get; } = new();
}

public class GpuFactorNode
{
    public SensorNode Fps { get; } = new();
}

