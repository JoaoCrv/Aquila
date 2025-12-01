using Aquila.Models;
using LibreHardwareMonitor.Hardware;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Collections.ObjectModel;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Automation;
using System.Windows.Threading;
using System.Xml.Linq;


/// <summary>
/// Class to deal with hardware monitoring using LibreHardwareMonitor library.
/// </summary>
/// 



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
    ///     Singleton service to monitor hardware components and retrieve sensor data.
    ///     It acts like the only source of truth for hardware information in the application.
    /// </summary>
    public class HardwareMonitorService
    {

        private Computer? _computer;
        private readonly DispatcherTimer _timer;

        public Dictionary<string, HardwareModel> Hardware { get; } = new();

        private bool isInitialScanComplete = false;

        public HardwareMonitorService()
        {
            _timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _timer.Tick += UpdateSensorReadings;
        }

        public void StartMonitoring()
        {
            if (_computer != null) return;

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
                // _computer.Accept(new UpdateVisitor());
                //UpdateSensorReadings(null, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[HardwareMonitorService] ERRO CRÍTICO ao iniciar: {ex.ToString()}");
            }
        }

        private void UpdateSensorReadings(object? sender, EventArgs e)
        {
            if (_computer == null) return;

            // Asks LibreHardwareMonitor to read new values from hardware
            _computer.Accept(new UpdateVisitor());

            //Process all detected hardware components
            foreach (IHardware hw in _computer.Hardware)
            {
                ProcessHardware(hw);
                foreach (IHardware subHw in hw.SubHardware)
                {
                    ProcessHardware(subHw);
                }
            }

            _isInitialScanComplete = true;
        }

        private void ProcessHardware(IHardware hw)
        {
            //if it's not in the Dictionary its the first time we see it

            if (!_isInitialScanComplete && !Hardware.ContainsKey(hw.Name)) {

                //Create a new HardwareModel for it
                var hardwareModel = new HardwareModel(Name = hw.Name);

                foreach (ISensor sensor in hw.Sensors.OrderBy(s => s.SensorType).ThenBy(s => s.Name))
                {
                    var sensorModel = new SensorModel
                    {
                        Name = sensor.Name,
                        Identifier = sensor.Identifier.ToString(),
                        Value = sensor.Value ?? 0,
                        Unit = GetSensorUnit(sensor.SensorType)
                    };
                    hardwareModel.Sensors[sensorModel[sensorModel.Identifier] = sensorModel;
                }
                Hardware[hw.Name] = hardwareModel;
            }
            else if (Hardware.TryGetValue(hw.Name, out var hardwareModel))
            {
                foreach (ISensor sensor in hw.Sensors)
                {
                    var sensorId = sensor.Identifier.ToString();
                    if (hardwareModel.Sensors.TryGetValue(sensorId, out var sensorModel))
                    {
                        // the Ui is automatically notified
                        sensorModel.Value = sensor.Value ?? 0;
                    }
                }
            }
        }

        private string GetSensorUnit(SensorType type)
        {
            return type switch
            {
                SensorType.Temperature => "°C",
                SensorType.Load => "%",
                SensorType.Clock => "MHz",
                SensorType.Power => "W",
                SensorType.Fan => "RPM",
                SensorType.Data => "GB",
                SensorType.SmallData => "MB",
                SensorType.Throughput => "MB/s",
                SensorType.Voltage => "V",
                SensorType.Frequency => "Hz",
                SensorType.Control => "%",
                _ => string.Empty
            };
        }
    }
}





        /*
        //public ObservableCollection<SensorInfo> Sensors { get; } = new();

        //public HardwareMonitorService()
        //{
        //    _timer = new DispatcherTimer
        //    {
        //        Interval = TimeSpan.FromSeconds(1)
        //    };
        //    _timer.Tick += UpdateSensorReadings;

        //}

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
}*/