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

        public Dictionary<string, HardwareModel> Hardware { get; } = [];

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
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[HardwareMonitorService] Failed to start monitoring: {ex}");
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
                ProcessHardware(hw, hw.Name);
                foreach (IHardware subHw in hw.SubHardware)
                {
                    ProcessHardware(subHw, hw.Name);
                }
            }
        }

        private void ProcessHardware(IHardware hw, string parentHardwareName)
        {
            // We try to obtain the hardware model from our dictionary
            //the variable 'hardwareModel'  will contain the model if it exists

            if (!Hardware.TryGetValue(parentHardwareName, out var hardwareModel)) {

                //If it does not exist, we create a new one
                hardwareModel = new HardwareModel { Name = parentHardwareName };

                //And then we add the new model to our Dictionary
                Hardware[parentHardwareName] = hardwareModel;
            }

            // At this point, we are sure that the 'hardwareModel' contains the correct model, and if not, we created a new one


            // now we process the sensors of the hardware
            foreach (ISensor sensor in hw.Sensors)
            {
                var sensorId = sensor.Identifier.ToString();

                //  check if we already have a model for this sepecific sensor
                if (!hardwareModel.Sensors.TryGetValue(sensorId, out var sensorModel))
                {
                    // if not, we create a new sensor model
                    sensorModel = new SensorModel
                    {
                        Name = sensor.Name,
                        Identifier = sensorId,
                        Unit = GetSensorUnit(sensor.SensorType),
                        SensorType = sensor.SensorType
                    };
                    // E adicionamo-lo ao dicionário de sensores do nosso hardware.
                    hardwareModel.Sensors[sensorId] = sensorModel;
                }

                // Finalmente, atualizamos o valor do sensor.
                // Isto acontece quer o sensor seja novo ou já existente.
                sensorModel.Value = sensor.Value ?? 0;
            }
        }

        private static string GetSensorUnit(SensorType type)
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