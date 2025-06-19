using LibreHardwareMonitor.Hardware;
using System;
using System.Text;
using System.Windows.Automation;


/// <summary>
/// Class to deal with hardware monitoring using LibreHardwareMonitor library.
/// </summary>
/// 
namespace Aquila.Services.Utilities
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

        private Computer computer = new Computer();


        // Initialize monitoring for CPU, GPU, Memory, etc.
        // This method should set up the necessary hooks or listeners
        // to monitor the hardware components based on the enabled flags.
        public void StartMonitoring(bool enableCpu = true, bool enableGpu = true, bool enableMemory = true, bool enableMotherboard = true, bool enableController = true, bool enableNetwork = true, bool enableStorage = true)
        {
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
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error initializing hardware monitoring: {ex.Message}");
                return;
            }
        }

        public void UpdateSensors()
        {
            // computer.Hardware.Update();
        }
        public void StopMonitoring()
        {
            // Stop monitoring hardware components and release resources
            if (computer != null)
            {
                try
                {
                    computer.Hardware.Clear();
                    computer.Close();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error during hardware cleanup: {ex.Message}");
                }
            }
        }

        public string listSensors()
        {
            StringBuilder sbHardwareList = new StringBuilder();
            foreach (IHardware hardware in computer.Hardware)
            {
                sbHardwareList.AppendLine("Hardware: " + hardware.Name);
                if (hardware.SubHardware.Length > 0)
                {
                    foreach (IHardware subhardware in hardware.SubHardware)
                    {
                        sbHardwareList.AppendLine("\tSubhardware: " + subhardware.Name);
                        if (subhardware.Sensors.Length > 0)
                        {
                            foreach (ISensor sensor in subhardware.Sensors)
                            {
                                sbHardwareList.Append(sensor.SensorType + ": ");
                                sbHardwareList.AppendLine($"\t\tSensor: {sensor.Name}, value: {sensor.Value}");
                            }
                        }
                        else
                        {
                            sbHardwareList.AppendLine("\tNo sensors available.\n");
                        }
                    }
                }

                if (hardware.Sensors.Length > 0)
                {
                    foreach (ISensor sensor in hardware.Sensors.OrderBy(s => s.SensorType).ToList())
                    {
                        sbHardwareList.Append(sensor.SensorType + ": ");
                        sbHardwareList.AppendLine($"\t\tTipo: {sensor.SensorType}, Index: {sensor.Index}, Indentifier: {sensor.Identifier}, Sensor: {sensor.Name}, value: {sensor.Value}");
                    }
                }
                else
                {
                    sbHardwareList.AppendLine("\tNo sensors available.\n");
                }
            }
            return sbHardwareList.ToString();
        }

    }
}