using Aquila.Models;
using LibreHardwareMonitor.Hardware;
using System.Collections.Generic;
using System.Linq;

namespace Aquila.Helpers
{
    public static partial class AquilaSnapshotBuilder
    {
        // GPU detection

        private static HardwareType? DetectGpuType(HardwareState data) =>
            PrimaryGpu(data)?.HardwareType;

        private static IEnumerable<HardwareDevice> AllGpus(HardwareState data) =>
            data.Devices.Where(hardware => GpuHardwareTypes.Contains(hardware.HardwareType));

        private static HardwareDevice? PrimaryGpu(HardwareState data) =>
            AllGpus(data)
                .OrderByDescending(gpu => GpuVramTotalFor(gpu)?.Value ?? 0)
                .ThenByDescending(gpu => GpuTypePriority(gpu.HardwareType))
                .ThenByDescending(gpu => GpuPowerFor(gpu)?.Value ?? 0)
                .ThenByDescending(gpu => GpuLoadFor(gpu)?.Value ?? 0)
                .FirstOrDefault();

        private static int GpuTypePriority(HardwareType hardwareType) => hardwareType switch
        {
            HardwareType.GpuNvidia => 3,
            HardwareType.GpuAmd => 2,
            HardwareType.GpuIntel => 1,
            _ => 0
        };

        private static SensorReading? GpuLoad(HardwareState data) =>
            PrimaryGpu(data) is { } gpu
                ? GpuLoadFor(gpu)
                : null;

        private static SensorReading? GpuTemperatureFor(HardwareDevice gpu) =>
            FindSensor(gpu, SensorType.Temperature, "GPU Core")
            ?? FirstSensor(gpu, SensorType.Temperature);

        private static SensorReading? GpuLoadFor(HardwareDevice gpu) =>
            FindSensor(gpu, SensorType.Load, "GPU Core")
            ?? FirstSensor(gpu, SensorType.Load);

        private static SensorReading? GpuClockFor(HardwareDevice gpu) =>
            FindSensor(gpu, SensorType.Clock, "GPU Core")
            ?? FirstSensor(gpu, SensorType.Clock);

        private static SensorReading? GpuPowerFor(HardwareDevice gpu) =>
            FindSensor(gpu, SensorType.Power, "GPU Package", "GPU Total")
            ?? FirstSensor(gpu, SensorType.Power);

        private static SensorReading? GpuFanFor(HardwareDevice gpu, int index = 0) =>
            IndexedSensor(gpu, SensorType.Fan, index);

        private static SensorReading? GpuVramUsedFor(HardwareDevice gpu) =>
            FindSensor(gpu, SensorType.SmallData, "GPU Memory Used")
            ?? FindSensor(gpu, SensorType.Data, "GPU Memory Used");

        private static SensorReading? GpuVramTotalFor(HardwareDevice gpu) =>
            FindSensor(gpu, SensorType.SmallData, "GPU Memory Total")
            ?? FindSensor(gpu, SensorType.Data, "GPU Memory Total");

        private static List<SensorReading> GpuCoreSensors(HardwareDevice gpu) =>
            gpu.Sensors
                .Where(sensor => sensor.SensorType == SensorType.Load &&
                    ContainsAny(sensor, "GPU Core", "3D", "Video", "Bus", "Memory Controller"))
                .OrderBy(sensor => sensor.Index)
                .ToList();

        private static GpuSnapshot? BuildPrimaryGpuSnapshot(HardwareState data)
        {
            var primaryGpu = PrimaryGpu(data);
            if (primaryGpu is null)
                return null;

            return new GpuSnapshot
            {
                Identifier = primaryGpu.Identifier,
                Name = primaryGpu.Name,
                Temperature = MetricValue.FromSensor(GpuTemperatureFor(primaryGpu)),
                Load = MetricValue.FromSensor(GpuLoadFor(primaryGpu)),
                Clock = MetricValue.FromSensor(GpuClockFor(primaryGpu)),
                Power = MetricValue.FromSensor(GpuPowerFor(primaryGpu)),
                FanRpm = MetricValue.FromSensor(GpuFanFor(primaryGpu, 0)),
                Fan2Rpm = MetricValue.FromSensor(GpuFanFor(primaryGpu, 1)),
                VramUsed = MetricValue.FromSensor(GpuVramUsedFor(primaryGpu)),
                VramTotal = MetricValue.FromSensor(GpuVramTotalFor(primaryGpu))
            };
        }
    }
}
