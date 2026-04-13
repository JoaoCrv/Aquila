using Aquila.Models.Api;
using Aquila.Services.Semantics;
using System.Linq;

namespace Aquila.Helpers
{
    public static class AquilaSnapshotBuilder
    {
        public static void PopulateSemantic(AquilaState state)
        {
            var raw = state.Hardware;
            var semantic = state.Semantic;

            var cpuFanResolution = AquilaSensorSelector.ResolveCpuFanRpm(raw.Motherboard);
            var cpuSecondaryFanResolution = AquilaSensorSelector.ResolveCpuSecondaryFanRpm(raw.Motherboard);
            var cpuFanControlResolution = AquilaSensorSelector.ResolveCpuFanControl(raw.Motherboard);

            semantic.Motherboard.Name = raw.Motherboard.Name;
            semantic.Cpu.Name = raw.Cpu.Name;
            semantic.Memory.Name = raw.Memory.Name;

            semantic.Cpu.Load.Total = AquilaSensorSelector.FindCpuTotalLoad(raw.Cpu);
            semantic.Cpu.Temperature.Package = AquilaSensorSelector.FindCpuTemperature(raw.Cpu);
            semantic.Cpu.Clock.Effective = AquilaSensorSelector.FindCpuEffectiveClock(raw.Cpu);
            semantic.Cpu.Power.Package = AquilaSensorSelector.FindCpuPower(raw.Cpu);

            semantic.Motherboard.Fan.Cpu = cpuFanResolution.Sensor;
            semantic.Motherboard.Fan.Secondary = cpuSecondaryFanResolution.Sensor;
            semantic.Motherboard.Control.Cpu = cpuFanControlResolution.Sensor;
            ApplyResolution(semantic.Motherboard.Fan.CpuResolution, cpuFanResolution);
            ApplyResolution(semantic.Motherboard.Fan.SecondaryResolution, cpuSecondaryFanResolution);
            ApplyResolution(semantic.Motherboard.Control.CpuResolution, cpuFanControlResolution);

            semantic.Cpu.Cores.Clear();
            foreach (var core in AquilaSensorSelector.FindCpuCoreLoads(raw.Cpu))
            {
                semantic.Cpu.Cores.Add(core);
            }

            semantic.Motherboard.Temperature.System = AquilaSensorSelector.GetMotherboardTemperatures(raw.Motherboard).FirstOrDefault();

            semantic.Motherboard.Temperatures.Clear();
            foreach (var sensor in AquilaSensorSelector.GetMotherboardTemperatures(raw.Motherboard))
            {
                semantic.Motherboard.Temperatures.Add(sensor);
            }

            semantic.Motherboard.Fans.Clear();
            foreach (var sensor in AquilaSensorSelector.GetMotherboardFans(raw.Motherboard))
            {
                var control = AquilaSensorSelector.FindMotherboardFanControl(raw.Motherboard, sensor);
                var fanNode = new FanNode(sensor.Name)
                {
                    Identifier = sensor.Identifier,
                    Unit = sensor.Unit,
                    Value = sensor.Value,
                    Min = sensor.Min,
                    Max = sensor.Max,
                    ControlPercent = control?.Value,
                };

                semantic.Motherboard.Fans.Add(fanNode);
            }

            semantic.Memory.Load.Total = AquilaSensorSelector.FindMemoryLoad(raw.Memory);
            semantic.Memory.Data.Used = AquilaSensorSelector.FindMemoryUsed(raw.Memory);
            semantic.Memory.Data.Available = AquilaSensorSelector.FindMemoryAvailable(raw.Memory);
            semantic.Memory.Data.Total = AquilaSensorSelector.FindMemoryTotal(raw.Memory);
            semantic.Memory.Data.Cache = AquilaSensorSelector.FindMemoryCache(raw.Memory);
            semantic.Memory.Data.PageReads = AquilaSensorSelector.FindMemoryPageReads(raw.Memory);
            semantic.Memory.Data.PageWrites = AquilaSensorSelector.FindMemoryPageWrites(raw.Memory);
            semantic.Memory.Power.Total = AquilaSensorSelector.FindMemoryPower(raw.Memory);

            var primaryNet = raw.NetworkAdapters.FirstOrDefault();
            semantic.Network.PrimaryAdapterName = primaryNet?.Name ?? string.Empty;
            semantic.Network.Throughput.Download = primaryNet is null ? null : AquilaSensorSelector.FindNetworkDownload(primaryNet);
            semantic.Network.Throughput.Upload = primaryNet is null ? null : AquilaSensorSelector.FindNetworkUpload(primaryNet);
            semantic.Network.Data.Downloaded = primaryNet is null ? null : AquilaSensorSelector.FindNetworkDataDownloaded(primaryNet);
            semantic.Network.Data.Uploaded = primaryNet is null ? null : AquilaSensorSelector.FindNetworkDataUploaded(primaryNet);

            semantic.Gpus.Clear();
            foreach (var gpu in raw.Gpus)
            {
                var gpuPowerResolution = AquilaSensorSelector.ResolveGpuPower(gpu);
                var gpuPrimaryFanResolution = AquilaSensorSelector.ResolveGpuPrimaryFan(gpu);
                var gpuSecondaryFanResolution = AquilaSensorSelector.ResolveGpuSecondaryFan(gpu);

                var semanticGpu = new GpuSemanticNode
                {
                    Name = gpu.Name,
                };
                semanticGpu.Load.Total = AquilaSensorSelector.FindGpuLoad(gpu);
                semanticGpu.Temperature.Core = AquilaSensorSelector.FindGpuTemperature(gpu);
                semanticGpu.Temperature.Hotspot = AquilaSensorSelector.FindGpuHotspotTemperature(gpu);
                semanticGpu.Clock.Core = AquilaSensorSelector.FindGpuCoreClock(gpu);
                semanticGpu.Power.Package = gpuPowerResolution.Sensor;
                ApplyResolution(semanticGpu.Power.PackageResolution, gpuPowerResolution);
                semanticGpu.Data.Used = AquilaSensorSelector.FindGpuVramUsed(gpu);
                semanticGpu.Data.Total = AquilaSensorSelector.FindGpuVramTotal(gpu);
                semanticGpu.Fan.Primary = gpuPrimaryFanResolution.Sensor;
                semanticGpu.Fan.Secondary = gpuSecondaryFanResolution.Sensor;
                ApplyResolution(semanticGpu.Fan.PrimaryResolution, gpuPrimaryFanResolution);
                ApplyResolution(semanticGpu.Fan.SecondaryResolution, gpuSecondaryFanResolution);
                semantic.Gpus.Add(semanticGpu);
            }

            semantic.Storage.Clear();
            foreach (var drive in raw.Drives)
            {
                var semanticDrive = new StorageSemanticNode
                {
                    Name = drive.Name,
                };
                semanticDrive.Temperature.System = AquilaSensorSelector.FindStorageTemperature(drive);
                semanticDrive.Throughput.Read = AquilaSensorSelector.FindStorageReadRate(drive);
                semanticDrive.Throughput.Write = AquilaSensorSelector.FindStorageWriteRate(drive);
                semanticDrive.Load.Total = AquilaSensorSelector.FindStorageUsedSpace(drive);
                semanticDrive.Data.Read = AquilaSensorSelector.FindStorageDataRead(drive);
                semanticDrive.Data.Written = AquilaSensorSelector.FindStorageDataWritten(drive);
                semantic.Storage.Add(semanticDrive);
            }
        }

        private static void ApplyResolution<T>(SensorResolutionNode target, SensorSelectionResult<T> result)
            where T : SensorNode
        {
            target.State = result.State;
            target.CandidateCount = result.CandidateCount;
            target.Reason = result.Reason;
        }
    }
}