using Aquila.Models;
using Aquila.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Aquila.ViewModels.Pages
{
    public partial class ExplorerViewModel : ObservableObject
    {
        private readonly HardwareMonitorService _hardwareMonitorService;

        public ICollection<HardwareModel> Hardware => _hardwareMonitorService.Hardware.Values;

        public ExplorerViewModel(HardwareMonitorService hardwareMonitorService)
        {
            _hardwareMonitorService = hardwareMonitorService;
        }
    }
}
