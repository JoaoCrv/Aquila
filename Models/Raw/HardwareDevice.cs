using LibreHardwareMonitor.Hardware;
using System.Collections.ObjectModel;

namespace Aquila.Models
{
    /// <summary>
    /// Provider-neutral raw hardware node with its current sensor readings.
    /// </summary>
    public class HardwareDevice(string identifier, string name, HardwareType hardwareType)
    {
        public string Identifier { get; } = identifier;
        public string Name { get; } = name;
        public HardwareType HardwareType { get; } = hardwareType;
        public ObservableCollection<SensorReading> Sensors { get; } = [];
    }
}