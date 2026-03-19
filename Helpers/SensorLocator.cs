using Aquila.Models;
using LibreHardwareMonitor.Hardware;
using System.Linq;

namespace Aquila.Helpers
{
    /// <summary>
    /// Finds sensors dynamically by hardware type, sensor type and name pattern.
    /// Replaces hardcoded sensor identifier strings — works on any machine regardless
    /// of CPU/GPU vendor or motherboard chipset.
    /// </summary>
    public static class SensorLocator
    {
        // ?? Generic lookup ????????????????????????????????????????????????????

        /// <summary>
        /// Returns the first sensor of <paramref name="sensorType"/> from the first
        /// hardware of <paramref name="hardwareType"/> whose name contains
        /// <paramref name="nameFragment"/> (case-insensitive). Returns null if not found.
        /// </summary>
        public static DataSensor? Find(
            ComputerData data,
            HardwareType hardwareType,
            SensorType sensorType,
            string nameFragment)
        {
            var hw = data.HardwareList.FirstOrDefault(h => h.HardwareType == hardwareType);
            return hw?.Sensors.FirstOrDefault(s =>
                s.SensorType == sensorType &&
                s.Name.Contains(nameFragment, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Returns the first sensor of <paramref name="sensorType"/> from the first
        /// hardware of <paramref name="hardwareType"/>, ordered by sensor index.
        /// Useful when the name is not predictable but the order is stable.
        /// </summary>
        public static DataSensor? FindFirst(
            ComputerData data,
            HardwareType hardwareType,
            SensorType sensorType)
        {
            var hw = data.HardwareList.FirstOrDefault(h => h.HardwareType == hardwareType);
            return hw?.Sensors
                .Where(s => s.SensorType == sensorType)
                .OrderBy(s => s.Index)
                .FirstOrDefault();
        }

        // ?? GPU vendor detection ??????????????????????????????????????????????

        /// <summary>
        /// Returns the HardwareType of the first GPU found, regardless of vendor.
        /// Returns null if no GPU is detected yet.
        /// </summary>
        public static HardwareType? DetectGpuType(ComputerData data)
        {
            HardwareType[] gpuTypes = [HardwareType.GpuNvidia, HardwareType.GpuAmd, HardwareType.GpuIntel];
            return gpuTypes.Cast<HardwareType?>()
                .FirstOrDefault(t => data.HardwareList.Any(h => h.HardwareType == t));
        }

        // ?? CPU ???????????????????????????????????????????????????????????????

        public static DataSensor? CpuTemperature(ComputerData data) =>
            Find(data, HardwareType.Cpu, SensorType.Temperature, "Package")
            ?? FindFirst(data, HardwareType.Cpu, SensorType.Temperature);

        public static DataSensor? CpuLoad(ComputerData data) =>
            Find(data, HardwareType.Cpu, SensorType.Load, "CPU Total")
            ?? Find(data, HardwareType.Cpu, SensorType.Load, "Total")
            ?? FindFirst(data, HardwareType.Cpu, SensorType.Load);

        public static DataSensor? CpuPower(ComputerData data) =>
            Find(data, HardwareType.Cpu, SensorType.Power, "Package")
            ?? Find(data, HardwareType.Cpu, SensorType.Power, "Total")
            ?? FindFirst(data, HardwareType.Cpu, SensorType.Power);

        // CPU fans come from the motherboard (LPC chipset), not from the CPU hardware node.
        // We look in Motherboard hardware and find fans by name pattern.
        public static DataSensor? CpuFan(ComputerData data, int index = 0) =>
            data.HardwareList
                .Where(h => h.HardwareType == HardwareType.Motherboard)
                .SelectMany(h => h.Sensors)
                .Where(s => s.SensorType == SensorType.Fan)
                .OrderBy(s => s.Index)
                .ElementAtOrDefault(index);

        // ?? GPU ???????????????????????????????????????????????????????????????

        public static DataSensor? GpuTemperature(ComputerData data)
        {
            var gpuType = DetectGpuType(data);
            if (gpuType is null) return null;
            return Find(data, gpuType.Value, SensorType.Temperature, "GPU Core")
                ?? FindFirst(data, gpuType.Value, SensorType.Temperature);
        }

        public static DataSensor? GpuLoad(ComputerData data)
        {
            var gpuType = DetectGpuType(data);
            if (gpuType is null) return null;
            return Find(data, gpuType.Value, SensorType.Load, "GPU Core")
                ?? FindFirst(data, gpuType.Value, SensorType.Load);
        }

        public static DataSensor? GpuClock(ComputerData data)
        {
            var gpuType = DetectGpuType(data);
            if (gpuType is null) return null;
            return Find(data, gpuType.Value, SensorType.Clock, "GPU Core")
                ?? FindFirst(data, gpuType.Value, SensorType.Clock);
        }

        public static DataSensor? GpuPower(ComputerData data)
        {
            var gpuType = DetectGpuType(data);
            if (gpuType is null) return null;
            return Find(data, gpuType.Value, SensorType.Power, "GPU Package")
                ?? Find(data, gpuType.Value, SensorType.Power, "GPU Total")
                ?? FindFirst(data, gpuType.Value, SensorType.Power);
        }

        public static DataSensor? GpuFan(ComputerData data, int index = 0)
        {
            var gpuType = DetectGpuType(data);
            if (gpuType is null) return null;
            return data.HardwareList
                .Where(h => h.HardwareType == gpuType.Value)
                .SelectMany(h => h.Sensors)
                .Where(s => s.SensorType == SensorType.Fan)
                .OrderBy(s => s.Index)
                .ElementAtOrDefault(index);
        }

        // ?? RAM ???????????????????????????????????????????????????????????????

        public static DataSensor? MemoryLoad(ComputerData data) =>
            Find(data, HardwareType.Memory, SensorType.Load, "Memory")
            ?? FindFirst(data, HardwareType.Memory, SensorType.Load);

        public static DataSensor? MemoryUsed(ComputerData data) =>
            Find(data, HardwareType.Memory, SensorType.Data, "Memory Used")
            ?? FindFirst(data, HardwareType.Memory, SensorType.Data);

        public static DataSensor? MemoryAvailable(ComputerData data) =>
            Find(data, HardwareType.Memory, SensorType.Data, "Memory Available");

        public static DataSensor? VirtualMemoryLoad(ComputerData data) =>
            Find(data, HardwareType.Memory, SensorType.Load, "Virtual Memory")
            ?? data.HardwareList
                .FirstOrDefault(h => h.HardwareType == HardwareType.Memory)
                ?.Sensors
                .Where(s => s.SensorType == SensorType.Load)
                .OrderBy(s => s.Index)
                .ElementAtOrDefault(1);

        public static DataSensor? VirtualMemoryUsed(ComputerData data) =>
            Find(data, HardwareType.Memory, SensorType.Data, "Virtual Memory Used");

        public static DataSensor? VirtualMemoryAvailable(ComputerData data) =>
            Find(data, HardwareType.Memory, SensorType.Data, "Virtual Memory Available");

        // ?? Network ???????????????????????????????????????????????????????????

        public static DataSensor? NetworkUploadSpeed(ComputerData data) =>
            Find(data, HardwareType.Network, SensorType.Throughput, "Upload")
            ?? FindFirst(data, HardwareType.Network, SensorType.Throughput);

        public static DataSensor? NetworkDownloadSpeed(ComputerData data) =>
            Find(data, HardwareType.Network, SensorType.Throughput, "Download")
            ?? data.HardwareList
                .FirstOrDefault(h => h.HardwareType == HardwareType.Network)
                ?.Sensors
                .Where(s => s.SensorType == SensorType.Throughput)
                .OrderBy(s => s.Index)
                .ElementAtOrDefault(1);

        public static DataSensor? NetworkDataUploaded(ComputerData data) =>
            Find(data, HardwareType.Network, SensorType.Data, "Data Uploaded")
            ?? FindFirst(data, HardwareType.Network, SensorType.Data);

        public static DataSensor? NetworkDataDownloaded(ComputerData data) =>
            Find(data, HardwareType.Network, SensorType.Data, "Data Downloaded")
            ?? data.HardwareList
                .FirstOrDefault(h => h.HardwareType == HardwareType.Network)
                ?.Sensors
                .Where(s => s.SensorType == SensorType.Data)
                .OrderBy(s => s.Index)
                .ElementAtOrDefault(1);
    }
}
