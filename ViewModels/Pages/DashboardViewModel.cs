using Aquila.Services;
using Aquila.ViewModels.Windows;
using System.Collections.ObjectModel;
using System.Windows.Threading;
using Aquila.Models;

namespace Aquila.ViewModels.Pages
{
    public class DashboardViewModel : ViewModelBase
    {
       
        private readonly HardwareMonitorService _hardwareMonitorService;

        public ObservableCollection<SensorInfo> Sensors =>_hardwareMonitorService.Sensors;

        // HardwareMonitorService is injected via dependency injection
        public DashboardViewModel( HardwareMonitorService monitor)
        {
           
            _hardwareMonitorService = monitor;

        }

    }
}
