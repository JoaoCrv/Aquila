using Aquila.Models.Api;
using LibreHardwareMonitor.Hardware;
using System;
using System.Linq;

namespace Aquila.Services.Providers
{
    public class LhmProvider : IDataProvider
    {
        private readonly Computer _computer;
        private readonly ProviderUpdateVisitor _visitor = new();
        private bool _disposed;

        public LhmProvider()
        {
            _computer = new Computer
            {
                IsCpuEnabled = true,
                IsGpuEnabled = true,
                IsMemoryEnabled = true,
                IsMotherboardEnabled = true,
                IsNetworkEnabled = true,
                IsStorageEnabled = true,
                IsControllerEnabled = true
            };
        }

        public void Initialize()
        {
            _computer.Open();
        }

        public void Populate(AquilaState apiState)
        {
            if (_disposed) return;
            
            _computer.Accept(_visitor);

            foreach (var hw in _computer.Hardware)
            {
                ProcessHardware(hw, apiState.Hardware);
            }
        }

        private void ProcessHardware(IHardware hw, HardwareNodes nodes)
        {
            switch (hw.HardwareType)
            {
                case HardwareType.Motherboard:
                    ProcessMotherboard(hw, nodes.Motherboard);
                    break;
                case HardwareType.GpuNvidia:
                case HardwareType.GpuAmd:
                case HardwareType.GpuIntel:
                    ProcessGpu(hw, nodes);
                    break;
                case HardwareType.Cpu:
                    ProcessCpu(hw, nodes.Cpu);
                    break;
                case HardwareType.Memory:
                    ProcessMemory(hw, nodes.Memory);
                    break;
                case HardwareType.Storage:
                    ProcessStorage(hw, nodes);
                    break;
                case HardwareType.Network:
                    ProcessNetwork(hw, nodes);
                    break;
            }

            foreach (var sub in hw.SubHardware)
            {
                ProcessHardware(sub, nodes);
            }
        }

        private void ProcessMotherboard(IHardware hw, MotherboardNode node)
        {
            node.Name = hw.Name;
            
            foreach (var sub in hw.SubHardware)
            {
                foreach (var sensor in sub.Sensors)
                {
                    if (sensor.SensorType == SensorType.Temperature)
                    {
                        if (Contains(sensor, "CPU")) UpdateSensor(node.CpuTemperature, sensor);
                        else if (Contains(sensor, "System")) UpdateSensor(node.SystemTemperature, sensor);
                        else if (Contains(sensor, "VRM")) UpdateSensor(node.VrmTemperature, sensor);
                        else if (Contains(sensor, "Chipset")) UpdateSensor(node.ChipsetTemperature, sensor);
                    }
                }
            }
        }

        private void ProcessCpu(IHardware hw, CpuNode node)
        {
            node.Name = hw.Name;
            foreach (var sensor in hw.Sensors)
            {
                if (sensor.SensorType == SensorType.Load && Contains(sensor, "Total"))
                    UpdateSensor(node.Load, sensor);
                else if (sensor.SensorType == SensorType.Temperature && Contains(sensor, "Package", "Tdie", "Tctl/Tdie"))
                    UpdateSensor(node.Temperature, sensor);
                else if (sensor.SensorType == SensorType.Power && Contains(sensor, "Package"))
                    UpdateSensor(node.Power, sensor);
                else if (sensor.SensorType == SensorType.Clock && Contains(sensor, "Effective"))
                    UpdateSensor(node.Clock, sensor);
            }
        }

        private void ProcessGpu(IHardware hw, HardwareNodes nodes)
        {
            var node = nodes.Gpus.FirstOrDefault(g => g.Name == hw.Name);
            if (node == null)
            {
                node = new GpuNode { Name = hw.Name };
                nodes.Gpus.Add(node);
            }
            
            foreach (var sensor in hw.Sensors)
            {
                if (sensor.SensorType == SensorType.Load && Contains(sensor, "Core")) UpdateSensor(node.Load, sensor);
                else if (sensor.SensorType == SensorType.Temperature && Contains(sensor, "Core")) UpdateSensor(node.Temperature, sensor);
                else if (sensor.SensorType == SensorType.Temperature && Contains(sensor, "Hot Spot")) UpdateSensor(node.HotSpotTemperature, sensor);
                else if (sensor.SensorType == SensorType.Power && Contains(sensor, "Total", "Package")) UpdateSensor(node.Power, sensor);
                else if (sensor.SensorType == SensorType.Clock && Contains(sensor, "Core") && !Contains(sensor, "Memory")) UpdateSensor(node.Clock, sensor);
                else if (sensor.SensorType == SensorType.Clock && Contains(sensor, "Memory")) UpdateSensor(node.MemoryClock, sensor);
                else if (sensor.SensorType == SensorType.SmallData && Contains(sensor, "Memory Used")) UpdateSensor(node.VramUsed, sensor);
                else if (sensor.SensorType == SensorType.SmallData && Contains(sensor, "Memory Total")) UpdateSensor(node.VramTotal, sensor);
            }
        }

