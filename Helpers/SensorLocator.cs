using Aquila.Models;
using LibreHardwareMonitor.Hardware;
using System.Collections.Generic;
using System.Linq;

namespace Aquila.Helpers
{
    public static class SensorLocator
    {
        // ?? Generic lookup ????????????????????????????????????????????????

        public static DataSensor? Find(ComputerData data, HardwareType hardwareType, SensorType sensorType, string nameFragment)
        {
            var hw = data.HardwareList.FirstOrDefault(h => h.HardwareType == hardwareType);
            return hw?.Sensors.FirstOrDefault(s =>
                s.SensorType == sensorType &&
                s.Name.Contains(nameFragment, StringComparison.OrdinalIgnoreCase));
        }

        public static DataSensor? FindFirst(ComputerData data, HardwareType hardwareType, SensorType sensorType)
        {
            var hw = data.HardwareList.FirstOrDefault(h => h.HardwareType == hardwareType);
            return hw?.Sensors.Where(s => s.SensorType == sensorType).OrderBy(s => s.Index).FirstOrDefault();
        }

        // ?? GPU detection ?????????????????????????????????????????????????

        public static HardwareType? DetectGpuType(ComputerData data)
        {
            HardwareType[] gpuTypes = [HardwareType.GpuNvidia, HardwareType.GpuAmd, HardwareType.GpuIntel];
            return gpuTypes.Cast<HardwareType?>().FirstOrDefault(t => data.HardwareList.Any(h => h.HardwareType == t));
        }

        public static IEnumerable<DataHardware> AllGpus(ComputerData data)
        {
            HardwareType[] gpuTypes = [HardwareType.GpuNvidia, HardwareType.GpuAmd, HardwareType.GpuIntel];
            return data.HardwareList.Where(h => gpuTypes.Contains(h.HardwareType));
        }

        // ?? CPU ???????????????????????????????????????????????????????????

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

        public static List<DataSensor> CpuCoreSensors(ComputerData data)
        {
            var hw = data.HardwareList.FirstOrDefault(h => h.HardwareType == HardwareType.Cpu);
            if (hw == null) return [];
            return hw.Sensors
                .Where(s => s.SensorType == SensorType.Load && s.Name.Contains("CPU Core #"))
                .OrderBy(s => s.Index)
                .ToList();
        }

        public static DataSensor? CpuFan(ComputerData data, int index = 0) =>
            data.HardwareList
                .Where(h => h.HardwareType == HardwareType.Motherboard)
                .SelectMany(h => h.Sensors)
                .Where(s => s.SensorType == SensorType.Fan)
                .OrderBy(s => s.Index)
                .ElementAtOrDefault(index);

        // ?? GPU (primary, by type) ????????????????????????????????????????

        public static DataSensor? GpuLoad(ComputerData data)
        {
            var gpuType = DetectGpuType(data);
            return gpuType is null ? null :
                Find(data, gpuType.Value, SensorType.Load, "GPU Core") ??
                FindFirst(data, gpuType.Value, SensorType.Load);
        }

        // ?? GPU helpers for a specific hardware node ??????????????????????

        public static DataSensor? GpuTemperatureFor(DataHardware gpu) =>
            gpu.Sensors.FirstOrDefault(s => s.SensorType == SensorType.Temperature && s.Name.Contains("GPU Core", StringComparison.OrdinalIgnoreCase))
            ?? gpu.Sensors.Where(s => s.SensorType == SensorType.Temperature).OrderBy(s => s.Index).FirstOrDefault();

        public static DataSensor? GpuLoadFor(DataHardware gpu) =>
            gpu.Sensors.FirstOrDefault(s => s.SensorType == SensorType.Load && s.Name.Contains("GPU Core", StringComparison.OrdinalIgnoreCase))
            ?? gpu.Sensors.Where(s => s.SensorType == SensorType.Load).OrderBy(s => s.Index).FirstOrDefault();

