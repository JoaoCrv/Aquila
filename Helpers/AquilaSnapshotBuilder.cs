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

        private static SensorReading? Find(HardwareState data, HardwareType hardwareType, SensorType sensorType, string nameFragment) =>
            FindSensor(FirstHardware(data, hardwareType), sensorType, nameFragment);

        private static SensorReading? FindFirst(HardwareState data, HardwareType hardwareType, SensorType sensorType) =>
            FirstSensor(FirstHardware(data, hardwareType), sensorType);

        private static HardwareDevice? FirstHardware(HardwareState data, HardwareType hardwareType) =>
            data.Devices.FirstOrDefault(h => h.HardwareType == hardwareType);

        private static SensorReading? FindSensor(HardwareDevice? hardware, SensorType sensorType, params string[] nameFragments)
        {
            if (hardware is null)
                return null;

            return hardware.Sensors.FirstOrDefault(sensor =>
                sensor.SensorType == sensorType &&
                nameFragments.Any(fragment => sensor.Name.Contains(fragment, StringComparison.OrdinalIgnoreCase)));
        }

        private static SensorReading? FirstSensor(HardwareDevice? hardware, SensorType sensorType) =>
            hardware?.Sensors
                .Where(sensor => sensor.SensorType == sensorType)
                .OrderBy(sensor => sensor.Index)
                .FirstOrDefault();

        private static SensorReading? IndexedSensor(HardwareDevice? hardware, SensorType sensorType, int index) =>
            hardware?.Sensors
                .Where(sensor => sensor.SensorType == sensorType)
                .OrderBy(sensor => sensor.Index)
                .ElementAtOrDefault(index);

        private static bool ContainsAny(SensorReading sensor, params string[] nameFragments) =>
            nameFragments.Any(fragment => sensor.Name.Contains(fragment, StringComparison.OrdinalIgnoreCase));

        public static AquilaSnapshot Build(HardwareState data, float pageReadsPerSec = 0, float pageWritesPerSec = 0, long cacheBytes = 0) =>
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
