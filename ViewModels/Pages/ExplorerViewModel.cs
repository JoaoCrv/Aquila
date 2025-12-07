using Aquila.Models;
using Aquila.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.Generic;
using System.Linq;

namespace Aquila.ViewModels.Pages
{
    public partial class ExplorerViewModel : ObservableObject
    {
        private readonly HardwareMonitorService _monitor;

        // A View vai fazer o binding a esta nova propriedade.
        public List<HardwareGroupViewModel> GroupedHardware { get; }

        public ExplorerViewModel(HardwareMonitorService monitor)
        {
            _monitor = monitor;

            // A LÓGICA DE TRANSFORMAÇÃO
            // Usamos LINQ para converter o Dicionário do serviço na nossa estrutura agrupada.
            GroupedHardware = [.. _monitor.Hardware.Values
                .Select(hw => new HardwareGroupViewModel
                {
                    HardwareName = hw.Name,
                    SensorGroups = [.. hw.Sensors.Values
                                       .GroupBy(sensor => sensor.SensorType) // 1. Agrupa os sensores pelo seu tipo
                                       .Select(group => new SensorGroupViewModel
                                       {
                                           CategoryName = group.Key.ToString(), // 2. O nome do grupo é o tipo (ex: "Temperature")
                                           Sensors = [.. group.OrderBy(s => s.Name)] // 3. A lista de sensores para esse grupo
                                       })
                                       .OrderBy(g => g.CategoryName)]
                })];
        }
    }
}