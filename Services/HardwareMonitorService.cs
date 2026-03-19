using Aquila.Models;
using LibreHardwareMonitor.Hardware;
using System;
using System.Linq;
using System.Windows.Threading;

namespace Aquila.Services
{
    public class UpdateVisitor : IVisitor
    {
        public void VisitComputer(IComputer computer) => computer.Traverse(this);
        public void VisitHardware(IHardware hardware)
        {
            hardware.Update();
            foreach (IHardware subHardware in hardware.SubHardware) subHardware.Accept(this);
        }
        public void VisitSensor(ISensor sensor) { }
        public void VisitParameter(IParameter parameter) { }
    }

    /// <summary>
    /// The ONE and ONLY hardware service. Reads raw data and transforms it.
    /// </summary>
    public class HardwareMonitorService : IDisposable
    {
        private Computer? _computer;
        private DispatcherTimer? _timer;
        private bool _disposed;

        public event Action? DataUpdated;
        public ComputerData ComputerData { get; } = new();

        public void StartMonitoring()
        {
            if (_computer != null) return;

            _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _timer.Tick += UpdateDataModel;

            _computer = new Computer
            {
                IsCpuEnabled = true,
                IsGpuEnabled = true,
                IsMemoryEnabled = true,
                IsMotherboardEnabled = true,
                IsStorageEnabled = true,
                IsNetworkEnabled = true
            };

            try
            {
                _computer.Open();
                _timer.Start();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[HardwareMonitorService] Failed to start: {ex}");
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            _timer?.Stop();
            _timer = null;

            _computer?.Close();
            _computer = null;
        }

        private void UpdateDataModel(object? sender, EventArgs e)
        {
            if (_computer == null) return;

            _computer.Accept(new UpdateVisitor());

            foreach (var rawHardware in _computer.Hardware)
            {
                var hardwareNode = ComputerData.HardwareList.FirstOrDefault(h => h.Identifier == rawHardware.Identifier.ToString());
                if (hardwareNode == null)
                {
                    hardwareNode = new DataHardware(rawHardware.Identifier.ToString(), rawHardware.Name, rawHardware.HardwareType);
                    ComputerData.HardwareList.Add(hardwareNode);
                }

                var allSensors = rawHardware.Sensors.Concat(rawHardware.SubHardware.SelectMany(s => s.Sensors));
                foreach (var rawSensor in allSensors)
                {
                    var sensorId = rawSensor.Identifier.ToString();
                    if (!ComputerData.SensorIndex.TryGetValue(sensorId, out var dataSensor))
                    {
                        dataSensor = new DataSensor(
                            rawSensor.Index,
                            sensorId,
                            rawSensor.Name,
                            rawSensor.SensorType,
                            GetSensorUnit(rawSensor.SensorType));
                        ComputerData.SensorIndex[sensorId] = dataSensor;
                        hardwareNode.Sensors.Add(dataSensor);
                    }
                    dataSensor.Value = rawSensor.Value ?? 0;
                    dataSensor.Min = rawSensor.Min ?? 0;
                    dataSensor.Max = rawSensor.Max ?? 0;
                }
            }

            DataUpdated?.Invoke();
        }

        private static string GetSensorUnit(SensorType type) => type switch
        {
            SensorType.Temperature => "°C",
            SensorType.Load => "%",
            SensorType.Clock => "MHz",
            SensorType.Power => "W",
            SensorType.Fan => "RPM",
            SensorType.Data => "GB",
            SensorType.SmallData => "MB",
            SensorType.Throughput => "B/s",
            SensorType.Voltage => "V",
            SensorType.Frequency => "Hz",
            SensorType.Control => "%",
            _ => string.Empty
        };
    }
}