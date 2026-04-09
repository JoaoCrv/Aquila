using Aquila.Models;
using LibreHardwareMonitor.Hardware;

namespace Aquila.Helpers
{
    public static partial class AquilaSnapshotBuilder
    {
        // Memory

        private static SensorReading? MemoryLoad(HardwareState data) =>
            Find(data, HardwareType.Memory, SensorType.Load, "Memory")
            ?? FindFirst(data, HardwareType.Memory, SensorType.Load);

        private static SensorReading? MemoryUsed(HardwareState data) =>
            Find(data, HardwareType.Memory, SensorType.Data, "Memory Used")
            ?? FindFirst(data, HardwareType.Memory, SensorType.Data);

        private static SensorReading? MemoryAvailable(HardwareState data) =>
            Find(data, HardwareType.Memory, SensorType.Data, "Memory Available");

        private static SensorReading? MemoryPower(HardwareState data) =>
            FindFirst(data, HardwareType.Memory, SensorType.Power);

        private static SensorReading? VirtualMemoryLoad(HardwareState data) =>
            Find(data, HardwareType.Memory, SensorType.Load, "Virtual Memory")
            ?? IndexedSensor(FirstHardware(data, HardwareType.Memory), SensorType.Load, 1);

        private static SensorReading? VirtualMemoryUsed(HardwareState data) =>
            Find(data, HardwareType.Memory, SensorType.Data, "Virtual Memory Used");

        private static SensorReading? VirtualMemoryAvailable(HardwareState data) =>
            Find(data, HardwareType.Memory, SensorType.Data, "Virtual Memory Available");

        private static MemorySnapshot BuildMemorySnapshot(HardwareState data, float pageReadsPerSec, float pageWritesPerSec, long cacheBytes) =>
            new()
            {
                LoadPercent = MetricValue.FromSensor(MemoryLoad(data)),
                UsedGb = MetricValue.FromSensor(MemoryUsed(data)),
                AvailableGb = MetricValue.FromSensor(MemoryAvailable(data)),
                Power = MetricValue.FromSensor(MemoryPower(data)),
                VirtualUsedGb = MetricValue.FromSensor(VirtualMemoryUsed(data)),
                VirtualAvailableGb = MetricValue.FromSensor(VirtualMemoryAvailable(data)),
                PageReadsPerSec = MetricValue.FromValue(pageReadsPerSec, "reads/s", "Windows PDH"),
                PageWritesPerSec = MetricValue.FromValue(pageWritesPerSec, "writes/s", "Windows PDH"),
                CacheBytes = MetricValue.FromValue(cacheBytes, "B", "Windows Performance Info")
            };

    }
}
