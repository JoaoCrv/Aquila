using Aquila.Models.Api;
using LibreHardwareMonitor.Hardware;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Aquila.Services.Translators
{
    /// <summary>
    /// Translates raw LibreHardwareMonitor data directly into the semantic state.
    /// Works with IHardware/ISensor natively — no intermediate bucket layer.
    /// SensorNode objects are created only for selected sensors and cached across ticks.
    /// </summary>
    public sealed class LhmSemanticTranslator
    {
        private readonly IComputer _computer;
        private readonly LhmUpdateVisitor _visitor = new();
        private readonly Dictionary<string, SensorNode> _cache = new();

        public LhmSemanticTranslator(IComputer computer)
        {
            _computer = computer;
        }

        public void Update(AquilaSemanticState semantic)
        {
            _computer.Accept(_visitor);

            IHardware? motherboard = null, cpu = null, memory = null;
            var gpus = new List<IHardware>();
            var drives = new List<IHardware>();
            var networks = new List<IHardware>();

            foreach (var hw in _computer.Hardware)
            {
                switch (hw.HardwareType)
                {
                    case HardwareType.Motherboard: motherboard = hw; break;
                    case HardwareType.Cpu: cpu = hw; break;
                    case HardwareType.Memory: memory = hw; break;
                    case HardwareType.GpuNvidia:
                    case HardwareType.GpuAmd:
                    case HardwareType.GpuIntel: gpus.Add(hw); break;
                    case HardwareType.Storage: drives.Add(hw); break;
                    case HardwareType.Network: networks.Add(hw); break;
                }
            }

            TranslateMotherboard(semantic, motherboard);
            TranslateCpu(semantic, cpu);
            TranslateMemory(semantic, memory);
            TranslateGpus(semantic, gpus);
            TranslateStorage(semantic, drives);
            TranslateNetwork(semantic, networks);
        }

        #region Flatten

        private static List<ISensor> Flatten(IHardware hw)
        {
            var result = new List<ISensor>(hw.Sensors);
            foreach (var sub in hw.SubHardware)
                result.AddRange(Flatten(sub));
            return result;
        }

        private static IEnumerable<ISensor> OfType(List<ISensor> sensors, SensorType type) =>
            sensors.Where(s => s.SensorType == type);

        #endregion

        #region Node cache

        private SensorNode? Pick(ISensor? sensor)
        {
            if (sensor == null) return null;
            return Sync(sensor);
        }

        private SensorNode? Pick(List<ISensor> sensors, SensorType type, string[] prefer, string[]? avoid = null) =>
            Pick(LhmSensorSelector.FindBest(OfType(sensors, type), prefer, avoid));

        private FanNode? PickFan(ISensor? sensor)
        {
            if (sensor == null) return null;
            return (FanNode)Sync(sensor, isFan: true);
        }

        private SensorNode Sync(ISensor sensor, bool isFan = false)
        {
            var id = sensor.Identifier.ToString();
            if (!_cache.TryGetValue(id, out var node))
            {
                node = (isFan || sensor.SensorType == SensorType.Fan)
                    ? new FanNode(sensor.Name)
                    : new SensorNode(sensor.Name);
                node.Identifier = id;
                node.Unit = GetUnit(sensor.SensorType);
                _cache[id] = node;
            }
            node.Name = sensor.Name;
            node.Value = sensor.Value;
            node.Min = sensor.Min;
            node.Max = sensor.Max;
            return node;
        }

        #endregion

        #region Translate

        private void TranslateMotherboard(AquilaSemanticState semantic, IHardware? hw)
        {
            if (hw == null) return;
            semantic.Motherboard.Name = hw.Name;
            var sensors = Flatten(hw);
            var fans = OfType(sensors, SensorType.Fan).ToList();
            var controls = OfType(sensors, SensorType.Control).ToList();

            var cpuFanRes = LhmSensorSelector.ResolveCpuFanRpm(fans);
            var cpuSecFanRes = LhmSensorSelector.ResolveCpuSecondaryFanRpm(fans, cpuFanRes.Sensor);
            var cpuFanCtrlRes = LhmSensorSelector.ResolveCpuFanControl(controls);

            semantic.Motherboard.Fan.Cpu = PickFan(cpuFanRes.Sensor);
            semantic.Motherboard.Fan.Secondary = PickFan(cpuSecFanRes.Sensor);
            semantic.Motherboard.Control.Cpu = Pick(cpuFanCtrlRes.Sensor);
            ApplyResolution(semantic.Motherboard.Fan.CpuResolution, cpuFanRes);
            ApplyResolution(semantic.Motherboard.Fan.SecondaryResolution, cpuSecFanRes);
            ApplyResolution(semantic.Motherboard.Control.CpuResolution, cpuFanCtrlRes);

            var rankedTemps = LhmSensorSelector.RankTemperatures(OfType(sensors, SensorType.Temperature)).ToList();
            semantic.Motherboard.Temperature.System = Pick(rankedTemps.FirstOrDefault());

            semantic.Motherboard.Temperatures.Clear();
            foreach (var t in rankedTemps)
                semantic.Motherboard.Temperatures.Add(Sync(t));

            semantic.Motherboard.Fans.Clear();
            foreach (var fan in LhmSensorSelector.RankFans(fans))
            {
                var control = LhmSensorSelector.FindFanControl(controls, fan);
                var fanNode = (FanNode)Sync(fan, isFan: true);
                fanNode.ControlPercent = control?.Value;
                semantic.Motherboard.Fans.Add(fanNode);
            }
        }

        private void TranslateCpu(AquilaSemanticState semantic, IHardware? hw)
        {
            if (hw == null) return;
            semantic.Cpu.Name = hw.Name;
            var sensors = Flatten(hw);

            semantic.Cpu.Load.Total = Pick(sensors, SensorType.Load, ["Total", "CPU Total", "Core (Avg)", "Package"], ["Max"]);
            semantic.Cpu.Temperature.Package = Pick(sensors, SensorType.Temperature, ["Package", "Tctl/Tdie", "Tdie", "CPU", "Core (Avg)"], ["Max"]);
            semantic.Cpu.Clock.Effective = Pick(sensors, SensorType.Clock, ["Effective", "Core (Avg)", "Average", "Core Clock", "Core"], ["Bus", "SoC", "Memory"]);
            semantic.Cpu.Power.Package = Pick(sensors, SensorType.Power, ["Package", "Total", "CPU"], ["Limit"]);

            semantic.Cpu.Cores.Clear();
            foreach (var core in LhmSensorSelector.FindCpuCoreLoads(OfType(sensors, SensorType.Load)))
                semantic.Cpu.Cores.Add(Sync(core));
        }

        private void TranslateMemory(AquilaSemanticState semantic, IHardware? hw)
        {
            if (hw == null) return;
            semantic.Memory.Name = hw.Name;
            var sensors = Flatten(hw);
            var loads = OfType(sensors, SensorType.Load).ToList();
            var data = OfType(sensors, SensorType.Data).Concat(OfType(sensors, SensorType.SmallData)).ToList();
            var powers = OfType(sensors, SensorType.Power).ToList();
            var temps = OfType(sensors, SensorType.Temperature).ToList();

            var physLoads = loads.Where(s => !LhmSensorSelector.IsVirtualMemorySensor(s)).ToList();
            var physData = data.Where(s => !LhmSensorSelector.IsVirtualMemorySensor(s)).ToList();
            var virtLoads = loads.Where(LhmSensorSelector.IsVirtualMemorySensor).ToList();
            var virtData = data.Where(LhmSensorSelector.IsVirtualMemorySensor).ToList();

            semantic.Memory.Load.Total = Pick(LhmSensorSelector.FindBest(physLoads, ["Memory", "Used", "Utilization", "Load"]));
            semantic.Memory.Data.Used = Pick(LhmSensorSelector.FindBest(physData, ["Used", "In Use"]));
            semantic.Memory.Data.Available = Pick(LhmSensorSelector.FindBest(physData, ["Available", "Free"]));
            semantic.Memory.Power.Total = Pick(LhmSensorSelector.FindBest(powers, ["Total", "DRAM", "Memory"]));

            // LHM does not expose a physical RAM total sensor — synthesize from Used + Available.
            SynthesizeMemoryTotal(semantic);

            semantic.Memory.VirtualLoad = Pick(LhmSensorSelector.FindBest(virtLoads, ["Memory", "Load", "Used", "Virtual"]));
            semantic.Memory.VirtualUsed = Pick(LhmSensorSelector.FindBest(virtData, ["Used", "In Use", "Virtual"]));
            semantic.Memory.VirtualAvailable = Pick(LhmSensorSelector.FindBest(virtData, ["Available", "Free", "Virtual"]));

            // DIMMs
            semantic.Memory.Dimms.Clear();
            var dimmSensors = temps.Concat(data)
                .Where(s => LhmSensorSelector.TryGetDimmSlot(s, out _))
                .ToList();

            foreach (var group in dimmSensors.GroupBy(GetDimmSlot).OrderBy(g => g.Key))
            {
                var g = group.ToList();
                var nameSensor = LhmSensorSelector.FindBest(g, ["DIMM", $"#{group.Key}", "Memory"]) ?? g.FirstOrDefault();

                var dimm = new MemoryDimmSemanticNode
                {
                    Slot = group.Key,
                    Name = nameSensor?.Name ?? $"DIMM {group.Key}",
                    Capacity = Pick(LhmSensorSelector.FindBest(g, ["Capacity"])),
                    Temperature = Pick(LhmSensorSelector.FindBest(g, ["DIMM", "Temperature"], ["Resolution", "Limit", "Critical"])),
                    WarningTemperature = Pick(LhmSensorSelector.FindBest(g, ["High Limit", "Warning"], ["Critical"])),
                    CriticalTemperature = Pick(LhmSensorSelector.FindBest(g, ["Critical High Limit", "Critical"])),
                };

                if (dimm.Temperature is not null || dimm.Capacity is not null)
                    semantic.Memory.Dimms.Add(dimm);
            }
        }

        private void SynthesizeMemoryTotal(AquilaSemanticState semantic)
        {
            const string id = "__ram_total__";
            var used = semantic.Memory.Data.Used?.Value;
            var avail = semantic.Memory.Data.Available?.Value;
            if (!used.HasValue || !avail.HasValue) return;

            if (!_cache.TryGetValue(id, out var node))
            {
                node = new SensorNode("Memory Total") { Identifier = id, Unit = "GB" };
                _cache[id] = node;
            }
            node.Value = used.Value + avail.Value;
            semantic.Memory.Data.Total = node;
        }

        private void TranslateGpus(AquilaSemanticState semantic, List<IHardware> gpus)
        {
            semantic.Gpus.Clear();
            foreach (var hw in gpus)
            {
                var sensors = Flatten(hw);
                var fans = OfType(sensors, SensorType.Fan).ToList();

                var powerRes = LhmSensorSelector.ResolveBest(OfType(sensors, SensorType.Power),
                    ["Total", "Board", "Package", "GPU"], ["Limit"],
                    "No GPU package power sensor exposed by this adapter.");
                var primaryFanRes = LhmSensorSelector.ResolveGpuPrimaryFan(fans);
                var secondaryFanRes = LhmSensorSelector.ResolveGpuSecondaryFan(fans, primaryFanRes.Sensor);

                var node = new GpuSemanticNode { Name = hw.Name };
                node.Load.Total = Pick(sensors, SensorType.Load, ["Core", "D3D", "GPU"], ["Memory", "Bus"]);
                node.Temperature.Core = Pick(sensors, SensorType.Temperature, ["Core", "Edge", "Package"], ["Hot Spot", "Hotspot", "Junction"]);
                node.Temperature.Hotspot = Pick(sensors, SensorType.Temperature, ["Hot Spot", "Hotspot", "Junction"]);
                node.Clock.Core = Pick(sensors, SensorType.Clock, ["Core", "Graphics"], ["Memory", "Shader", "Video"]);
                node.Power.Package = Pick(powerRes.Sensor);
                ApplyResolution(node.Power.PackageResolution, powerRes);
                node.Data.Used = Pick(sensors, SensorType.Data, ["Memory Used", "VRAM Used", "Memory Allocated"]);
                node.Data.Total = Pick(sensors, SensorType.Data, ["Memory Total", "VRAM Total"]);
                node.Fan.Primary = PickFan(primaryFanRes.Sensor);
                node.Fan.Secondary = PickFan(secondaryFanRes.Sensor);
                ApplyResolution(node.Fan.PrimaryResolution, primaryFanRes);
                ApplyResolution(node.Fan.SecondaryResolution, secondaryFanRes);
                semantic.Gpus.Add(node);
            }
        }

        private void TranslateStorage(AquilaSemanticState semantic, List<IHardware> drives)
        {
            semantic.Storage.Clear();
            foreach (var hw in drives)
            {
                var sensors = Flatten(hw);
                var tempRes = LhmSensorSelector.ResolveBest(OfType(sensors, SensorType.Temperature),
                    ["Composite", "Temperature"], ["Air Flow"],
                    "No storage temperature sensor exposed by this drive.");
                var readRes = LhmSensorSelector.ResolveBest(OfType(sensors, SensorType.Throughput),
                    ["Read"], ["Write"],
                    "No storage read throughput sensor exposed by this drive.");
                var writeRes = LhmSensorSelector.ResolveBest(OfType(sensors, SensorType.Throughput),
                    ["Write"], ["Read"],
                    "No storage write throughput sensor exposed by this drive.");
                var usedRes = LhmSensorSelector.ResolveBest(OfType(sensors, SensorType.Load),
                    ["Used", "Space"], ["Free"],
                    "No storage usage/load sensor exposed by this drive.");
                var dataReadRes = LhmSensorSelector.ResolveBest(
                    OfType(sensors, SensorType.Data).Concat(OfType(sensors, SensorType.SmallData)),
                    ["Data Read", "Read"], ["Written", "Write"],
                    "No cumulative storage data-read sensor exposed by this drive.");
                var dataWrittenRes = LhmSensorSelector.ResolveBest(
                    OfType(sensors, SensorType.Data).Concat(OfType(sensors, SensorType.SmallData)),
                    ["Data Written", "Written", "Write"], ["Read"],
                    "No cumulative storage data-written sensor exposed by this drive.");

                var node = new StorageSemanticNode { Name = hw.Name };
                node.Temperature.System = Pick(tempRes.Sensor);
                node.Throughput.Read = Pick(readRes.Sensor);
                node.Throughput.Write = Pick(writeRes.Sensor);
                node.Load.Total = Pick(usedRes.Sensor);
                node.Data.Read = Pick(dataReadRes.Sensor);
                node.Data.Written = Pick(dataWrittenRes.Sensor);
                ApplyResolution(node.Temperature.SystemResolution, tempRes);
                ApplyResolution(node.Throughput.ReadResolution, readRes);
                ApplyResolution(node.Throughput.WriteResolution, writeRes);
                ApplyResolution(node.Load.TotalResolution, usedRes);
                ApplyResolution(node.Data.ReadResolution, dataReadRes);
                ApplyResolution(node.Data.WrittenResolution, dataWrittenRes);
                semantic.Storage.Add(node);
            }
        }

        private void TranslateNetwork(AquilaSemanticState semantic, List<IHardware> networks)
        {
            var primaryRes = LhmSensorSelector.ResolvePrimaryNetworkAdapter(networks);
            var primary = primaryRes.Value;
            semantic.Network.PrimaryAdapterName = primary?.Name ?? string.Empty;
            ApplyResolution(semantic.Network.PrimaryAdapterResolution, primaryRes);

            if (primary is null)
            {
                semantic.Network.Throughput.Download = null;
                semantic.Network.Throughput.Upload = null;
                semantic.Network.Data.Downloaded = null;
                semantic.Network.Data.Uploaded = null;
                ApplyMissing(semantic.Network.Throughput.DownloadResolution, "No primary network adapter was resolved.");
                ApplyMissing(semantic.Network.Throughput.UploadResolution, "No primary network adapter was resolved.");
                ApplyMissing(semantic.Network.Data.DownloadedResolution, "No primary network adapter was resolved.");
                ApplyMissing(semantic.Network.Data.UploadedResolution, "No primary network adapter was resolved.");
            }
            else
            {
                var sensors = Flatten(primary);
                var dlRes = LhmSensorSelector.ResolveBest(OfType(sensors, SensorType.Throughput),
                    ["Download", "Received", "Rx"], ["Upload", "Sent", "Tx"],
                    "No network download throughput sensor exposed by the selected adapter.");
                var ulRes = LhmSensorSelector.ResolveBest(OfType(sensors, SensorType.Throughput),
                    ["Upload", "Sent", "Tx"], ["Download", "Received", "Rx"],
                    "No network upload throughput sensor exposed by the selected adapter.");
                var dldRes = LhmSensorSelector.ResolveBest(OfType(sensors, SensorType.Data),
                    ["Downloaded", "Received", "Download"], ["Uploaded", "Sent", "Upload"],
                    "No cumulative downloaded data sensor exposed by the selected adapter.");
                var uldRes = LhmSensorSelector.ResolveBest(OfType(sensors, SensorType.Data),
                    ["Uploaded", "Sent", "Upload"], ["Downloaded", "Received", "Download"],
                    "No cumulative uploaded data sensor exposed by the selected adapter.");

                semantic.Network.Throughput.Download = Pick(dlRes.Sensor);
                semantic.Network.Throughput.Upload = Pick(ulRes.Sensor);
                semantic.Network.Data.Downloaded = Pick(dldRes.Sensor);
                semantic.Network.Data.Uploaded = Pick(uldRes.Sensor);
                ApplyResolution(semantic.Network.Throughput.DownloadResolution, dlRes);
                ApplyResolution(semantic.Network.Throughput.UploadResolution, ulRes);
                ApplyResolution(semantic.Network.Data.DownloadedResolution, dldRes);
                ApplyResolution(semantic.Network.Data.UploadedResolution, uldRes);
            }
        }

        #endregion

        #region Resolution helpers

        private static void ApplyResolution(SensorResolutionNode target, SensorMatch result)
        {
            target.State = result.State;
            target.CandidateCount = result.CandidateCount;
            target.Reason = result.Reason;
        }

        private static void ApplyResolution<T>(ResolutionNode target, SelectionMatch<T> result) where T : class
        {
            target.State = result.State;
            target.CandidateCount = result.CandidateCount;
            target.Reason = result.Reason;
        }

        private static void ApplyMissing(SensorResolutionNode target, string reason)
        {
            target.State = SemanticResolutionState.Missing;
            target.CandidateCount = 0;
            target.Reason = reason;
        }

        #endregion

        #region Helpers

        private static string GetUnit(SensorType sensorType) => sensorType switch
        {
            SensorType.Temperature => "°C",
            SensorType.Load or SensorType.Control or SensorType.Level => "%",
            SensorType.Clock => "MHz",
            SensorType.Power => "W",
            SensorType.Voltage => "V",
            SensorType.Current => "A",
            SensorType.Fan => "RPM",
            SensorType.Throughput => "B/s",
            SensorType.TimeSpan => "s",
            SensorType.Energy => "mWh",
            SensorType.Data => "GB",
            SensorType.SmallData => "MB",
            _ => string.Empty,
        };

        private static int GetDimmSlot(ISensor sensor) =>
            LhmSensorSelector.TryGetDimmSlot(sensor, out var slot) ? slot : int.MaxValue;

        #endregion

        private sealed class LhmUpdateVisitor : IVisitor
        {
            public void VisitComputer(IComputer computer) => computer.Traverse(this);
            public void VisitHardware(IHardware hardware)
            {
                hardware.Update();
                foreach (var sub in hardware.SubHardware)
                    sub.Accept(this);
            }
            public void VisitSensor(ISensor sensor) { }
            public void VisitParameter(IParameter parameter) { }
        }
    }
}
