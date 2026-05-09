using Aquila.Models;
using Aquila.Models.Nodes;
using LibreHardwareMonitor.Hardware;
using System.Collections.Generic;
using System.Linq;

namespace Aquila.Services.LibreHardwareMonitor;

public class LHMTranslater
{
    public void Translate(IEnumerable<IHardware> hardware, AquilaState state)
    {
        // índices para listas dinâmicas
        int cpuIndex = 0;
        int gpuIndex = 0;
        int networkIndex = 0;
        int storageIndex = 0;

        foreach (var hw in hardware)
        {
            switch (hw.HardwareType)
            {
                case HardwareType.Cpu:
                    TranslateCpu(hw, GetOrCreate(state.Hardware.Cpus, cpuIndex++));
                    break;

                case HardwareType.Memory:
                    TranslateMemory(hw, state.Hardware.Memory);
                    break;

                case HardwareType.Motherboard:
                    TranslateMotherboard(hw, state.Hardware.Motherboard);
                    break;

                case HardwareType.GpuNvidia:
                case HardwareType.GpuAmd:
                case HardwareType.GpuIntel:
                    TranslateGpu(hw, GetOrCreate(state.Hardware.Gpus, gpuIndex++));
                    break;

                case HardwareType.Network:
                    TranslateNetwork(hw, GetOrCreate(state.Hardware.Networks, networkIndex++));
                    break;

                case HardwareType.Storage:
                    TranslateStorage(hw, GetOrCreate(state.Hardware.Storages, storageIndex++));
                    break;
            }
        }

        FillDerived(state);
    }

    private static void FillDerived(AquilaState state)
    {
        var mem = state.Hardware.Memory.Data;
        if (mem.Used.Value.HasValue || mem.Available.Value.HasValue)
        {
            mem.Total.Value = (mem.Used.Value ?? 0) + (mem.Available.Value ?? 0);
            mem.Total.Unit  = "GB";
        }

        var cpuPower = state.Hardware.Cpus.Sum(c => c.Power.Package.Value ?? 0);
        var gpuPower = state.Hardware.Gpus.Sum(g => g.Power.Package.Value ?? 0);
        state.Hardware.TotalPower.Value = cpuPower + gpuPower;
        state.Hardware.TotalPower.Unit  = "W";
    }

    // ── CPU ──────────────────────────────────────────────────────────
    private static void TranslateCpu(IHardware hw, CpuNode node)
    {
        node.Name = hw.Name;

        foreach (var sensor in hw.Sensors)
        {
            switch (sensor.SensorType)
            {
                case SensorType.Load:
                    switch (sensor.Name)
                    {
                        case "CPU Total":
                            Fill(node.Load.Total, sensor, "%"); break;
                        case "CPU Core Max":
                            Fill(node.Load.CoreMax, sensor, "%"); break;
                        default:
                            if (sensor.Name.Contains("Core"))
                                Fill(node.Load.Cores.GetOrCreate(sensor.Index), sensor, "%");
                            break;
                    }
                    break;

                case SensorType.Temperature:
                    switch (sensor.Name)
                    {
                        case "Core (Tctl/Tdie)":
                        case "CPU Package":
                            Fill(node.Temperature.Primary, sensor, "°C"); break;
                        case "CCD1 (Tdie)":
                        case "Core Average":
                            Fill(node.Temperature.Secondary, sensor, "°C"); break;
                    }
                    break;

                case SensorType.Power:
                    switch (sensor.Name)
                    {
                        case "CPU Package":
                        case "Package":
                            Fill(node.Power.Package, sensor, "W"); break;
                            //case "CPU Memory":
                            //  Fill(node.Power.Memory, sensor, "W"); break;
                    }
                    break;

                case SensorType.Clock:
                    switch (sensor.Name)
                    {
                        case "Bus Speed":
                            Fill(node.Clock.BusSpeed, sensor, "MHz"); break;
                        case "CPU Cores":
                        case "Core Average":
                        case "Cores (Average)":
                        case "Cores (Average Effective)":
                            Fill(node.Clock.CoresAverage, sensor, "MHz");
                            break;
                        default:
                            if (sensor.Name.StartsWith("Core #"))
                                Fill(node.Clock.Cores.GetOrCreate(sensor.Index), sensor, "MHz");
                            break;
                    }
                    break;
            }
        }
    }

