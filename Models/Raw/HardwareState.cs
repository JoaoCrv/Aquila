using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Aquila.Models
{
    /// <summary>
    /// Mutable in-memory store of the latest raw hardware state used by Explorer and snapshot building.
    /// </summary>
    public class HardwareState
    {
        public ObservableCollection<HardwareDevice> Devices { get; } = [];
        public Dictionary<string, SensorReading> SensorsById { get; } = [];
    }
}