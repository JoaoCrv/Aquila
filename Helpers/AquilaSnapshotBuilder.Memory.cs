using Aquila.Models;
using LibreHardwareMonitor.Hardware;

namespace Aquila.Helpers
{
    public static partial class AquilaSnapshotBuilder
    {
        // Memory

        private static DataSensor? MemoryLoad(ComputerData data) =>
            Find(data, HardwareType.Memory, SensorType.Load, "Memory")
            ?? FindFirst(data, HardwareType.Memory, SensorType.Load);

        private static DataSensor? MemoryUsed(ComputerData data) =>
            Find(data, HardwareType.Memory, SensorType.Data, "Memory Used")
            ?? FindFirst(data, HardwareType.Memory, SensorType.Data);

        private static DataSensor? MemoryAvailable(ComputerData data) =>
            Find(data, HardwareType.Memory, SensorType.Data, "Memory Available");

        private static DataSensor? MemoryPower(ComputerData data) =>
            FindFirst(data, HardwareType.Memory, SensorType.Power);

        private static DataSensor? VirtualMemoryLoad(ComputerData data) =>
            Find(data, HardwareType.Memory, SensorType.Load, "Virtual Memory")
            ?? IndexedSensor(FirstHardware(data, HardwareType.Memory), SensorType.Load, 1);

        private static DataSensor? VirtualMemoryUsed(ComputerData data) =>
            Find(data, HardwareType.Memory, SensorType.Data, "Virtual Memory Used");

        private static DataSensor? VirtualMemoryAvailable(ComputerData data) =>
            Find(data, HardwareType.Memory, SensorType.Data, "Virtual Memory Available");

        private static MemorySnapshot BuildMemorySnapshot(ComputerData data, float pageReadsPerSec, float pageWritesPerSec, long cacheBytes) =>
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
