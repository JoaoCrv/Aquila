using Aquila.Models;
using LibreHardwareMonitor.Hardware;
using System;
using System.Collections.ObjectModel;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Automation;
using System.Windows.Threading;


/// <summary>
/// Class to deal with hardware monitoring using LibreHardwareMonitor library.
/// </summary>
/// 



namespace Aquila.Services
{
    public class UpdateVisitor : IVisitor
    {
        public void VisitComputer(IComputer computer)
        {
            computer.Traverse(this);
        }
        public void VisitHardware(IHardware hardware)
        {
            hardware.Update();
            foreach (IHardware subHardware in hardware.SubHardware) subHardware.Accept(this);
        }
        public void VisitSensor(ISensor sensor) { }
        public void VisitParameter(IParameter parameter) { }
    }
    public class HardwareMonitorService
    {

        private Computer? computer;
        private DispatcherTimer? _timer;

        public ObservableCollection<SensorInfo> Sensors { get; } = new();

        public HardwareMonitorService()
        {
            _timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _timer.Tick += UpdateSensorReadings;

        }

        // Initialize monitoring for CPU, GPU, Memory, etc.
        // This method should set up the necessary hooks or listeners
        // to monitor the hardware components based on the enabled flags.
        public void StartMonitoring(bool enableCpu = true, bool enableGpu = true, bool enableMemory = true, bool enableMotherboard = true, bool enableController = true, bool enableNetwork = true, bool enableStorage = true)
        {
            System.Diagnostics.Debug.WriteLine($"[HardwareMonitorService] Start Monitoring");
            computer = new Computer
            {
                IsCpuEnabled = enableCpu,
                IsGpuEnabled = enableGpu,
                IsMemoryEnabled = enableMemory,
                IsMotherboardEnabled = enableMotherboard,
                IsStorageEnabled = enableStorage,
                IsNetworkEnabled = enableNetwork
            };
            try
            {

                computer.Open();
                computer.Accept(new UpdateVisitor());

                UpdateSensorReadings(null, EventArgs.Empty);
                _timer.Start();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error initializing hardware monitoring: {ex.Message}");
                return;
            }
        }
        public void StopMonitoring()
        {
            // Stop monitoring hardware components and release resources
            if (computer != null)
            {
                try
                {
                    computer.Close();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error during hardware monitoring cleanup: {ex.Message}");
                }
            }
        }

        private void UpdateSensorReadings(object? sender, EventArgs e)
        {
            if (computer == null)
            {
                return;
            }
            var currentReadings = GetUpdatedSensorReadings();
            System.Diagnostics.Debug.WriteLine($"[HardwareMonitorService] Leituras encontradas: {currentReadings.Count}");
            Sensors.Clear();
            foreach (var sensor in currentReadings)
            {
                Sensors.Add(sensor);
            }
        }
        public List<SensorInfo> GetUpdatedSensorReadings()
        {
            List<SensorInfo> sensors = new List<SensorInfo>();
            if (computer == null)
            {
                return sensors;
            }

            foreach (IHardware hardware in computer.Hardware)
            {
                hardware.Update();
                sensors.AddRange(GetSensorsFromHardware(hardware, hardware.Name));

                foreach (IHardware subHardware in hardware.SubHardware)
                {
                    subHardware.Update();
                    sensors.AddRange(GetSensorsFromHardware(subHardware, $"{hardware.Name}"));

                }
            }
            return sensors;
        }

        private IEnumerable<SensorInfo> GetSensorsFromHardware(IHardware hardware, string displayName)
        {
            foreach (ISensor sensor in hardware.Sensors.OrderBy(s => s.SensorType))
            {
                if (sensor.Value.HasValue)
                {
                    yield return new SensorInfo
                    {
                        HardwareName = displayName,
                        SensorName = sensor.Name,
                        SensorType = sensor.SensorType.ToString(),
                        Identifier = sensor.Identifier.ToString(),
                        Value = sensor.Value.Value
                    };
                }
            }
        }
    }
}