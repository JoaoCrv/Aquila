using LibreHardwareMonitor.Hardware;
using System;

namespace Aquila.Services.Providers
{
    public class LhmProvider : IDisposable
    {
        private readonly Computer _computer;
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

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _computer.Close();
        }
    }
}
