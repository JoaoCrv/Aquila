namespace Aquila.Models.Nodes;

public class MotherboardNode
{
    public string? Name { get; set; }
    public string? ChipsetName { get; set; }

    public SensorGroup Temperature { get; } = new();
    public SensorGroup Voltage { get; } = new();
    public SensorGroup Fan { get; } = new();
    public SensorGroup Control { get; } = new();
}