    // ── Memory ───────────────────────────────────────────────────────
    private static void TranslateMemory(IHardware hw, MemoryNode node)
    {
        switch (hw.Name)
        {
            case "Total Memory":
                foreach (var sensor in hw.Sensors)
                {
                    switch (sensor.Name)
                    {
                        case "Memory": Fill(node.Load.Total, sensor, "%"); break;
                        case "Memory Used": Fill(node.Data.Used, sensor, "GB"); break;
                        case "Memory Available": Fill(node.Data.Available, sensor, "GB"); break;
                    }
                }
                break;

            case "Virtual Memory":
                foreach (var sensor in hw.Sensors)
                {
                    switch (sensor.Name)
                    {
                        case "Virtual Memory": Fill(node.Virtual.Load, sensor, "%"); break;
                        case "Virtual Memory Used": Fill(node.Virtual.Used, sensor, "GB"); break;
                        case "Virtual Memory Available": Fill(node.Virtual.Available, sensor, "GB"); break;
                    }
                }
                break;

            default:
                if (!hw.Identifier.ToString().Contains("dimm")) break;

                var temps = hw.Sensors
                    .Where(s => s.SensorType == SensorType.Temperature)
                    .OrderBy(s => s.Index)
                    .ToList();

                if (temps.Count == 0) break;

                var index = int.Parse(
                    hw.Identifier.ToString()
                        .Split('/')
                        .Last(s => int.TryParse(s, out _)));

                var dimm = node.GetOrCreateDimm(index - 1);
                dimm.Name = hw.Name;

                if (temps.Count > 0) Fill(dimm.Temperature, temps[0], "°C");
                if (temps.Count > 1) Fill(dimm.WarningTemperature, temps[1], "°C");
                if (temps.Count > 2) Fill(dimm.CriticalTemperature, temps[2], "°C");
                break;
        }
    }

    // ── Motherboard ──────────────────────────────────────────────────
    private static void TranslateMotherboard(IHardware hw, MotherboardNode node)
    {
        node.Name = hw.Name;

        foreach (var sub in hw.SubHardware)
        {
            node.ChipsetName = sub.Name;
            PopulateMotherboardSensors(sub.Sensors, node);
        }

        // sensores directos (raramente existem mas prevenimos)
        PopulateMotherboardSensors(hw.Sensors, node);

        // pump: qualquer header com "PUMP" ou "AIO" (CPU_PUMP, W_PUMP+, AIO_PUMP, ...)
        node.CpuPump = node.Fan.FirstOrDefault(s =>
            s.Name?.Contains("PUMP", StringComparison.OrdinalIgnoreCase) == true ||
            s.Name?.Contains("AIO",  StringComparison.OrdinalIgnoreCase) == true);

        // CPU fans: headers com "CPU" excluindo o pump
        var cpuFans = node.Fan
            .Where(s => s.Name?.Contains("CPU", StringComparison.OrdinalIgnoreCase) == true
                     && s != node.CpuPump)
            .ToList();

        node.CpuFan = cpuFans.FirstOrDefault(s =>
            s.Name?.Contains("Optional", StringComparison.OrdinalIgnoreCase) != true)
            ?? cpuFans.FirstOrDefault();

        node.CpuFanSecondary = cpuFans.FirstOrDefault(s =>
            s.Name?.Contains("Optional", StringComparison.OrdinalIgnoreCase) == true)
            ?? cpuFans.Skip(1).FirstOrDefault();
    }

    private static void PopulateMotherboardSensors(
        IEnumerable<ISensor> sensors,
        MotherboardNode node)
    {
        foreach (var sensor in sensors)
        {
            switch (sensor.SensorType)
            {
                case SensorType.Temperature:
                    Fill(node.Temperature.GetOrCreate(sensor.Name), sensor, "°C"); break;
                case SensorType.Voltage:
                    Fill(node.Voltage.GetOrCreate(sensor.Name), sensor, "V"); break;
                case SensorType.Fan:
                    Fill(node.Fan.GetOrCreate(sensor.Name), sensor, "RPM"); break;
                case SensorType.Control:
                    Fill(node.Control.GetOrCreate(sensor.Name), sensor, "%"); break;
            }
        }
    }

    // ── GPU ──────────────────────────────────────────────────────────
    private static void TranslateGpu(IHardware hw, GpuNode node)
    {
        node.Name = hw.Name;

        foreach (var sensor in hw.Sensors)
        {
            switch (sensor.SensorType)
            {
                case SensorType.Load:
                    switch (sensor.Name)
                    {
                        case "GPU Core": Fill(node.Load.Core, sensor, "%"); break;
                        case "GPU Memory": Fill(node.Load.Memory, sensor, "%"); break;
                        case "GPU D3D": Fill(node.Load.D3D, sensor, "%"); break;
                    }
                    break;

                case SensorType.Temperature:
                    if (node.Temperature.Primary.Value is null)
                        Fill(node.Temperature.Primary, sensor, "°C");
                    else
                        Fill(node.Temperature.Secondary, sensor, "°C");
                    break;

                case SensorType.Clock:
                    switch (sensor.Name)
                    {
                        case "GPU Core": Fill(node.Clock.Core, sensor, "MHz"); break;
                        case "GPU Memory": Fill(node.Clock.Memory, sensor, "MHz"); break;
                        case "GPU SoC": Fill(node.Clock.Soc, sensor, "MHz"); break;
                    }
                    break;

                case SensorType.Power:
                    switch (sensor.Name)
                    {
                        case "GPU Package":
                        case "GPU Power":
                            Fill(node.Power.Package, sensor, "W"); break;
                        case "GPU Core":
                            Fill(node.Power.Core, sensor, "W"); break;
                        case "GPU SoC":
                            Fill(node.Power.Soc, sensor, "W"); break;
                    }
                    break;

                case SensorType.Fan:
                    if (node.Fan.Primary.Value is null)
                        Fill(node.Fan.Primary, sensor, "RPM");
                    else
                        Fill(node.Fan.Secondary, sensor, "RPM");
                    break;

                case SensorType.SmallData:
                    switch (sensor.Name)
                    {
                        case "GPU Memory Used": Fill(node.Data.Used, sensor, "MB"); break;
                        case "GPU Memory Free": Fill(node.Data.Free, sensor, "MB"); break;
                        case "GPU Memory Total": Fill(node.Data.Total, sensor, "MB"); break;
                        case "GPU Memory Dedicated Used": Fill(node.Data.DedicatedUsed, sensor, "MB"); break;
                        case "GPU Memory Dedicated Free": Fill(node.Data.DedicatedFree, sensor, "MB"); break;
                        case "GPU Memory Shared Used": Fill(node.Data.SharedUsed, sensor, "MB"); break;
                        case "GPU Memory Shared Free": Fill(node.Data.SharedFree, sensor, "MB"); break;
                    }
                    break;
            }
        }
    }

