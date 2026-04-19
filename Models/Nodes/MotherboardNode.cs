using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Aquila.Models.Nodes;

public class MotherboardNode
{
    public string? Name { get; set; }
    public string? ChipsetName { get; set; }
    public string? Manufacturer { get; set; }
    public string? Model { get; set; }
    public string? SerialNumber { get; set; }

    public SensorGroup Temperature { get; } = new();
    public SensorGroup Voltage { get; } = new();
    public SensorGroup Fan { get; } = new();
    public SensorGroup Control { get; } = new();
}