        public static DataSensor? GpuClockFor(DataHardware gpu) =>
            gpu.Sensors.FirstOrDefault(s => s.SensorType == SensorType.Clock && s.Name.Contains("GPU Core", StringComparison.OrdinalIgnoreCase))
            ?? gpu.Sensors.Where(s => s.SensorType == SensorType.Clock).OrderBy(s => s.Index).FirstOrDefault();

        public static DataSensor? GpuPowerFor(DataHardware gpu) =>
            gpu.Sensors.FirstOrDefault(s => s.SensorType == SensorType.Power &&
                (s.Name.Contains("GPU Package", StringComparison.OrdinalIgnoreCase) || s.Name.Contains("GPU Total", StringComparison.OrdinalIgnoreCase)))
            ?? gpu.Sensors.Where(s => s.SensorType == SensorType.Power).OrderBy(s => s.Index).FirstOrDefault();

        public static DataSensor? GpuFanFor(DataHardware gpu, int index = 0) =>
            gpu.Sensors.Where(s => s.SensorType == SensorType.Fan).OrderBy(s => s.Index).ElementAtOrDefault(index);

        public static DataSensor? GpuVramUsedFor(DataHardware gpu) =>
            gpu.Sensors.FirstOrDefault(s => s.SensorType == SensorType.SmallData &&
                s.Name.Contains("GPU Memory Used", StringComparison.OrdinalIgnoreCase))
            ?? gpu.Sensors.FirstOrDefault(s => s.SensorType == SensorType.Data &&
                s.Name.Contains("GPU Memory Used", StringComparison.OrdinalIgnoreCase));

        public static DataSensor? GpuVramTotalFor(DataHardware gpu) =>
            gpu.Sensors.FirstOrDefault(s => s.SensorType == SensorType.SmallData &&
                s.Name.Contains("GPU Memory Total", StringComparison.OrdinalIgnoreCase))
            ?? gpu.Sensors.FirstOrDefault(s => s.SensorType == SensorType.Data &&
                s.Name.Contains("GPU Memory Total", StringComparison.OrdinalIgnoreCase));

        public static List<DataSensor> GpuCoreSensors(DataHardware gpu) =>
            gpu.Sensors
                .Where(s => s.SensorType == SensorType.Load &&
                    (s.Name.Contains("GPU Core", StringComparison.OrdinalIgnoreCase) ||
                     s.Name.Contains("3D", StringComparison.OrdinalIgnoreCase) ||
                     s.Name.Contains("Video", StringComparison.OrdinalIgnoreCase) ||
                     s.Name.Contains("Bus", StringComparison.OrdinalIgnoreCase) ||
                     s.Name.Contains("Memory Controller", StringComparison.OrdinalIgnoreCase)))
                .OrderBy(s => s.Index)
                .ToList();

        // ?? RAM ???????????????????????????????????????????????????????????

        public static DataSensor? MemoryLoad(ComputerData data) =>
            Find(data, HardwareType.Memory, SensorType.Load, "Memory") ?? FindFirst(data, HardwareType.Memory, SensorType.Load);

        public static DataSensor? MemoryUsed(ComputerData data) =>
            Find(data, HardwareType.Memory, SensorType.Data, "Memory Used") ?? FindFirst(data, HardwareType.Memory, SensorType.Data);

        public static DataSensor? MemoryAvailable(ComputerData data) =>
            Find(data, HardwareType.Memory, SensorType.Data, "Memory Available");

        public static DataSensor? MemoryPower(ComputerData data) =>
            FindFirst(data, HardwareType.Memory, SensorType.Power);

        public static DataSensor? VirtualMemoryLoad(ComputerData data) =>
            Find(data, HardwareType.Memory, SensorType.Load, "Virtual Memory")
            ?? data.HardwareList.FirstOrDefault(h => h.HardwareType == HardwareType.Memory)
                ?.Sensors.Where(s => s.SensorType == SensorType.Load).OrderBy(s => s.Index).ElementAtOrDefault(1);

        public static DataSensor? VirtualMemoryUsed(ComputerData data) =>
            Find(data, HardwareType.Memory, SensorType.Data, "Virtual Memory Used");

