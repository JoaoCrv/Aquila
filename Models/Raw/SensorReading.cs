using CommunityToolkit.Mvvm.ComponentModel;
using LibreHardwareMonitor.Hardware;

namespace Aquila.Models
{
    /// <summary>
    /// Mutable raw reading for a single hardware sensor inside Aquila's internal hardware state.
    /// </summary>
    public partial class SensorReading : ObservableObject
    {
        public int Index { get; }
        public string Identifier { get; }
        public string Name { get; }
        public SensorType SensorType { get; }

        [ObservableProperty] private float _value;
        [ObservableProperty] private float _min;
        [ObservableProperty] private float _max;
        [ObservableProperty] private string? _unit;

        public SensorReading(int index, string identifier, string name, SensorType sensorType, string? unit)
        {
            Index = index;
            Identifier = identifier;
            Name = name;
            SensorType = sensorType;
            Unit = unit;
        }
    }
}