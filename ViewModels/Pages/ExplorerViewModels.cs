using Aquila.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Aquila.ViewModels.Pages
{
    public class SensorGroupViewModel
    {
        public string CategoryName { get; set; } = string.Empty;
        public IEnumerable<SensorModel> Sensors { get; set; } = [];


    }

    public class HardwareGroupViewModel
    {
        public string HardwareName { get; set; } = string.Empty;
        public IEnumerable<SensorGroupViewModel> SensorGroups { get; set; } = [];
    }
}
