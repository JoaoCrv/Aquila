using Aquila.Models;

namespace Aquila.Helpers
{
    public static partial class AquilaSnapshotBuilder
    {
        private static PowerSnapshot BuildPowerSnapshot(ComputerData data)
        {
            var cpuPower = CpuPower(data);
            var memoryPower = MemoryPower(data);
            var gpuPower = PrimaryGpu(data) is { } primaryGpu ? GpuPowerFor(primaryGpu) : null;
            var totalPower = (cpuPower?.Value ?? 0) + (memoryPower?.Value ?? 0) + (gpuPower?.Value ?? 0);

            return new PowerSnapshot
            {
                Cpu = MetricValue.FromSensor(cpuPower),
                Memory = MetricValue.FromSensor(memoryPower),
                Gpu = MetricValue.FromSensor(gpuPower),
                Total = MetricValue.FromValue(totalPower, "W", "Aquila total")
            };
        }
    }
}