        public static DataSensor? VirtualMemoryAvailable(ComputerData data) =>
            Find(data, HardwareType.Memory, SensorType.Data, "Virtual Memory Available");

        // ?? Fans ??????????????????????????????????????????????????????????

        public static List<DataSensor> MotherboardFans(ComputerData data) =>
            data.HardwareList
                .Where(h => h.HardwareType == HardwareType.Motherboard)
                .SelectMany(h => h.Sensors)
                .Where(s => s.SensorType == SensorType.Fan)
                .OrderBy(s => s.Index)
                .ToList();

        // ?? System temperatures ???????????????????????????????????????????

        public static List<(string Label, DataSensor Sensor)> SystemTemperatures(ComputerData data)
        {
            var results = new List<(string Label, DataSensor Sensor)>();

            if (CpuTemperature(data) is { } cpuTemp)
                results.Add(("CPU", cpuTemp));

            foreach (var gpu in AllGpus(data))
            {
                if (GpuTemperatureFor(gpu) is { } gpuTemp)
                    results.Add(("GPU", gpuTemp));
            }

            var mb = data.HardwareList.FirstOrDefault(h => h.HardwareType == HardwareType.Motherboard);
            if (mb != null)
            {
                foreach (var s in mb.Sensors
                    .Where(s => s.SensorType == SensorType.Temperature)
                    .OrderBy(s => s.Index)
                    .Take(4))
                {
                    results.Add((s.Name, s));
                }
            }

            return results;
        }

        // ?? Network ???????????????????????????????????????????????????????

        public static DataSensor? NetworkUploadSpeed(ComputerData data) =>
            Find(data, HardwareType.Network, SensorType.Throughput, "Upload")
            ?? FindFirst(data, HardwareType.Network, SensorType.Throughput);

        public static DataSensor? NetworkDownloadSpeed(ComputerData data) =>
            Find(data, HardwareType.Network, SensorType.Throughput, "Download")
            ?? data.HardwareList.FirstOrDefault(h => h.HardwareType == HardwareType.Network)
                ?.Sensors.Where(s => s.SensorType == SensorType.Throughput).OrderBy(s => s.Index).ElementAtOrDefault(1);

        public static DataSensor? NetworkDataUploaded(ComputerData data) =>
            Find(data, HardwareType.Network, SensorType.Data, "Data Uploaded")
            ?? FindFirst(data, HardwareType.Network, SensorType.Data);

        public static DataSensor? NetworkDataDownloaded(ComputerData data) =>
            Find(data, HardwareType.Network, SensorType.Data, "Data Downloaded")
            ?? data.HardwareList.FirstOrDefault(h => h.HardwareType == HardwareType.Network)
                ?.Sensors.Where(s => s.SensorType == SensorType.Data).OrderBy(s => s.Index).ElementAtOrDefault(1);

        // ?? Storage ???????????????????????????????????????????????????????????????????

        public static IEnumerable<DataHardware> AllStorageDrives(ComputerData data) =>
            data.HardwareList.Where(h => h.HardwareType == HardwareType.Storage);

        public static DataSensor? StorageTemperatureFor(DataHardware drive) =>
            drive.Sensors.Where(s => s.SensorType == SensorType.Temperature)
                         .OrderBy(s => s.Index).FirstOrDefault();

        public static DataSensor? StorageReadRateFor(DataHardware drive) =>
            drive.Sensors.FirstOrDefault(s => s.SensorType == SensorType.Throughput
                && s.Name.Contains("Read", StringComparison.OrdinalIgnoreCase));

        public static DataSensor? StorageWriteRateFor(DataHardware drive) =>
            drive.Sensors.FirstOrDefault(s => s.SensorType == SensorType.Throughput
                && s.Name.Contains("Write", StringComparison.OrdinalIgnoreCase));

        public static DataSensor? StorageUsedSpaceFor(DataHardware drive) =>
            drive.Sensors.FirstOrDefault(s => s.SensorType == SensorType.Load
                && s.Name.Contains("Used Space", StringComparison.OrdinalIgnoreCase));
    }
}
