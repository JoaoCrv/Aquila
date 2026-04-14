using Aquila.Models.Api;
using LibreHardwareMonitor.Hardware;
using System;
using System.Collections;
using System.Collections.ObjectModel;
using System.Linq;

namespace Aquila.Services.Providers
{
    public class LhmProvider : IDataProvider
    {
        private readonly Computer _computer;
        private readonly ProviderUpdateVisitor _visitor = new();
        private bool _disposed;

        public IComputer Computer => _computer;

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
                IsControllerEnabled = true,
                IsBatteryEnabled = true,
                IsPowerMonitorEnabled = true,
                IsPsuEnabled = true
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
                    nodes.Motherboard.Name = hw.Name;
                    ExtractAndSortSensors(hw, nodes.Motherboard);
                    break;
                case HardwareType.GpuNvidia:
                case HardwareType.GpuAmd:
                case HardwareType.GpuIntel:
                    var gpu = nodes.Gpus.FirstOrDefault(g => g.Name == hw.Name);
                    if (gpu == null) { gpu = new GpuNode { Name = hw.Name, Vendor = hw.HardwareType.ToString() }; nodes.Gpus.Add(gpu); }
                    ExtractAndSortSensors(hw, gpu);
                    break;
                case HardwareType.Cpu:
                    nodes.Cpu.Name = hw.Name;
                    ExtractAndSortSensors(hw, nodes.Cpu);
                    break;
                case HardwareType.Memory:
                    nodes.Memory.Name = hw.Name;
                    ExtractAndSortSensors(hw, nodes.Memory);
                    break;
                case HardwareType.Storage:
                    var drive = nodes.Drives.FirstOrDefault(d => d.Name == hw.Name);
                    if (drive == null) { drive = new StorageNode { Name = hw.Name }; nodes.Drives.Add(drive); }
                    ExtractAndSortSensors(hw, drive);
                    break;
                case HardwareType.Network:
                    var net = nodes.NetworkAdapters.FirstOrDefault(n => n.Name == hw.Name);
                    if (net == null) { net = new NetworkNode { Name = hw.Name }; nodes.NetworkAdapters.Add(net); }
                    ExtractAndSortSensors(hw, net);
                    break;
            }

            // Não Processamos o SubHardware individualmente no loop base, porque o ExtractAndSortSensors já varre os SubHardwares de forma orgânica e os anexa à raiz desse componente (ex: SuperIO anexado à Motherboard)!

        }

        private void ExtractAndSortSensors(IHardware hw, BaseHardwareNode node)
        {
            foreach (var sensor in hw.Sensors)
            {
                AppendSensorToNode(sensor, node);
            }

            foreach (var sub in hw.SubHardware)
            {
                ExtractAndSortSensors(sub, node);
            }
        }

        private static void AppendSensorToNode(ISensor lhmSensor, BaseHardwareNode node)
        {
            IList? list = GetCollectionForSensorType(lhmSensor.SensorType, node);
            if (list == null) return;

            var identifier = lhmSensor.Identifier.ToString();

            SensorNode? existing = null;
            foreach (SensorNode sn in list)
            {
                if (sn.Identifier == identifier)
                {
                    existing = sn;
                    break;
                }
            }

            if (existing != null)
            {
                UpdateSensor(existing, lhmSensor);
            }
            else
            {
                var newNode = (lhmSensor.SensorType == SensorType.Fan) ? new FanNode(lhmSensor.Name) : new SensorNode(lhmSensor.Name);
                newNode.Identifier = identifier;
                UpdateSensor(newNode, lhmSensor);
                list.Add(newNode);
            }
        }

        private static IList? GetCollectionForSensorType(SensorType type, BaseHardwareNode node)
        {
            return type switch
            {
                SensorType.Temperature => node.Temperatures,
                SensorType.Load => node.Loads,
                SensorType.Clock => node.Clocks,
                SensorType.Power => node.Powers,
                SensorType.Voltage => node.Voltages,
                SensorType.Data or SensorType.SmallData => node.Data,
                SensorType.Throughput => node.Throughput,
                SensorType.Control => node.Controls,
                SensorType.Fan => node.Fans,
                _ => null, // Ex: Factor, Level (ignorados ou podes mapear se quiseres)
            };
        }

        private static void UpdateSensor(SensorNode node, ISensor lhmSensor)
        {
            node.Name = lhmSensor.Name;
            node.Unit = GetUnit(lhmSensor.SensorType, lhmSensor.Name);
            node.Value = lhmSensor.Value;
            node.Min = lhmSensor.Min;
            node.Max = lhmSensor.Max;
        }

        private static string GetUnit(SensorType sensorType, string sensorName)
        {
            return sensorType switch
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