    // ── Network ──────────────────────────────────────────────────────
    private static void TranslateNetwork(IHardware hw, NetworkNode node)
    {
        node.Name = hw.Name;

        foreach (var sensor in hw.Sensors)
        {
            switch (sensor.Name)
            {
                case "Upload Speed": Fill(node.Throughput.Upload, sensor, "B/s"); break;
                case "Download Speed": Fill(node.Throughput.Download, sensor, "B/s"); break;
                case "Data Uploaded": Fill(node.Data.Uploaded, sensor, "GB"); break;
                case "Data Downloaded": Fill(node.Data.Downloaded, sensor, "GB"); break;
            }
        }
    }

    // ── Storage ──────────────────────────────────────────────────────
    private static void TranslateStorage(IHardware hw, StorageNode node)
    {
        node.Name = hw.Name;

        foreach (var sensor in hw.Sensors)
        {
            switch (sensor.SensorType)
            {
                case SensorType.Temperature:
                    switch (sensor.Name)
                    {
                        case "Temperature":
                            Fill(node.Temperature.Primary, sensor, "°C"); break;
                        case "Temperature 1":
                            Fill(node.Temperature.Warning, sensor, "°C"); break;
                        case "Temperature 2":
                            Fill(node.Temperature.Critical, sensor, "°C"); break;
                        default:
                            if (node.Temperature.Primary.Value is null)
                                Fill(node.Temperature.Primary, sensor, "°C");
                            break;
                    }
                    break;

                case SensorType.Load:
                    switch (sensor.Name)
                    {
                        case "Used Space": Fill(node.Load.UsedSpace, sensor, "%"); break;
                        case "Read Rate": Fill(node.Load.Read, sensor, "%"); break;
                        case "Write Rate": Fill(node.Load.Write, sensor, "%"); break;
                        case "Total Activity": Fill(node.Load.Total, sensor, "%"); break;
                    }
                    break;

                case SensorType.Data:
                    switch (sensor.Name)
                    {
                        case "Data Read": Fill(node.Data.Read, sensor, "GB"); break;
                        case "Data Written": Fill(node.Data.Written, sensor, "GB"); break;
                    }
                    break;

                case SensorType.Level:
                    switch (sensor.Name)
                    {
                        case "Remaining Life":
                        case "Percentage Used":
                            Fill(node.Level.Life, sensor, "%"); break;
                        case "Available Spare":
                            Fill(node.Level.AvailableSpare, sensor, "%"); break;
                        case "Available Spare Threshold":
                            Fill(node.Level.AvailableSpareThreshold, sensor, "%"); break;
                    }
                    break;

                case SensorType.Throughput:
                    switch (sensor.Name)
                    {
                        case "Read Rate": Fill(node.Throughput.ReadRate, sensor, "MB/s"); break;
                        case "Write Rate": Fill(node.Throughput.WriteRate, sensor, "MB/s"); break;
                    }
                    break;

                case SensorType.Factor:
                    switch (sensor.Name)
                    {
                        case "Power On Hours": Fill(node.Factor.PowerOnHours, sensor, "h"); break;
                        case "Power On Count": Fill(node.Factor.PowerOnCount, sensor, ""); break;
                    }
                    break;
            }
        }
    }

    // ── Helpers ──────────────────────────────────────────────────────
    private static void Fill(SensorNode node, ISensor sensor, string unit)
    {
        node.Value = sensor.Value;
        node.Min = sensor.Min;
        node.Max = sensor.Max;
        node.Unit = unit;
        node.Name = sensor.Name;
        node.Identifier = sensor.Identifier.ToString();
    }

    private static T GetOrCreate<T>(List<T> list, int index) where T : new()
    {
        while (list.Count <= index)
            list.Add(new T());
        return list[index];
    }
}