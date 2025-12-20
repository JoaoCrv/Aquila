using Aquila.Models;
using Aquila.Services;
using CommunityToolkit.Mvvm.ComponentModel;
namespace Aquila.ViewModels.Pages
{

    public partial class DashboardViewModel : ObservableObject
    {
        private readonly HardwareMonitorService _monitorService;

        [ObservableProperty]
        private ComputerData _computer; 
        public DashboardViewModel(HardwareMonitorService monitorService)
        {
            _monitorService = monitorService;
            _computer = _monitorService.ComputerData;
        }
    }
}