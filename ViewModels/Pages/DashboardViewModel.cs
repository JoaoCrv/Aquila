using Aquila.Services;
using Aquila.ViewModels.Windows;
using System.Collections.ObjectModel;
using System.Windows.Threading;
using Aquila.Models;

namespace Aquila.ViewModels.Pages
{
    public class DashboardViewModel : ViewModelBase
    {
        private readonly DispatcherTimer _timer;
        private readonly HardwareMonitorService _hardwareMonitorService;

        public ObservableCollection<SensorInfo> Sensors { get; }

        // HardwareMonitorService is injected via dependency injection
        public DashboardViewModel( HardwareMonitorService monitor)
        {
            Sensors = new ObservableCollection<SensorInfo>();
            _hardwareMonitorService = monitor;

            //This only needs to be started once.
            //Later, we can optimize this so it wont start multiple times if the user navigates away and back.
            _hardwareMonitorService.StartMonitoring();

            _timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _timer.Tick += UpdateHardwareInfo;
            _timer.Start();

            // Initial load to avoid waiting for the first tick
            UpdateHardwareInfo(null, EventArgs.Empty);
        }

        // Search for updated hardware info and update the Sensors collection
        private void UpdateHardwareInfo(object? sender, EventArgs e)
        {
            var newSensors = _hardwareMonitorService.GetUpdatedSensorReadings();
            //Not the most eficient way to update, but good enough for now.
            //In the future we can implement a diffing algorithm to only update changed sensors.
            Sensors.Clear();
            foreach (var sensor in newSensors)
            {
                Sensors.Add(sensor);
            }
        }
    }
}
