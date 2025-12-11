using LibreHardwareMonitor.Hardware;
using System;
using System.Linq;
using System.Windows.Threading;

namespace Aquila.Services
{
    // The UpdateVisitor class is a helper for LHM and remains unchanged.
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


    ///<summary>
    ///"Driver" service at a low level. Its sole responsibility is to read raw data from LibreHardwareMonitor and trigger an event.
    /// </summary>
    public class HardwareMonitorService
    {
        private Computer? _computer;
        private readonly DispatcherTimer _timer;

        /// <summary>
        /// Expose the collection of raw data read directly from the library.
        /// </summary>
        /// 
        public IHardware[] RawHardware { get; private set; } = [];

        /// <summary>
        /// Event triggered every second, after reading new data.
        /// </summary>
        public event Action? DataUpdated;

        public HardwareMonitorService()
        {
            _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
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
                System.Diagnostics.Debug.WriteLine($"[HardwareMonitorService] Failed to start: {ex}");
            }
        }

        private void UpdateSensorReadings(object? sender, EventArgs e)
        {
            if (_computer == null) return;

            _computer.Accept(new UpdateVisitor());

            //Update the property with the latest data
            RawHardware = (IHardware[])_computer.Hardware;

            //Notify subscribers (like HardwareApiService) that new data is available
            DataUpdated?.Invoke();
        }
    }
}