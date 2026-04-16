using Aquila.Models.Api;
using LibreHardwareMonitor.Hardware;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Aquila.Services.Translators
{
    /// <summary>
    /// Result of a sensor resolution with diagnostic metadata.
    /// </summary>
    public readonly struct SensorMatch
    {
        public ISensor? Sensor { get; init; }
        public SemanticResolutionState State { get; init; }
        public int CandidateCount { get; init; }
        public string Reason { get; init; }

        public static SensorMatch Missing(string reason) =>
            new() { State = SemanticResolutionState.Missing, CandidateCount = 0, Reason = reason };
    }

    /// <summary>
    /// Result of a non-sensor selection (e.g. network adapter) with diagnostic metadata.
    /// </summary>
    public readonly struct SelectionMatch<T> where T : class
    {
        public T? Value { get; init; }
        public SemanticResolutionState State { get; init; }
        public int CandidateCount { get; init; }
        public string Reason { get; init; }

        public static SelectionMatch<T> Missing(string reason) =>
            new() { State = SemanticResolutionState.Missing, CandidateCount = 0, Reason = reason };
    }

    /// <summary>
    /// Sensor selection heuristics working directly with LHM's ISensor.
    /// No intermediate SensorNode/SensorBucket creation needed for selection.
    /// </summary>
    public static class LhmSensorSelector
    {
        #region Generic selection

        public static ISensor? FindBest(IEnumerable<ISensor> sensors, string[] prefer, string[]? avoid = null)
        {
            var ranked = Rank(sensors, prefer, avoid).ToList();
            return ranked.FirstOrDefault(s => s.Value.HasValue) ?? ranked.FirstOrDefault();
        }

        public static SensorMatch ResolveBest(IEnumerable<ISensor> sensors, string[] prefer, string[]? avoid = null, string? missingReason = null)
        {
            var ranked = Rank(sensors, prefer, avoid).ToList();

            if (ranked.Count == 0)
                return SensorMatch.Missing(missingReason ?? "No candidate sensor matched this semantic slot.");

            var selected = ranked.FirstOrDefault(s => s.Value.HasValue) ?? ranked[0];
            var selectedScore = GetScore(selected.Name, prefer, avoid);
            var competing = ranked.Count(s => GetScore(s.Name, prefer, avoid) == selectedScore);

            return new SensorMatch
            {
                Sensor = selected,
                State = competing > 1 ? SemanticResolutionState.Ambiguous : SemanticResolutionState.Matched,
                CandidateCount = ranked.Count,
                Reason = competing > 1
                    ? "Multiple candidates tied; using the highest-ranked match."
                    : "Resolved successfully."
            };
        }

        #endregion

        #region CPU

        public static IEnumerable<ISensor> FindCpuCoreLoads(IEnumerable<ISensor> loads)
        {
            var list = loads.ToList();
            var preferred = list
                .Where(l => l.Value.HasValue)
                .Where(l => ContainsAny(l.Name, "Core", "P-Core", "E-Core", "Thread"))
                .Where(l => !ContainsAny(l.Name, "Total", "Package", "Core (Avg)", "Max"))
                .OrderBy(l => l.Name)
                .ToList();

            if (preferred.Count > 0) return preferred;

            return list
                .Where(l => l.Value.HasValue)
                .Where(l => !ContainsAny(l.Name, "Total", "Package", "Core (Avg)", "Max"))
                .OrderBy(l => l.Name);
        }

        #endregion

        #region Motherboard fans & controls

        public static SensorMatch ResolveCpuFanRpm(IEnumerable<ISensor> fans) =>
            ResolveBest(fans,
                ["CPU", "Processor", "Pump"], ["Chassis", "Case", "System"],
                "No CPU or pump RPM sensor exposed by the motherboard.");

        public static SensorMatch ResolveCpuFanControl(IEnumerable<ISensor> controls) =>
            ResolveBest(
                controls.Where(c => ContainsAny(c.Name, "Fan", "Pump")),
                ["CPU", "Processor", "Pump"], ["Chassis", "Case", "System"],
                "No CPU or pump duty-cycle control sensor exposed by the motherboard.");

        public static SensorMatch ResolveCpuSecondaryFanRpm(IEnumerable<ISensor> fans, ISensor? primaryFan)
        {
            var ranked = Rank(fans, ["CPU", "Pump", "System", "Chassis", "Case"]).ToList();
            var candidates = ranked
                .Where(f => primaryFan == null || f.Identifier.ToString() != primaryFan.Identifier.ToString())
                .ToList();

            if (candidates.Count == 0)
                return SensorMatch.Missing("No secondary CPU fan or pump RPM sensor is available.");

            var pumpCandidates = candidates.Where(f => ContainsAny(f.Name, "Pump", "AIO", "Water", "Liquid")).ToList();
            if (pumpCandidates.Count > 0)
            {
                return new SensorMatch
                {
                    Sensor = pumpCandidates[0],
                    State = pumpCandidates.Count > 1 ? SemanticResolutionState.Ambiguous : SemanticResolutionState.Matched,
                    CandidateCount = pumpCandidates.Count,
                    Reason = pumpCandidates.Count > 1
                        ? "Multiple pump-like RPM sensors matched."
                        : "Resolved secondary CPU cooling sensor as pump RPM."
                };
            }

            return new SensorMatch
            {
                Sensor = candidates[0],
                State = candidates.Count > 1 ? SemanticResolutionState.Ambiguous : SemanticResolutionState.Matched,
                CandidateCount = candidates.Count,
                Reason = candidates.Count > 1
                    ? "Multiple secondary RPM candidates matched."
                    : "Resolved secondary CPU cooling sensor."
            };
        }

        public static IEnumerable<ISensor> RankTemperatures(IEnumerable<ISensor> temps) =>
            Rank(temps, ["CPU", "System", "VRM", "MOS", "Chipset", "PCH"]);

        public static IEnumerable<ISensor> RankFans(IEnumerable<ISensor> fans) =>
            Rank(fans, ["CPU", "Pump", "System", "Chassis", "Case"]);

        public static ISensor? FindFanControl(IEnumerable<ISensor> controls, ISensor fan)
        {
            var controlList = controls.Where(c => ContainsAny(c.Name, "Fan", "Pump", "AIO", "Water", "Liquid")).ToList();
            if (controlList.Count == 0) return null;

            var fanName = fan.Name ?? string.Empty;
            var preferred = new List<string> { fanName };
            if (ContainsAny(fanName, "CPU", "Processor")) preferred.AddRange(["CPU", "Processor"]);
            if (ContainsAny(fanName, "Pump", "AIO", "Water", "Liquid")) preferred.AddRange(["Pump", "AIO", "Water", "Liquid"]);
            if (ContainsAny(fanName, "Chassis", "Case", "System", "VRM")) preferred.AddRange(["Chassis", "Case", "System", "VRM"]);

            return FindBest(controlList, [.. preferred]);
        }

        #endregion

        #region GPU fans

        public static SensorMatch ResolveGpuPrimaryFan(IEnumerable<ISensor> fans)
        {
            var ranked = Rank(fans, ["GPU", "Core", "Fan 1", "Left", "Primary"]).ToList();
            if (ranked.Count == 0)
                return SensorMatch.Missing("No GPU fan RPM sensor exposed by this adapter.");

            return new SensorMatch
            {
                Sensor = ranked[0],
                State = ranked.Count > 1 ? SemanticResolutionState.Ambiguous : SemanticResolutionState.Matched,
                CandidateCount = ranked.Count,
                Reason = ranked.Count > 1
                    ? "Multiple GPU fan RPM sensors matched."
                    : "Resolved GPU primary fan RPM sensor."
            };
        }

        public static SensorMatch ResolveGpuSecondaryFan(IEnumerable<ISensor> fans, ISensor? primaryFan)
        {
            var ranked = Rank(fans, ["GPU", "Core", "Fan 2", "Right", "Secondary"]).ToList();
            var candidates = ranked
                .Where(f => primaryFan == null || f.Identifier.ToString() != primaryFan.Identifier.ToString())
                .ToList();

            if (candidates.Count == 0)
                return SensorMatch.Missing("No secondary GPU fan RPM sensor is available.");

            return new SensorMatch
            {
                Sensor = candidates[0],
                State = candidates.Count > 1 ? SemanticResolutionState.Ambiguous : SemanticResolutionState.Matched,
                CandidateCount = candidates.Count,
                Reason = candidates.Count > 1
                    ? "Multiple secondary GPU fan RPM sensors matched."
                    : "Resolved GPU secondary fan RPM sensor."
            };
        }

        #endregion

        #region Network adapter

        public static SelectionMatch<IHardware> ResolvePrimaryNetworkAdapter(List<IHardware> adapters)
        {
            var ranked = adapters
                .OrderByDescending(GetNetworkAdapterScore)
                .ThenBy(a => a.Name)
                .ToList();

            if (ranked.Count == 0)
                return SelectionMatch<IHardware>.Missing("No network adapters are available.");

            var selected = ranked[0];
            var selectedScore = GetNetworkAdapterScore(selected);
            var competing = ranked.Count(a => GetNetworkAdapterScore(a) == selectedScore);

            return new SelectionMatch<IHardware>
            {
                Value = selected,
                State = competing > 1 ? SemanticResolutionState.Ambiguous : SemanticResolutionState.Matched,
                CandidateCount = ranked.Count,
                Reason = competing > 1
                    ? "Multiple network adapters tied for primary selection."
                    : "Resolved primary network adapter."
            };
        }

        private static int GetNetworkAdapterScore(IHardware adapter)
        {
            var score = GetScore(adapter.Name,
                ["Ethernet", "Wi-Fi", "WiFi", "LAN", "Intel", "Realtek"],
                ["Bluetooth", "Virtual", "Loopback", "VMware", "Hyper-V", "vEthernet", "VPN", "Teredo"]);

            if (adapter.Sensors.Any(s =>
                (s.SensorType == SensorType.Throughput || s.SensorType == SensorType.Data) && s.Value.HasValue))
                score += 100;

            return score;
        }

        #endregion

        #region Memory

        public static bool IsVirtualMemorySensor(ISensor sensor)
        {
            var id = sensor.Identifier.ToString();
            if (ContainsAny(sensor.Name, "Virtual"))
                return true;
            if (id.Contains("/virtual/", StringComparison.OrdinalIgnoreCase))
                return true;
            if (id.EndsWith("/data/2", StringComparison.OrdinalIgnoreCase)
                || id.EndsWith("/data/3", StringComparison.OrdinalIgnoreCase)
                || id.EndsWith("/load/1", StringComparison.OrdinalIgnoreCase))
                return true;
            return false;
        }

        public static bool TryGetDimmSlot(ISensor sensor, out int slot)
        {
            slot = 0;
            var id = sensor.Identifier.ToString();
            var marker = "/memory/dimm/";
            var idx = id.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            if (idx < 0) return false;
            var start = idx + marker.Length;
            var end = start;
            while (end < id.Length && char.IsDigit(id[end])) end++;
            return end != start && int.TryParse(id[start..end], out slot);
        }

        #endregion

        #region Scoring & ranking

        private static IEnumerable<ISensor> Rank(IEnumerable<ISensor> sensors, string[] prefer, string[]? avoid = null)
        {
            var list = sensors?.ToList() ?? [];
            return list
                .OrderByDescending(s => GetScore(s.Name, prefer, avoid))
                .ThenBy(s => s.Name);
        }

        private static int GetScore(string? name, IEnumerable<string> prefer, IEnumerable<string>? avoid = null)
        {
            if (string.IsNullOrWhiteSpace(name)) return 0;

            var score = 0;
            foreach (var term in prefer)
                if (name.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0)
                    score += Math.Max(1, term.Length);

            if (avoid != null)
                foreach (var term in avoid)
                    if (name.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0)
                        score -= Math.Max(1, term.Length);

            return score;
        }

        private static bool ContainsAny(string? value, params string[] terms)
        {
            if (string.IsNullOrWhiteSpace(value)) return false;
            return terms.Any(t => value.IndexOf(t, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        #endregion
    }
}
