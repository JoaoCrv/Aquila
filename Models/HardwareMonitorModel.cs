using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.Generic;
using LibreHardwareMonitor.Hardware;

namespace Aquila.Models
{
    // This is the sensor model that the UI will consume.
    public partial class Sensor : ObservableObject
    {
        public int Index { get; }
        public string Identifier { get; }
        public string Name { get; }
        public SensorType SensorType { get; }

        [ObservableProperty] private float _value;
        [ObservableProperty] private float _min;
        [ObservableProperty] private float _max;
        [ObservableProperty] private string? _unit;

        /// We use a constructor to set the immutable properties once.
        public Sensor(int index, string identifier, string name, SensorType sensorType, string? unit)
        {
            Index = index;
            Identifier = identifier;
            Name = name;
            SensorType = sensorType;
            Unit = unit;
        }
    }

    // The hardware that the UI will consume.
    public class Hardware(int index, string identifier, string name, HardwareType hardwareType)
    {
        public int Index { get; } = index;
        public string Identifier { get; } = identifier;
        public string Name { get; } = name;
        public HardwareType HardwareType { get; } = hardwareType;


        // Dictionary of sensors for quick access. The key is the Sensor Index (int).
        public Dictionary<int, Sensor> Sensors { get; } = [];
    }

    // The root of our API. The key is the HardwareType (enum).
    public class HardwareRoot
    {
        public Dictionary<HardwareType, Dictionary<int, Hardware>> Data { get; } = new();
    }
}