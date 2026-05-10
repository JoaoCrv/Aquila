using Aquila.Models.Nodes;
using System.Collections.Generic;

namespace Aquila.Models;

public class HardwareNode
{
    public MotherboardNode Motherboard { get; } = new();
    public List<CpuNode> Cpus { get; } = new();
    public MemoryNode Memory { get; } = new();
    public List<GpuNode> Gpus { get; } = new();
    public List<NetworkNode> Networks { get; } = new();
    public List<StorageNode> Storages { get; } = new();

    public SensorNode TotalPower { get; } = new();
}