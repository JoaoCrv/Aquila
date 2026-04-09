using Aquila.Models;
using LibreHardwareMonitor.Hardware;
using System.Collections.Generic;
using System.Linq;

namespace Aquila.Helpers
{
    public static partial class AquilaSnapshotBuilder
    {
        // GPU detection

        private static HardwareType? DetectGpuType(ComputerData data) =>
            PrimaryGpu(data)?.HardwareType;

        private static IEnumerable<DataHardware> AllGpus(ComputerData data) =>
            data.HardwareList.Where(hardware => GpuHardwareTypes.Contains(hardware.HardwareType));

        private static DataHardware? PrimaryGpu(ComputerData data) =>
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

        private static DataSensor? GpuLoad(ComputerData data) =>
            PrimaryGpu(data) is { } gpu
                ? GpuLoadFor(gpu)
                : null;

        private static DataSensor? GpuTemperatureFor(DataHardware gpu) =>
            FindSensor(gpu, SensorType.Temperature, "GPU Core")
            ?? FirstSensor(gpu, SensorType.Temperature);

        private static DataSensor? GpuLoadFor(DataHardware gpu) =>
            FindSensor(gpu, SensorType.Load, "GPU Core")
            ?? FirstSensor(gpu, SensorType.Load);

        private static DataSensor? GpuClockFor(DataHardware gpu) =>
            FindSensor(gpu, SensorType.Clock, "GPU Core")
            ?? FirstSensor(gpu, SensorType.Clock);

        private static DataSensor? GpuPowerFor(DataHardware gpu) =>
            FindSensor(gpu, SensorType.Power, "GPU Package", "GPU Total")
            ?? FirstSensor(gpu, SensorType.Power);

        private static DataSensor? GpuFanFor(DataHardware gpu, int index = 0) =>
            IndexedSensor(gpu, SensorType.Fan, index);

        private static DataSensor? GpuVramUsedFor(DataHardware gpu) =>
            FindSensor(gpu, SensorType.SmallData, "GPU Memory Used")
            ?? FindSensor(gpu, SensorType.Data, "GPU Memory Used");

        private static DataSensor? GpuVramTotalFor(DataHardware gpu) =>
            FindSensor(gpu, SensorType.SmallData, "GPU Memory Total")
            ?? FindSensor(gpu, SensorType.Data, "GPU Memory Total");

        private static List<DataSensor> GpuCoreSensors(DataHardware gpu) =>
            gpu.Sensors
                .Where(sensor => sensor.SensorType == SensorType.Load &&
                    ContainsAny(sensor, "GPU Core", "3D", "Video", "Bus", "Memory Controller"))
                .OrderBy(sensor => sensor.Index)
                .ToList();

        private static GpuSnapshot? BuildPrimaryGpuSnapshot(ComputerData data)
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
