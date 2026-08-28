using System.Collections.Generic;
using System.Linq;
using Aquila.Models.Nodes;

namespace Aquila.Models;

/// <summary>One sensor with a human-readable label, for listing/lookup (Explorer, widgets).</summary>
public sealed record SensorEntry(string Label, SensorNode Sensor, MetricKind Metric)
{
    /// <summary>
    /// Which family of limits judges this reading — the same names the settings form and the colour
    /// converter use.
    ///
    /// Worked out here because this is the only place that still knows what KIND of part a sensor came
    /// from: by the time a widget holds a SensorNode it has a name, a unit and a value, and no way to tell
    /// 62 °C on an NVMe from 62 °C on a die. Anything downstream would be guessing from the label.
    ///
    /// The pair, not one collapsed name: 62 °C on a die and 62 °C on an NVMe differ only in where they came
    /// from, and a single "Temperature" family could never let a GPU at 83 °C be ordinary while a CPU at 83 °C
    /// is warm.
    /// </summary>
    public MetricKey Key { get; init; }

    /// <summary>Whether this reading can be judged at all. Watts, clocks, volts and bytes per second have no
    /// universal "in trouble" number, so they are honestly unjudgeable rather than quietly measured against
    /// the percentage steps.</summary>
    public bool IsJudgeable => Thresholds.Preset(Key) is not null;
}

/// <summary>A hardware component and its live sensors.</summary>
public sealed record SensorComponent(string Name, IReadOnlyList<SensorEntry> Sensors);

/// <summary>
/// Flattens the typed <see cref="HardwareNode"/> tree into a list of components, each with its live
/// <see cref="SensorNode"/>s. References the same live nodes (not copies), so values stay current.
/// Used by the Explorer and, later, by widgets (resolve a sensor by its <c>Identifier</c>).
/// </summary>
public static class SensorCatalog
{
    public static IReadOnlyList<SensorComponent> GetComponents(HardwareNode hw)
    {
        var components = new List<SensorComponent>();

        for (int i = 0; i < hw.Cpus.Count; i++)
            Add(components, hw.Cpus[i].Name ?? $"CPU {i + 1}", HardwareKind.Cpu, CpuSensors(hw.Cpus[i]));

        Add(components, "Memory", HardwareKind.Memory, MemorySensors(hw.Memory));

        for (int i = 0; i < hw.Gpus.Count; i++)
            Add(components, hw.Gpus[i].Name ?? $"GPU {i + 1}", HardwareKind.Gpu, GpuSensors(hw.Gpus[i]));

        Add(components, hw.Motherboard.Name ?? "Motherboard", HardwareKind.Motherboard, MotherboardSensors(hw.Motherboard));

        for (int i = 0; i < hw.Networks.Count; i++)
            Add(components, hw.Networks[i].Name ?? $"Network {i + 1}", HardwareKind.Network, NetworkSensors(hw.Networks[i]));

        for (int i = 0; i < hw.Storages.Count; i++)
            Add(components, hw.Storages[i].Name ?? $"Storage {i + 1}", HardwareKind.Storage, StorageSensors(hw.Storages[i]));

        Add(components, "System", HardwareKind.System, [new("Total Power", hw.TotalPower, MetricKind.Power)]);

        return components;
    }

    /// <summary>Resolves a live sensor by its Identifier, or null. Used to bind widgets to a sensor.</summary>
    public static SensorNode? FindByIdentifier(HardwareNode hw, string identifier) =>
        FindEntry(hw, identifier)?.Sensor;

    /// <summary>The whole entry, so a caller can have the sensor's family as well as the sensor. A widget
    /// saved before families existed has none stored, and a temperature judged on the percentage scale
    /// would sit at Normal all the way to boiling.</summary>
    public static SensorEntry? FindEntry(HardwareNode hw, string identifier) =>
        GetComponents(hw)
            .SelectMany(c => c.Sensors)
            .FirstOrDefault(e => e.Sensor.Identifier == identifier);

    // Only adds entries whose sensor has a value (skips unpopulated nodes), and drops empty components.
    //
    // The hardware half of the key is stamped here and the metric half comes from each builder, because
    // this is the one place that has both: the loop above knows it is walking CPUs, and the builder knows
    // that c.Temperature.Primary is a temperature. Neither knows alone, and the leaf knows neither.
    private static void Add(List<SensorComponent> components, string name, HardwareKind hardware,
        IEnumerable<SensorEntry> entries)
    {
        var live = entries
            .Where(e => e.Sensor.Value.HasValue)
            .Select(e => e with { Key = new MetricKey(hardware, e.Metric) })
            .ToList();

        if (live.Count > 0)
            components.Add(new SensorComponent(name, live));
    }

