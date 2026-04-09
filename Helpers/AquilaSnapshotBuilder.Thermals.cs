using Aquila.Models;
using LibreHardwareMonitor.Hardware;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Aquila.Helpers
{
    public static partial class AquilaSnapshotBuilder
    {
        // Fans and temperatures

        private static List<DataSensor> MotherboardFans(ComputerData data) =>
            data.HardwareList
                .Where(hardware => hardware.HardwareType == HardwareType.Motherboard)
                .SelectMany(hardware => hardware.Sensors)
                .Where(sensor => sensor.SensorType == SensorType.Fan)
                .OrderBy(sensor => sensor.Index)
                .ToList();

        private static List<(string Label, DataSensor Sensor)> SystemTemperatures(ComputerData data)
        {
            var results = new List<(string Label, DataSensor Sensor)>();

            if (CpuTemperature(data) is { } cpuTemp)
                results.Add(("CPU", cpuTemp));

            if (PrimaryGpu(data) is { } gpu && GpuTemperatureFor(gpu) is { } gpuTemp)
                results.Add(("GPU", gpuTemp));

            var motherboard = FirstHardware(data, HardwareType.Motherboard);
            if (motherboard != null)
            {
                foreach (var sensor in motherboard.Sensors
                    .Where(sensor => sensor.SensorType == SensorType.Temperature
                                  && sensor.Value > 0
                                  && !sensor.Name.Equals("CPU", StringComparison.OrdinalIgnoreCase)
                                  && !sensor.Name.StartsWith("CPU ", StringComparison.OrdinalIgnoreCase))
                    .OrderBy(sensor => sensor.Index)
                    .Take(4))
                {
                    results.Add((sensor.Name, sensor));
                }
            }

            return results;
        }

        private static IReadOnlyList<TemperatureSnapshot> BuildTemperatureSnapshots(ComputerData data) =>
            SystemTemperatures(data)
                .Select(item => new TemperatureSnapshot
                {
                    Label = item.Label,
                    Value = MetricValue.FromSensor(item.Sensor)
                })
                .ToList();

        private static IReadOnlyList<FanSnapshot> BuildFanSnapshots(ComputerData data) =>
            MotherboardFans(data)
                .Select(sensor => new FanSnapshot
                {
                    Name = sensor.Name,
                    Speed = MetricValue.FromSensor(sensor)
                })
                .ToList();
    }
}
