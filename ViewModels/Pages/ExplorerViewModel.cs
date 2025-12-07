using Aquila.Models;
using Aquila.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks; // Adiciona este using!

namespace Aquila.ViewModels.Pages
{
    public partial class ExplorerViewModel(HardwareMonitorService monitor) : ObservableObject
    {
        private readonly HardwareMonitorService _monitor = monitor;

        // A propriedade agora notifica a UI quando a sua referência muda.
        [ObservableProperty]
        private List<HardwareGroupViewModel> _groupedHardware = [];

        /// <summary>
        /// Este método carrega e processa os dados de forma assíncrona.
        /// </summary>
        public async Task InitializeAsync()
        {
            // Executa a tarefa de processamento intensiva numa thread de fundo.
            var processedData = await Task.Run(() =>
            {
                // A nossa lógica de transformação LINQ, exatamente como antes.
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

            // Depois de os dados estarem prontos, atualizamos a propriedade na thread da UI.
            GroupedHardware = processedData;
        }
    }
}