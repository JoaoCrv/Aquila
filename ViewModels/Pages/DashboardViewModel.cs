using Aquila.Models;
using Aquila.Services;
using Aquila.ViewModels.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;
using System.Threading;
using System.Windows.Threading;

namespace Aquila.ViewModels.Pages
{
    public class DashboardViewModel : ObservableObject
    {
       
        private readonly HardwareMonitorService _hardwareMonitorService;

        public Dictionary<string, HardwareModel> Hardware => _hardwareMonitorService.Hardware;

        // HardwareMonitorService is injected via dependency injection
        public DashboardViewModel( HardwareMonitorService hardwareMonitorService)
        {
           
            _hardwareMonitorService = hardwareMonitorService;

        }

    }
}
