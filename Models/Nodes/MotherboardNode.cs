namespace Aquila.Models.Nodes;

public class MotherboardNode
{
    public string? Name { get; set; }
    public string? ChipsetName { get; set; }

    public SensorGroup Temperature { get; } = new();
    public SensorGroup Voltage { get; } = new();
    public SensorGroup Fan { get; } = new();
    public SensorGroup Control { get; } = new();

    // CPU fans resolved from Fan by name — null if not present on this board
    public SensorNode? CpuFan { get; set; }
    public SensorNode? CpuFanSecondary { get; set; }
    public SensorNode? CpuPump { get; set; }
}
