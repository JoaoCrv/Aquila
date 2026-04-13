using Aquila.Models.Api;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Aquila.Helpers
{
    /// <summary>
    /// First practical semantic layer over the raw provider state.
    /// Rules are based on hardware type + sensor type + sensor name.
    /// No machine-specific identifier suffixes are used for semantic selection.
    /// </summary>
    public static class AquilaSensorSelector
    {
        public static SensorNode? FindCpuTotalLoad(CpuNode cpu) =>
            FindBest(cpu.Loads, prefer: ["Total", "CPU Total", "Core (Avg)", "Package"], avoid: ["Max"]);

        public static IEnumerable<SensorNode> FindCpuCoreLoads(CpuNode cpu)
        {
            var preferred = cpu.Loads
                .Where(load => load.Value.HasValue)
                .Where(load => ContainsAny(load.Name, "Core", "P-Core", "E-Core", "Thread"))
                .Where(load => !ContainsAny(load.Name, "Total", "Package", "Core (Avg)", "Max"))
                .OrderBy(load => load.Name)
                .ToList();

            if (preferred.Count > 0)
                return preferred;

            return cpu.Loads
                .Where(load => load.Value.HasValue)
                .Where(load => !ContainsAny(load.Name, "Total", "Package", "Core (Avg)", "Max"))
                .OrderBy(load => load.Name);
        }

        public static SensorNode? FindCpuTemperature(CpuNode cpu) =>
            FindBest(cpu.Temperatures, prefer: ["Package", "Tctl/Tdie", "Tdie", "CPU", "Core (Avg)"], avoid: ["Max"]);

        public static SensorNode? FindCpuEffectiveClock(CpuNode cpu) =>
            FindBest(cpu.Clocks, prefer: ["Effective", "Core (Avg)", "Average", "Core Clock", "Core"], avoid: ["Bus", "SoC", "Memory"]);

        public static SensorNode? FindCpuPower(CpuNode cpu) =>
            FindBest(cpu.Powers, prefer: ["Package", "Total", "CPU"], avoid: ["Limit"]);

        public static SensorNode? FindCpuPrimaryFan(MotherboardNode motherboard) =>
            FindBest(GetMotherboardFans(motherboard), prefer: ["CPU", "Processor", "Pump"], avoid: ["Chassis", "Case", "System"]);

        public static SensorNode? FindCpuSecondaryFan(MotherboardNode motherboard)
        {
            var fans = GetMotherboardFans(motherboard).ToList();
            var primary = FindCpuPrimaryFan(motherboard);
            return fans.FirstOrDefault(fan => !ReferenceEquals(fan, primary)) ?? fans.Skip(1).FirstOrDefault();
        }

        public static IEnumerable<SensorNode> GetMotherboardTemperatures(MotherboardNode motherboard) =>
            Rank(motherboard.Temperatures, prefer: ["CPU", "System", "VRM", "MOS", "Chipset", "PCH"]);

        public static IEnumerable<SensorNode> GetMotherboardFans(MotherboardNode motherboard)
        {
            var sensors = motherboard.Fans.Cast<SensorNode>()
                .Concat(motherboard.Controls.Where(control => ContainsAny(control.Name, "Fan", "Pump")));

            return Rank(sensors, prefer: ["CPU", "Pump", "System", "Chassis", "Case"]);
        }

        public static SensorNode? FindGpuLoad(GpuNode gpu) =>
            FindBest(gpu.Loads, prefer: ["Core", "D3D", "GPU"], avoid: ["Memory", "Bus"]);

        public static SensorNode? FindGpuTemperature(GpuNode gpu) =>
            FindBest(gpu.Temperatures, prefer: ["Core", "Edge", "Package"], avoid: ["Hot Spot", "Hotspot", "Junction"]);

        public static SensorNode? FindGpuHotspotTemperature(GpuNode gpu) =>
            FindBest(gpu.Temperatures, prefer: ["Hot Spot", "Hotspot", "Junction"]);

        public static SensorNode? FindGpuCoreClock(GpuNode gpu) =>
            FindBest(gpu.Clocks, prefer: ["Core", "Graphics"], avoid: ["Memory", "Shader", "Video"]);

        public static SensorNode? FindGpuPower(GpuNode gpu) =>
            FindBest(gpu.Powers, prefer: ["Total", "Board", "Package", "GPU"], avoid: ["Limit"]);

        public static SensorNode? FindGpuVramUsed(GpuNode gpu) =>
            FindBest(gpu.Data, prefer: ["Memory Used", "VRAM Used", "Memory Allocated"]);

        public static SensorNode? FindGpuVramTotal(GpuNode gpu) =>
            FindBest(gpu.Data, prefer: ["Memory Total", "VRAM Total"]);

        public static SensorNode? FindMemoryLoad(MemoryNode memory) =>
            FindBest(memory.Loads, prefer: ["Memory", "Used", "Utilization", "Load"], avoid: ["Virtual"]);

        public static SensorNode? FindMemoryUsed(MemoryNode memory) =>
            FindBest(memory.Data, prefer: ["Used", "In Use"], avoid: ["Virtual"]);

        public static SensorNode? FindMemoryAvailable(MemoryNode memory) =>
            FindBest(memory.Data, prefer: ["Available", "Free"], avoid: ["Virtual"]);

        public static SensorNode? FindMemoryTotal(MemoryNode memory)
        {
            var explicitTotal = FindBest(memory.Data, prefer: ["Total"]);
            if (explicitTotal != null)
                return explicitTotal;

            var used = FindMemoryUsed(memory);
            var available = FindMemoryAvailable(memory);
            if (used?.Value is float usedValue && available?.Value is float availableValue)
            {
                return new SensorNode("Total")
                {
                    Value = usedValue + availableValue,
                    Unit = string.IsNullOrWhiteSpace(used.Unit) ? "GB" : used.Unit,
                };
            }

            return null;
        }

        public static SensorNode? FindMemoryCache(MemoryNode memory) =>
            FindBest(memory.Data, prefer: ["Cache", "Cached", "Standby"]);

        public static SensorNode? FindMemoryPageReads(MemoryNode memory) =>
            FindBest(memory.Data.Concat(memory.Throughput), prefer: ["Page Reads", "Reads"], avoid: ["Writes"]);

        public static SensorNode? FindMemoryPageWrites(MemoryNode memory) =>
            FindBest(memory.Data.Concat(memory.Throughput), prefer: ["Page Writes", "Writes"], avoid: ["Reads"]);

        public static SensorNode? FindMemoryPower(MemoryNode memory) =>
            FindBest(memory.Powers, prefer: ["Total", "DRAM", "Memory"]);

        public static SensorNode? FindNetworkDownload(NetworkNode network) =>
            FindBest(network.Throughput, prefer: ["Download", "Received", "Rx"], avoid: ["Upload", "Sent", "Tx"]);

        public static SensorNode? FindNetworkUpload(NetworkNode network) =>
            FindBest(network.Throughput, prefer: ["Upload", "Sent", "Tx"], avoid: ["Download", "Received", "Rx"]);

        public static SensorNode? FindNetworkDataDownloaded(NetworkNode network) =>
            FindBest(network.Data, prefer: ["Downloaded", "Received", "Download"], avoid: ["Uploaded", "Sent", "Upload"]);

        public static SensorNode? FindNetworkDataUploaded(NetworkNode network) =>
            FindBest(network.Data, prefer: ["Uploaded", "Sent", "Upload"], avoid: ["Downloaded", "Received", "Download"]);

        public static SensorNode? FindStorageTemperature(StorageNode drive) =>
            FindBest(drive.Temperatures, prefer: ["Composite", "Temperature"], avoid: ["Air Flow"]);

        public static SensorNode? FindStorageReadRate(StorageNode drive) =>
            FindBest(drive.Throughput, prefer: ["Read"], avoid: ["Write"]);

        public static SensorNode? FindStorageWriteRate(StorageNode drive) =>
            FindBest(drive.Throughput, prefer: ["Write"], avoid: ["Read"]);

        public static SensorNode? FindStorageUsedSpace(StorageNode drive) =>
            FindBest(drive.Loads, prefer: ["Used", "Space"], avoid: ["Free"]);

        public static SensorNode? FindStorageDataRead(StorageNode drive) =>
            FindBest(drive.Data, prefer: ["Data Read", "Read"], avoid: ["Written", "Write"]);

        public static SensorNode? FindStorageDataWritten(StorageNode drive) =>
            FindBest(drive.Data, prefer: ["Data Written", "Written", "Write"], avoid: ["Read"]);

        private static T? FindBest<T>(IEnumerable<T> sensors, string[] prefer, string[]? avoid = null)
            where T : SensorNode
        {
            var ranked = Rank(sensors, prefer, avoid).ToList();
            return ranked.FirstOrDefault(sensor => sensor.Value.HasValue)
                ?? ranked.FirstOrDefault();
        }

        private static IEnumerable<T> Rank<T>(IEnumerable<T> sensors, string[] prefer, string[]? avoid = null)
            where T : SensorNode
        {
            var materialized = sensors?.ToList() ?? [];
            return materialized
                .OrderByDescending(sensor => GetScore(sensor.Name, prefer, avoid))
                .ThenBy(sensor => sensor.Name);
        }

        private static bool ContainsAny(string? value, params string[] terms)
        {
            if (string.IsNullOrWhiteSpace(value))
                return false;

            return terms.Any(term => value.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private static int GetScore(string? sensorName, IEnumerable<string> preferredTerms, IEnumerable<string>? avoidTerms = null)
        {
            if (string.IsNullOrWhiteSpace(sensorName))
                return 0;

            var score = 0;
            foreach (var term in preferredTerms)
            {
                if (sensorName.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0)
                    score += Math.Max(1, term.Length);
            }

            if (avoidTerms != null)
            {
                foreach (var term in avoidTerms)
                {
                    if (sensorName.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0)
                        score -= Math.Max(1, term.Length);
                }
            }

            return score;
        }
    }
}
