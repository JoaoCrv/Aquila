using Aquila.Models.Api;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Aquila.Services.Semantics
{
    public sealed class SensorSelectionResult<T>(T? sensor, SemanticResolutionState state, int candidateCount, string reason)
        where T : SensorNode
    {
        public T? Sensor { get; } = sensor;
        public SemanticResolutionState State { get; } = state;
        public int CandidateCount { get; } = candidateCount;
        public string Reason { get; } = reason;
    }

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

        public static SensorSelectionResult<FanNode> ResolveCpuFanRpm(MotherboardNode motherboard) =>
            ResolveBest(motherboard.Fans, prefer: ["CPU", "Processor", "Pump"], avoid: ["Chassis", "Case", "System"], missingReason: "No CPU or pump RPM sensor exposed by the motherboard.");

        public static SensorSelectionResult<SensorNode> ResolveCpuFanControl(MotherboardNode motherboard) =>
            ResolveBest(motherboard.Controls.Where(c => ContainsAny(c.Name, "Fan", "Pump")),
                        prefer: ["CPU", "Processor", "Pump"], avoid: ["Chassis", "Case", "System"], missingReason: "No CPU or pump duty-cycle control sensor exposed by the motherboard.");

        public static SensorSelectionResult<FanNode> ResolveCpuSecondaryFanRpm(MotherboardNode motherboard)
        {
            var fans = GetMotherboardFans(motherboard).ToList();
            var cpuFan = ResolveCpuFanRpm(motherboard).Sensor;

            var candidates = fans
                .Where(fan => !ReferenceEquals(fan, cpuFan))
                .ToList();

            if (candidates.Count == 0)
            {
                return new SensorSelectionResult<FanNode>(
                    null,
                    SemanticResolutionState.Missing,
                    0,
                    "No secondary CPU fan or pump RPM sensor is available.");
            }

            var pumpCandidates = candidates
                .Where(fan => ContainsAny(fan.Name, "Pump", "AIO", "Water", "Liquid"))
                .ToList();

            var pumpFan = pumpCandidates.FirstOrDefault();

            if (pumpFan is not null)
            {
                return new SensorSelectionResult<FanNode>(
                    pumpFan,
                    pumpCandidates.Skip(1).Any() ? SemanticResolutionState.Ambiguous : SemanticResolutionState.Matched,
                    pumpCandidates.Count,
                    pumpCandidates.Count > 1
                        ? "Multiple pump-like RPM sensors matched; using the highest-ranked candidate."
                        : "Resolved secondary CPU cooling sensor as pump RPM.");
            }

            return new SensorSelectionResult<FanNode>(
                candidates[0],
                candidates.Count > 1 ? SemanticResolutionState.Ambiguous : SemanticResolutionState.Matched,
                candidates.Count,
                candidates.Count > 1
                    ? "Multiple secondary RPM candidates matched; using the highest-ranked fan after the CPU primary fan."
                    : "Resolved secondary CPU cooling sensor as the remaining motherboard RPM fan.");
        }

        public static IEnumerable<SensorNode> GetMotherboardTemperatures(MotherboardNode motherboard) =>
            Rank(motherboard.Temperatures, prefer: ["CPU", "System", "VRM", "MOS", "Chipset", "PCH"]);

        public static IEnumerable<FanNode> GetMotherboardFans(MotherboardNode motherboard) =>
            Rank(motherboard.Fans, prefer: ["CPU", "Pump", "System", "Chassis", "Case"]);

        public static SensorNode? FindMotherboardFanControl(MotherboardNode motherboard, FanNode fan)
        {
            var controls = motherboard.Controls
                .Where(control => ContainsAny(control.Name, "Fan", "Pump", "AIO", "Water", "Liquid"))
                .ToList();

            if (controls.Count == 0)
                return null;

            var fanName = fan.Name ?? string.Empty;

            var preferred = new List<string> { fanName };
            if (ContainsAny(fanName, "CPU", "Processor"))
                preferred.AddRange(["CPU", "Processor"]);
            if (ContainsAny(fanName, "Pump", "AIO", "Water", "Liquid"))
                preferred.AddRange(["Pump", "AIO", "Water", "Liquid"]);
            if (ContainsAny(fanName, "Chassis", "Case", "System", "VRM"))
                preferred.AddRange(["Chassis", "Case", "System", "VRM"]);

            return FindBest(controls, prefer: preferred.ToArray());
        }

        public static SensorNode? FindGpuLoad(GpuNode gpu) =>
            FindBest(gpu.Loads, prefer: ["Core", "D3D", "GPU"], avoid: ["Memory", "Bus"]);

        public static SensorNode? FindGpuTemperature(GpuNode gpu) =>
            FindBest(gpu.Temperatures, prefer: ["Core", "Edge", "Package"], avoid: ["Hot Spot", "Hotspot", "Junction"]);

        public static SensorNode? FindGpuHotspotTemperature(GpuNode gpu) =>
            FindBest(gpu.Temperatures, prefer: ["Hot Spot", "Hotspot", "Junction"]);

        public static SensorNode? FindGpuCoreClock(GpuNode gpu) =>
            FindBest(gpu.Clocks, prefer: ["Core", "Graphics"], avoid: ["Memory", "Shader", "Video"]);

        public static SensorSelectionResult<SensorNode> ResolveGpuPower(GpuNode gpu) =>
            ResolveBest(gpu.Powers, prefer: ["Total", "Board", "Package", "GPU"], avoid: ["Limit"], missingReason: "No GPU package power sensor exposed by this adapter.");

        public static SensorNode? FindGpuPower(GpuNode gpu) =>
            FindBest(gpu.Powers, prefer: ["Total", "Board", "Package", "GPU"], avoid: ["Limit"]);

        public static SensorSelectionResult<FanNode> ResolveGpuPrimaryFan(GpuNode gpu)
        {
            var ranked = Rank(gpu.Fans, prefer: ["GPU", "Core", "Fan 1", "Left", "Primary"]).ToList();
            if (ranked.Count == 0)
            {
                return new SensorSelectionResult<FanNode>(null, SemanticResolutionState.Missing, 0,
                    "No GPU fan RPM sensor exposed by this adapter.");
            }

            return new SensorSelectionResult<FanNode>(
                ranked[0],
                ranked.Count > 1 ? SemanticResolutionState.Ambiguous : SemanticResolutionState.Matched,
                ranked.Count,
                ranked.Count > 1
                    ? "Multiple GPU fan RPM sensors matched; using the highest-ranked primary fan."
                    : "Resolved GPU primary fan RPM sensor.");
        }

        public static SensorSelectionResult<FanNode> ResolveGpuSecondaryFan(GpuNode gpu)
        {
            var ranked = Rank(gpu.Fans, prefer: ["GPU", "Core", "Fan 2", "Right", "Secondary"]).ToList();
            var primary = ResolveGpuPrimaryFan(gpu).Sensor;
            var candidates = ranked.Where(fan => !ReferenceEquals(fan, primary)).ToList();

            if (candidates.Count == 0)
            {
                return new SensorSelectionResult<FanNode>(null, SemanticResolutionState.Missing, 0,
                    "No secondary GPU fan RPM sensor is available.");
            }

            return new SensorSelectionResult<FanNode>(
                candidates[0],
                candidates.Count > 1 ? SemanticResolutionState.Ambiguous : SemanticResolutionState.Matched,
                candidates.Count,
                candidates.Count > 1
                    ? "Multiple secondary GPU fan RPM sensors matched; using the highest-ranked remaining fan."
                    : "Resolved GPU secondary fan RPM sensor.");
        }

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
            return FindBest(memory.Data, prefer: ["Total"]);
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

        private static SensorSelectionResult<T> ResolveBest<T>(IEnumerable<T> sensors, string[] prefer, string[]? avoid = null, string? missingReason = null)
            where T : SensorNode
        {
            var ranked = Rank(sensors, prefer, avoid).ToList();

            if (ranked.Count == 0)
            {
                return new SensorSelectionResult<T>(
                    null,
                    SemanticResolutionState.Missing,
                    0,
                    missingReason ?? "No candidate sensor matched this semantic slot.");
            }

            var selected = ranked.FirstOrDefault(sensor => sensor.Value.HasValue)
                ?? ranked[0];

            var selectedScore = GetScore(selected.Name, prefer, avoid);
            var competingCandidates = ranked.Count(sensor => GetScore(sensor.Name, prefer, avoid) == selectedScore);
            var state = competingCandidates > 1 ? SemanticResolutionState.Ambiguous : SemanticResolutionState.Matched;
            var reason = state == SemanticResolutionState.Ambiguous
                ? "Multiple candidate sensors tied for this semantic slot; using the highest-ranked match."
                : "Semantic slot resolved successfully.";

            return new SensorSelectionResult<T>(selected, state, ranked.Count, reason);
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