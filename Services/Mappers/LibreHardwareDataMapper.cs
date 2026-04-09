using Aquila.Models;
using LibreHardwareMonitor.Hardware;
using System.Collections.Generic;
using System.Linq;

namespace Aquila.Services
{
    /// <summary>
    /// Maps raw LibreHardwareMonitor objects into Aquila's provider-neutral raw `ComputerData` store.
    /// This keeps provider-specific translation isolated from the rest of the app.
    /// </summary>
    public sealed class LibreHardwareDataMapper
    {
        public void UpdateFromHardware(ComputerData computerData, IEnumerable<IHardware> hardwareItems)
        {
            foreach (var rawHardware in hardwareItems)
            {
                var hardwareNode = GetOrCreateHardware(computerData, rawHardware);
                var allSensors = rawHardware.Sensors.Concat(rawHardware.SubHardware.SelectMany(s => s.Sensors));

                foreach (var rawSensor in allSensors)
                {
                    var dataSensor = GetOrCreateSensor(computerData, hardwareNode, rawSensor);
                    dataSensor.Value = rawSensor.Value ?? 0;
                    dataSensor.Min = rawSensor.Min ?? 0;
                    dataSensor.Max = rawSensor.Max ?? 0;
                }
            }
        }

        private static DataHardware GetOrCreateHardware(ComputerData computerData, IHardware rawHardware)
        {
            var identifier = rawHardware.Identifier.ToString();
            var hardwareNode = computerData.HardwareList.FirstOrDefault(h => h.Identifier == identifier);

            if (hardwareNode is null)
            {
                hardwareNode = new DataHardware(identifier, rawHardware.Name, rawHardware.HardwareType);
                computerData.HardwareList.Add(hardwareNode);
            }

            return hardwareNode;
        }

        private static DataSensor GetOrCreateSensor(ComputerData computerData, DataHardware hardwareNode, ISensor rawSensor)
        {
            var sensorId = rawSensor.Identifier.ToString();
            if (computerData.SensorIndex.TryGetValue(sensorId, out var existingSensor))
                return existingSensor;

            var dataSensor = new DataSensor(
                rawSensor.Index,
                sensorId,
                rawSensor.Name,
                rawSensor.SensorType,
                GetSensorUnit(rawSensor.SensorType));

            computerData.SensorIndex[sensorId] = dataSensor;
            hardwareNode.Sensors.Add(dataSensor);
            return dataSensor;
        }

        private static string GetSensorUnit(SensorType type) => type switch
        {
            SensorType.Temperature => "°C",
            SensorType.Load => "%",
            SensorType.Clock => "MHz",
            SensorType.Power => "W",
            SensorType.Fan => "RPM",
            SensorType.Data => "GB",
            SensorType.SmallData => "MB",
            SensorType.Throughput => "B/s",
            SensorType.Voltage => "V",
            SensorType.Frequency => "Hz",
            SensorType.Control => "%",
            _ => string.Empty
        };
    }
}
