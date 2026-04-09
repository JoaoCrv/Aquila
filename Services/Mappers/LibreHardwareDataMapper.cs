using Aquila.Models;
using LibreHardwareMonitor.Hardware;
using System.Collections.Generic;
using System.Linq;

namespace Aquila.Services
{
    /// <summary>
    /// Maps raw LibreHardwareMonitor objects into Aquila's provider-neutral raw `HardwareState` store.
    /// This keeps provider-specific translation isolated from the rest of the app.
    /// </summary>
    public sealed class LibreHardwareDataMapper
    {
        public void UpdateFromHardware(HardwareState hardwareState, IEnumerable<IHardware> hardwareItems)
        {
            foreach (var rawHardware in hardwareItems)
            {
                var hardwareDevice = GetOrCreateHardware(hardwareState, rawHardware);
                var allSensors = rawHardware.Sensors.Concat(rawHardware.SubHardware.SelectMany(s => s.Sensors));

                foreach (var rawSensor in allSensors)
                {
                    var sensorReading = GetOrCreateSensor(hardwareState, hardwareDevice, rawSensor);
                    sensorReading.Value = rawSensor.Value ?? 0;
                    sensorReading.Min = rawSensor.Min ?? 0;
                    sensorReading.Max = rawSensor.Max ?? 0;
                }
            }
        }

        private static HardwareDevice GetOrCreateHardware(HardwareState hardwareState, IHardware rawHardware)
        {
            var identifier = rawHardware.Identifier.ToString();
            var hardwareDevice = hardwareState.Devices.FirstOrDefault(h => h.Identifier == identifier);

            if (hardwareDevice is null)
            {
                hardwareDevice = new HardwareDevice(identifier, rawHardware.Name, rawHardware.HardwareType);
                hardwareState.Devices.Add(hardwareDevice);
            }

            return hardwareDevice;
        }

        private static SensorReading GetOrCreateSensor(HardwareState hardwareState, HardwareDevice hardwareDevice, ISensor rawSensor)
        {
            var sensorId = rawSensor.Identifier.ToString();
            if (hardwareState.SensorsById.TryGetValue(sensorId, out var existingSensor))
                return existingSensor;

            var sensorReading = new SensorReading(
                rawSensor.Index,
                sensorId,
                rawSensor.Name,
                rawSensor.SensorType,
                GetSensorUnit(rawSensor.SensorType));

            hardwareState.SensorsById[sensorId] = sensorReading;
            hardwareDevice.Sensors.Add(sensorReading);
            return sensorReading;
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
