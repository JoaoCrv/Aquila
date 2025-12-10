using Aquila.Models;
using Aquila.Services;
using Aquila.ViewModels.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Windows.Threading;

namespace Aquila.ViewModels.Pages
{
    public partial class DashboardViewModel(HardwareMonitorService monitor) : ObservableObject
    {
       
        private readonly HardwareMonitorService _monitor = monitor;

        public HardwareModel? CPU =>
        _monitor.Hardware.Values.FirstOrDefault(h =>
            h.Name.Contains("CPU"));

        public HardwareModel? GPU =>
       _monitor.Hardware.Values.FirstOrDefault(h =>
           h.Name.Contains("GPU"));

    }

}
