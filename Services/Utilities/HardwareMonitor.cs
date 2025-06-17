using LibreHardwareMonitor.Hardware;
using System;
using System.Windows.Automation;


/// <summary>
/// Class to deal with hardware monitoring using LibreHardwareMonitor library.
/// </summary>
/// 
namespace Aquila.Services.Utilities
{
    public class HardwareMonitorService
    {
        private Computer computer = new Computer();

        public void MonitorHardwareService(bool enableCpu = true, bool enableGpu=true, bool enableMemory=true, bool enableMotherboard=true, bool enableController=true, bool enableNetwork=true, bool enableStorage=true)
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
        }

        public void StartMonitoring(bool enableCpu, bool enableGpu, bool enableMemory, bool enableMotherboard, bool enableController, bool enableNetwork, bool enableStorage)
        {
            // Initialize monitoring for CPU, GPU, Memory, etc.
            // This method should set up the necessary hooks or listeners
            // to monitor the hardware components based on the enabled flags.
            try
            {
                computer.Open();
            }
            catch (Exception ex)
            {
                // Handle any exceptions that may occur during initialization
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
                    computer.Hardware.Clear();
                    computer.Close();
                }
                catch (Exception ex)
                {
                    // Handle any exceptions that may occur during cleanup
                    Console.WriteLine($"Error during hardware cleanup: {ex.Message}");
                }
            }
        }
    }
}