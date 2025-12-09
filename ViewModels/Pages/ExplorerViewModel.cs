using Aquila.Models;
using Aquila.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks; // Adiciona este using!

namespace Aquila.ViewModels.Pages
{
    public partial class ExplorerViewModel(HardwareMonitorService monitor, UiService uiservice) : ObservableObject
    {
        private readonly HardwareMonitorService _monitor = monitor;
        private readonly UiService _uiService = uiservice;

        [ObservableProperty]
        private List<HardwareGroupViewModel> _groupedHardware = [];

        /// <summary>
        /// this method initializes the grouped hardware data asynchronously.
        /// </summary>
        public async Task InitializeAsync()
        {
            _uiService.IsLoading = true;

            try {

                await Task.Delay(1000);
                var processingTask = Task.Run(() =>
                {
                    return _monitor.Hardware.Values
                        .Select(hw => new HardwareGroupViewModel
                        {
                            HardwareName = hw.Name,
                            SensorGroups = [.. hw.Sensors.Values
                                           .GroupBy(sensor => sensor.SensorType)
                                           .Select(group => new SensorGroupViewModel
                                           {
                                               CategoryName = group.Key.ToString(),
                                               Sensors = [.. group.OrderBy(s => s.Name)]
                                           })
                                           .OrderBy(g => g.CategoryName)]
                        })
                        .ToList();
                });
                var delayTask = Task.Delay(1000);
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