    private static IEnumerable<SensorEntry> CpuSensors(CpuNode c)
    {
        yield return new("Load", c.Load.Total, MetricKind.Load);
        yield return new("Core Max Load", c.Load.CoreMax, MetricKind.Load);
        for (int i = 0; i < c.Load.Cores.Count; i++)
            if (c.Load.Cores[i] is { } core) yield return new($"Core #{i + 1} Load", core, MetricKind.Load);
        yield return new("Temperature", c.Temperature.Primary, MetricKind.Temperature);
        yield return new("Temperature (Secondary)", c.Temperature.Secondary, MetricKind.Temperature);
        yield return new("Package Power", c.Power.Package, MetricKind.Power);
        yield return new("Clock (Average)", c.Clock.CoresAverage, MetricKind.Clock);
        yield return new("Bus Speed", c.Clock.BusSpeed, MetricKind.Clock);
    }

    private static IEnumerable<SensorEntry> MemorySensors(MemoryNode m)
    {
        yield return new("Load", m.Load.Total, MetricKind.Load);
        yield return new("Used", m.Data.Used, MetricKind.Data);
        yield return new("Available", m.Data.Available, MetricKind.Data);
        yield return new("Total", m.Data.Total, MetricKind.Data);
        yield return new("Virtual Load", m.Virtual.Load, MetricKind.Load);
        yield return new("Virtual Used", m.Virtual.Used, MetricKind.Data);
        yield return new("Virtual Available", m.Virtual.Available, MetricKind.Data);
        foreach (var d in m.Dimms)
        {
            var label = d.Name ?? "DIMM";
            yield return new($"{label} Temperature", d.Temperature, MetricKind.Temperature);
        }
    }

    private static IEnumerable<SensorEntry> GpuSensors(GpuNode g)
    {
        yield return new("Core Load", g.Load.Core, MetricKind.Load);
        yield return new("Memory Load", g.Load.Memory, MetricKind.Load);
        yield return new("Temperature", g.Temperature.Primary, MetricKind.Temperature);
        yield return new("Hot Spot", g.Temperature.Secondary, MetricKind.Temperature);
        yield return new("Core Clock", g.Clock.Core, MetricKind.Clock);
        yield return new("Memory Clock", g.Clock.Memory, MetricKind.Clock);
        yield return new("Power", g.Power.Package, MetricKind.Power);
        yield return new("VRAM Used", g.Data.Used, MetricKind.Data);
        yield return new("VRAM Total", g.Data.Total, MetricKind.Data);
        yield return new("Fan", g.Fan.Primary, MetricKind.Fan);
        yield return new("Fan (Secondary)", g.Fan.Secondary, MetricKind.Fan);
    }

    private static IEnumerable<SensorEntry> MotherboardSensors(MotherboardNode mb)
    {
        foreach (var s in mb.Temperature) yield return new($"{s.Name} Temp", s, MetricKind.Temperature);
        foreach (var s in mb.Fan)         yield return new($"{s.Name}", s, MetricKind.Fan);
        foreach (var s in mb.Control)     yield return new($"{s.Name} Duty", s, MetricKind.Duty);
        foreach (var s in mb.Voltage)     yield return new($"{s.Name} Voltage", s, MetricKind.Voltage);
    }

    private static IEnumerable<SensorEntry> NetworkSensors(NetworkNode n)
    {
        yield return new("Download", n.Throughput.Download, MetricKind.Throughput);
        yield return new("Upload", n.Throughput.Upload, MetricKind.Throughput);
        yield return new("Downloaded", n.Data.Downloaded, MetricKind.Data);
        yield return new("Uploaded", n.Data.Uploaded, MetricKind.Data);
    }

    private static IEnumerable<SensorEntry> StorageSensors(StorageNode s)
    {
        yield return new("Used Space", s.Load.UsedSpace, MetricKind.Load);
        yield return new("Temperature", s.Temperature.Primary, MetricKind.Temperature);
        yield return new("Read Rate", s.Throughput.ReadRate, MetricKind.Throughput);
        yield return new("Write Rate", s.Throughput.WriteRate, MetricKind.Throughput);
        yield return new("Life", s.Level.Life, MetricKind.Level);
        yield return new("Data Read", s.Data.Read, MetricKind.Data);
        yield return new("Data Written", s.Data.Written, MetricKind.Data);
    }
}
