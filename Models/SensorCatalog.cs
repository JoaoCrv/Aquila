using System.Collections.Generic;
using System.Linq;
using Aquila.Models.Nodes;

namespace Aquila.Models;

/// <summary>One sensor with a human-readable label, for listing/lookup (Explorer, widgets).</summary>
public sealed record SensorEntry(string Label, SensorNode Sensor);

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
            Add(components, hw.Cpus[i].Name ?? $"CPU {i + 1}", CpuSensors(hw.Cpus[i]));

        Add(components, "Memory", MemorySensors(hw.Memory));

        for (int i = 0; i < hw.Gpus.Count; i++)
            Add(components, hw.Gpus[i].Name ?? $"GPU {i + 1}", GpuSensors(hw.Gpus[i]));

        Add(components, hw.Motherboard.Name ?? "Motherboard", MotherboardSensors(hw.Motherboard));

        for (int i = 0; i < hw.Networks.Count; i++)
            Add(components, hw.Networks[i].Name ?? $"Network {i + 1}", NetworkSensors(hw.Networks[i]));

        for (int i = 0; i < hw.Storages.Count; i++)
            Add(components, hw.Storages[i].Name ?? $"Storage {i + 1}", StorageSensors(hw.Storages[i]));

        Add(components, "System", [new("Total Power", hw.TotalPower)]);

        return components;
    }

    /// <summary>Resolves a live sensor by its Identifier, or null. Used to bind widgets to a sensor.</summary>
    public static SensorNode? FindByIdentifier(HardwareNode hw, string identifier) =>
        GetComponents(hw)
            .SelectMany(c => c.Sensors)
            .FirstOrDefault(e => e.Sensor.Identifier == identifier)?.Sensor;

    // Only adds entries whose sensor has a value (skips unpopulated nodes), and drops empty components.
    private static void Add(List<SensorComponent> components, string name, IEnumerable<SensorEntry> entries)
    {
        var live = entries.Where(e => e.Sensor.Value.HasValue).ToList();
        if (live.Count > 0)
            components.Add(new SensorComponent(name, live));
    }

    private static IEnumerable<SensorEntry> CpuSensors(CpuNode c)
    {
        yield return new("Load", c.Load.Total);
        yield return new("Core Max Load", c.Load.CoreMax);
        for (int i = 0; i < c.Load.Cores.Count; i++)
            if (c.Load.Cores[i] is { } core) yield return new($"Core #{i + 1} Load", core);
        yield return new("Temperature", c.Temperature.Primary);
        yield return new("Temperature (Secondary)", c.Temperature.Secondary);
        yield return new("Package Power", c.Power.Package);
        yield return new("Clock (Average)", c.Clock.CoresAverage);
        yield return new("Bus Speed", c.Clock.BusSpeed);
    }

    private static IEnumerable<SensorEntry> MemorySensors(MemoryNode m)
    {
        yield return new("Load", m.Load.Total);
        yield return new("Used", m.Data.Used);
        yield return new("Available", m.Data.Available);
        yield return new("Total", m.Data.Total);
        yield return new("Virtual Load", m.Virtual.Load);
        yield return new("Virtual Used", m.Virtual.Used);
        yield return new("Virtual Available", m.Virtual.Available);
        foreach (var d in m.Dimms)
        {
            var label = d.Name ?? "DIMM";
            yield return new($"{label} Temperature", d.Temperature);
        }
    }

    private static IEnumerable<SensorEntry> GpuSensors(GpuNode g)
    {
        yield return new("Core Load", g.Load.Core);
        yield return new("Memory Load", g.Load.Memory);
        yield return new("Temperature", g.Temperature.Primary);
        yield return new("Hot Spot", g.Temperature.Secondary);
        yield return new("Core Clock", g.Clock.Core);
        yield return new("Memory Clock", g.Clock.Memory);
        yield return new("Power", g.Power.Package);
        yield return new("VRAM Used", g.Data.Used);
        yield return new("VRAM Total", g.Data.Total);
        yield return new("Fan", g.Fan.Primary);
        yield return new("Fan (Secondary)", g.Fan.Secondary);
    }

    private static IEnumerable<SensorEntry> MotherboardSensors(MotherboardNode mb)
    {
        foreach (var s in mb.Temperature) yield return new($"{s.Name} Temp", s);
        foreach (var s in mb.Fan)         yield return new($"{s.Name}", s);
        foreach (var s in mb.Voltage)     yield return new($"{s.Name} Voltage", s);
    }

    private static IEnumerable<SensorEntry> NetworkSensors(NetworkNode n)
    {
        yield return new("Download", n.Throughput.Download);
        yield return new("Upload", n.Throughput.Upload);
        yield return new("Downloaded", n.Data.Downloaded);
        yield return new("Uploaded", n.Data.Uploaded);
    }

    private static IEnumerable<SensorEntry> StorageSensors(StorageNode s)
    {
        yield return new("Used Space", s.Load.UsedSpace);
        yield return new("Temperature", s.Temperature.Primary);
        yield return new("Read Rate", s.Throughput.ReadRate);
        yield return new("Write Rate", s.Throughput.WriteRate);
        yield return new("Life", s.Level.Life);
        yield return new("Data Read", s.Data.Read);
        yield return new("Data Written", s.Data.Written);
    }
}
