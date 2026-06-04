using Aquila.Models;
using Aquila.Models.Nodes;
using System;

namespace Aquila.Services.Mock;

public class MockDriver : IHardwareDriver
{
    private readonly Random _rng = new();

    public string Name => "Mock";
    public string Version => "1.0";
    public bool RequiresElevation => false;
    public bool IsAvailable => true;

    public void Initialize() { }
    public void Shutdown() { }

    public void Populate(AquilaState state)
    {
        // CPU
        var cpu = GetOrAdd(state.Hardware.Cpus);
        cpu.Name = "AMD Ryzen 7 9800X3D";
        FillSensor(cpu.Load.Total, _rng.NextSingle() * 100, 0, 100, "%");
        FillSensor(cpu.Temperature.Primary, 65 + _rng.NextSingle() * 20, 30, 95, "°C");
        FillSensor(cpu.Power.Package, 85 + _rng.NextSingle() * 40, 0, 200, "W");
        FillSensor(cpu.Clock.CoresAverage, 4200 + _rng.NextSingle() * 600, 400, 5000, "MHz");

        // Memory
        FillSensor(state.Hardware.Memory.Load.Total, 67f, 0, 100, "%");
        FillSensor(state.Hardware.Memory.Data.Used, 14.9f, 0, 32, "GB");
        FillSensor(state.Hardware.Memory.Data.Available, 16.1f, 0, 32, "GB");

        var dimm = state.Hardware.Memory.GetOrCreateDimm(0);
        dimm.Name = "G Skill Intl - F5-6000J3038F16G (#1)";
        FillSensor(dimm.Temperature, 39f, 30, 60, "°C");
        FillSensor(dimm.WarningTemperature, 85f, 85, 85, "°C");
        FillSensor(dimm.CriticalTemperature, 95f, 95, 95, "°C");

        // Motherboard
        state.Hardware.Motherboard.Name = "MSI MAG X870 TOMAHAWK WIFI";
        state.Hardware.Motherboard.ChipsetName = "Nuvoton NCT6687D-R";
        FillSensor(state.Hardware.Motherboard.Temperature.GetOrCreate("CPU Core"), 47f, 30, 90, "°C");
        FillSensor(state.Hardware.Motherboard.Temperature.GetOrCreate("Chipset"), 46f, 30, 90, "°C");
        FillSensor(state.Hardware.Motherboard.Fan.GetOrCreate("CPU Fan"), 911f, 0, 2000, "RPM");
        FillSensor(state.Hardware.Motherboard.Fan.GetOrCreate("Pump Fan #1"), 4026f, 0, 5000, "RPM");
        FillSensor(state.Hardware.Motherboard.Voltage.GetOrCreate("Vcore"), 1.074f, 0, 2, "V");

        // GPU
        var gpu = GetOrAdd(state.Hardware.Gpus);
        gpu.Name = "AMD Radeon Graphics";
        FillSensor(gpu.Load.Core, _rng.NextSingle() * 100, 0, 100, "%");
        FillSensor(gpu.Temperature.Primary, 65 + _rng.NextSingle() * 20, 30, 110, "°C");
        FillSensor(gpu.Power.Package, 150 + _rng.NextSingle() * 50, 0, 300, "W");
        FillSensor(gpu.Data.Used, 4096f, 0, 16384, "MB");
        FillSensor(gpu.Fan.Primary, 1200 + _rng.NextSingle() * 800, 0, 3000, "RPM");

        // Network
        var net = GetOrAdd(state.Hardware.Networks);
        net.Name = "Ethernet";
        FillSensor(net.Throughput.Download, _rng.NextSingle() * 100, 0, 1000, "MB/s");
        FillSensor(net.Throughput.Upload, _rng.NextSingle() * 10, 0, 1000, "MB/s");

        // Storage
        var ssd = GetOrAdd(state.Hardware.Storages);
        ssd.Name = "Samsung 980 Pro 1TB";
        FillSensor(ssd.Temperature.Primary, 38f, 20, 70, "°C");
        FillSensor(ssd.Load.UsedSpace, 65f, 0, 100, "%");
        FillSensor(ssd.Level.Life, 98f, 0, 100, "%");
        FillSensor(ssd.Throughput.ReadRate, _rng.NextSingle() * 7000, 0, 7000, "MB/s");
        FillSensor(ssd.Throughput.WriteRate, _rng.NextSingle() * 5000, 0, 5000, "MB/s");
    }

    private static T GetOrAdd<T>(List<T> list) where T : new()
    {
        if (list.Count == 0)
            list.Add(new T());
        return list[0];
    }

    private static void FillSensor(SensorNode node, float? value, float? min, float? max, string unit)
    {
        node.Value = value;
        node.Min = min;
        node.Max = max;
        node.Unit = unit;
    }
}