        private void ProcessMemory(IHardware hw, MemoryNode node)
        {
            foreach (var sensor in hw.Sensors)
            {
                if (sensor.SensorType == SensorType.Data && Contains(sensor, "Used")) UpdateSensor(node.UsedGb, sensor);
                else if (sensor.SensorType == SensorType.Data && Contains(sensor, "Available")) UpdateSensor(node.AvailableGb, sensor);
                else if (sensor.SensorType == SensorType.Load && Contains(sensor, "Memory")) UpdateSensor(node.Load, sensor);
            }
            
            if (node.UsedGb.Value.HasValue && node.AvailableGb.Value.HasValue && node.TotalGb.Value == null)
            {
                node.TotalGb.Value = node.UsedGb.Value + node.AvailableGb.Value;
                // Min Max tracking can be ignored for static total
            }
        }

        private void ProcessStorage(IHardware hw, HardwareNodes nodes)
        {
            var node = nodes.Drives.FirstOrDefault(d => d.Name == hw.Name);
            if (node == null)
            {
                node = new StorageNode { Name = hw.Name };
                nodes.Drives.Add(node);
            }

            foreach (var sensor in hw.Sensors)
            {
                if (sensor.SensorType == SensorType.Temperature && Contains(sensor, "Temperature", "Composite")) UpdateSensor(node.Temperature, sensor);
                else if (sensor.SensorType == SensorType.Load && Contains(sensor, "Used Space")) UpdateSensor(node.UsedPercent, sensor);
                else if (sensor.SensorType == SensorType.Throughput && Contains(sensor, "Read Rate")) UpdateSensor(node.ReadRate, sensor);
                else if (sensor.SensorType == SensorType.Throughput && Contains(sensor, "Write Rate")) UpdateSensor(node.WriteRate, sensor);
                else if (sensor.SensorType == SensorType.Data && Contains(sensor, "Data Read")) UpdateSensor(node.DataRead, sensor);
                else if (sensor.SensorType == SensorType.Data && Contains(sensor, "Data Written")) UpdateSensor(node.DataWritten, sensor);
            }
        }

        private void ProcessNetwork(IHardware hw, HardwareNodes nodes)
        {
            var node = nodes.NetworkAdapters.FirstOrDefault(n => n.Name == hw.Name);
            if (node == null)
            {
                node = new NetworkNode { Name = hw.Name };
                nodes.NetworkAdapters.Add(node);
            }

            foreach (var sensor in hw.Sensors)
            {
                if (sensor.SensorType == SensorType.Throughput && Contains(sensor, "Upload Speed")) UpdateSensor(node.UploadSpeed, sensor);
                else if (sensor.SensorType == SensorType.Throughput && Contains(sensor, "Download Speed")) UpdateSensor(node.DownloadSpeed, sensor);
                else if (sensor.SensorType == SensorType.Data && Contains(sensor, "Uploaded")) UpdateSensor(node.DataUploaded, sensor);
                else if (sensor.SensorType == SensorType.Data && Contains(sensor, "Downloaded")) UpdateSensor(node.DataDownloaded, sensor);
            }
        }

        private static bool Contains(ISensor sensor, params string[] terms)
        {
            return terms.Any(t => sensor.Name.IndexOf(t, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private static void UpdateSensor(SensorNode node, ISensor lhmSensor)
        {
            node.Value = lhmSensor.Value;
            node.Min = lhmSensor.Min;
            node.Max = lhmSensor.Max;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _computer.Close();
        }

        private sealed class ProviderUpdateVisitor : IVisitor
        {
            public void VisitComputer(IComputer computer) => computer.Traverse(this);
            public void VisitHardware(IHardware hardware)
            {
                hardware.Update();
                foreach (IHardware subHardware in hardware.SubHardware)
                    subHardware.Accept(this);
            }
            public void VisitSensor(ISensor sensor) { }
            public void VisitParameter(IParameter parameter) { }
        }
    }
}
