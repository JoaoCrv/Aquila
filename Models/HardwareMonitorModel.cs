using CommunityToolkit.Mvvm.ComponentModel;
using HidSharp.Reports.Units;
using LibreHardwareMonitor.Hardware;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Aquila.Models
{
    /// <summary>
    ///     The nomenclature is designed to avoid conflict with Services and Librehardwaremonitor.
    /// </summary>
    public partial class DataSensor : ObservableObject
    {
        public int Index { get; }
        public string Identifier { get; }
        public string Name { get; }
        public SensorType SensorType { get; }

        [ObservableProperty] private float _value;
        [ObservableProperty] private float _min;
        [ObservableProperty] private float _max;
        [ObservableProperty] private string? _unit;

        public DataSensor(int index, string identifier, string name, SensorType sensorType, string? unit)
        {
            Index = index;
            Identifier = identifier;
            Name = name;
            SensorType = sensorType;
            Unit = unit;
        }
    }

    public class DataHardware(string identifier, string name, HardwareType hardwareType)
    {
        public string Identifier { get; } = identifier;
        public string Name { get; } = name;
        public HardwareType HardwareType { get; } = hardwareType;
        // One single list of ALL sensors that belong to this hardware.
        public ObservableCollection<DataSensor> Sensors { get; } = [];
    }

    // The root of our model. Contains the list and the index.
    public class ComputerData
    {
        // The hierarchical list, perfect for the ExplorerPage.
        public ObservableCollection<DataHardware> HardwareList { get; } = [];

        // Global index, perfect for Dashboard widgets.
        // The key is the Identifier (string), the value is the DataSensor object.
        public Dictionary<string, DataSensor> SensorIndex { get; } = [];
    }
}