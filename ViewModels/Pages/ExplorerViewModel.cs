using Aquila.Models;
using Aquila.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Aquila.ViewModels.Pages
{
    // ViewModel para a página Explorer
    public class ExplorerGroupedHardware
    {
        public string HardwareName { get; set; } = string.Empty;
        public List<ExplorerGroupedSensor> SensorGroups { get; set; } = [];
    }

    public class ExplorerGroupedSensor
    {
        public string CategoryName { get; set; } = string.Empty;
        public List<DataSensor> Sensors { get; set; } = [];
    }

    public partial class ExplorerViewModel : ObservableObject
    {
        private readonly HardwareMonitorService _monitorService;
        private readonly UiService _uiService;

        [ObservableProperty]
        private List<ExplorerGroupedHardware> _groupedHardware = [];

        public ExplorerViewModel(HardwareMonitorService monitorService, UiService uiService)
        {
            _monitorService = monitorService;
            _uiService = uiService;
        }

        public async Task InitializeAsync()
        {
            _uiService.IsLoading = true;
            try
            {
                // Garante um tempo mínimo para a animação do loading
                var delayTask = Task.Delay(500);

                var processingTask = Task.Run(() =>
                {
                    // Transforma a lista "achatada" do serviço na estrutura agrupada que a View precisa
                    return _monitorService.ComputerData.HardwareList
                        .Select(hw => new ExplorerGroupedHardware
                        {
                            HardwareName = hw.Name,
                            SensorGroups = hw.Sensors
                                .GroupBy(sensor => sensor.SensorType)
                                .Select(group => new ExplorerGroupedSensor
                                {
                                    CategoryName = group.Key.ToString(),
                                    Sensors = group.OrderBy(s => s.Name).ToList()
                                })
                                .OrderBy(g => g.CategoryName)
                                .ToList()
                        })
                        .ToList();
                });

                await Task.WhenAll(processingTask, delayTask);
                GroupedHardware = await processingTask;
            }
            finally
            {
                _uiService.IsLoading = false;
            }
        }
    }
}