using Aquila.Models;
using LibreHardwareMonitor.Hardware;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Aquila.Helpers
{
    /// <summary>
    /// Builds Aquila's semantic snapshot from the app's internal raw hardware model.
    /// This keeps UI-facing semantics independent from any specific provider or SDK.
    /// </summary>
    public static partial class AquilaSnapshotBuilder
    {
        private static readonly HardwareType[] GpuHardwareTypes =
        [
            HardwareType.GpuNvidia,
            HardwareType.GpuAmd,
            HardwareType.GpuIntel
        ];

        // Internal lookup helpers

        private static DataSensor? Find(ComputerData data, HardwareType hardwareType, SensorType sensorType, string nameFragment) =>
            FindSensor(FirstHardware(data, hardwareType), sensorType, nameFragment);

        private static DataSensor? FindFirst(ComputerData data, HardwareType hardwareType, SensorType sensorType) =>
            FirstSensor(FirstHardware(data, hardwareType), sensorType);

        private static DataHardware? FirstHardware(ComputerData data, HardwareType hardwareType) =>
            data.HardwareList.FirstOrDefault(h => h.HardwareType == hardwareType);

        private static DataSensor? FindSensor(DataHardware? hardware, SensorType sensorType, params string[] nameFragments)
        {
            if (hardware is null)
                return null;

            return hardware.Sensors.FirstOrDefault(sensor =>
                sensor.SensorType == sensorType &&
                nameFragments.Any(fragment => sensor.Name.Contains(fragment, StringComparison.OrdinalIgnoreCase)));
        }

        private static DataSensor? FirstSensor(DataHardware? hardware, SensorType sensorType) =>
            hardware?.Sensors
                .Where(sensor => sensor.SensorType == sensorType)
                .OrderBy(sensor => sensor.Index)
                .FirstOrDefault();

        private static DataSensor? IndexedSensor(DataHardware? hardware, SensorType sensorType, int index) =>
            hardware?.Sensors
                .Where(sensor => sensor.SensorType == sensorType)
                .OrderBy(sensor => sensor.Index)
                .ElementAtOrDefault(index);

        private static bool ContainsAny(DataSensor sensor, params string[] nameFragments) =>
            nameFragments.Any(fragment => sensor.Name.Contains(fragment, StringComparison.OrdinalIgnoreCase));

        public static AquilaSnapshot Build(ComputerData data, float pageReadsPerSec = 0, float pageWritesPerSec = 0, long cacheBytes = 0) =>
            new()
            {
                Cpu = BuildCpuSnapshot(data),
                Gpu = new GpuCollectionSnapshot { Primary = BuildPrimaryGpuSnapshot(data) },
                Memory = BuildMemorySnapshot(data, pageReadsPerSec, pageWritesPerSec, cacheBytes),
                Power = BuildPowerSnapshot(data),
                Network = BuildNetworkSnapshot(data),
                Storage = BuildStorageSnapshots(data),
                Temperatures = BuildTemperatureSnapshots(data),
                Fans = BuildFanSnapshots(data)
            };
    }
}
