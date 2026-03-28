using Aquila.Models;
using Aquila.Services;
using LibreHardwareMonitor.Hardware;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Aquila.ViewModels.Pages
{
    // ViewModel para a página Explorer
    public class ExplorerGroupedHardware
    {
        public string HardwareName { get; set; } = string.Empty;
        public HardwareType HardwareType { get; set; }
        public List<ExplorerGroupedSensor> SensorGroups { get; set; } = [];
    }

    public class ExplorerGroupedSensor
    {
        public string CategoryName { get; set; } = string.Empty;
        public List<DataSensor> Sensors { get; set; } = [];
    }

    public partial class ExplorerViewModel(HardwareMonitorService monitorService, UiService uiService) : ObservableObject
    {
        private readonly HardwareMonitorService _monitorService = monitorService;
        private readonly UiService _uiService = uiService;

        [ObservableProperty]
        private List<ExplorerGroupedHardware> _groupedHardware = [];

        public async Task InitializeAsync()
        {
            var snapshot = _monitorService.ComputerData.HardwareList.ToList();

            GroupedHardware = await Task.Run(() =>
                snapshot
                    .Select(hw => new ExplorerGroupedHardware
                    {
                        HardwareName = hw.Name,
                        HardwareType = hw.HardwareType,
                        SensorGroups = hw.Sensors
                            .ToList()
                            .GroupBy(sensor => sensor.SensorType)
                            .Select(group => new ExplorerGroupedSensor
                            {
                                CategoryName = group.Key.ToString(),
                                Sensors = group.OrderBy(s => s.Name).ToList()
                            })
                            .OrderBy(g => g.CategoryName)
                            .ToList()
                    })
                    .ToList());
        }
    }
}