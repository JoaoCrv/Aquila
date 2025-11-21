using Aquila.Models;
using Aquila.Services.Utilities;
using System.Collections.ObjectModel;
using System.Windows.Threading;

namespace Aquila.ViewModels
{
    internal class MainWindowViewModel : ViewModelBase
    {
        private readonly DispatcherTimer _timer;
        private readonly HardwareMonitorService _monitor;

        public ObservableCollection<SensorInfo> Sensors { get; set; }

        public MainWindowViewModel()
        {
            Sensors = new ObservableCollection<SensorInfo>();
            _monitor = new HardwareMonitorService();
            _monitor.StartMonitoring();
            _timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _timer.Tick += UpdateHardwareInfo;
            _timer.Start();
        }

    private void UpdateHardwareInfo(object? sender, EventArgs e)
        {
            var newSensors = _monitor.GetUpdatedSensorReadings();
            Sensors.Clear();
            foreach (var sensor in newSensors)
            {
                Sensors.Add(sensor);
            }
        }
    }
}
