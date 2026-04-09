using Aquila.Models.Api;
using Aquila.Services;
using Microsoft.Win32;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Windows;
using System.Reflection;
using System.Collections;

namespace Aquila.ViewModels.Pages
{
    public partial class ExplorerGroupedHardware : ObservableObject
    {
        public string HardwareName { get; set; } = string.Empty;
        public string HardwareType { get; set; } = string.Empty;
        public List<ExplorerGroupedSensor> SensorGroups { get; set; } = [];

        [ObservableProperty] private bool _isExpanded;
    }

    public class ExplorerGroupedSensor
    {
        public string CategoryName { get; set; } = "Sensors";
        public List<SensorNode> Sensors { get; set; } = [];
    }

    public partial class ExplorerViewModel : ObservableObject
    {
        private readonly AquilaService _aquila;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(FilteredHardware))]
        private List<ExplorerGroupedHardware> _groupedHardware = [];

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(FilteredHardware))]
        private string _searchText = string.Empty;

        public IEnumerable<ExplorerGroupedHardware> FilteredHardware
        {
            get
            {
                if (string.IsNullOrWhiteSpace(SearchText))
                    return GroupedHardware;

                return GroupedHardware
                    .Select(hw => new ExplorerGroupedHardware
                    {
                        HardwareName = hw.HardwareName,
                        HardwareType = hw.HardwareType,
                        IsExpanded = true,
                        SensorGroups = hw.SensorGroups
                            .Select(g => new ExplorerGroupedSensor
                            {
                                CategoryName = g.CategoryName,
                                Sensors = g.Sensors
                                    .Where(s => s.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase))
                                    .ToList()
                            })
                            .Where(g => g.Sensors.Count > 0)
                            .ToList()
                    })
                    .Where(hw => hw.SensorGroups.Count > 0);
            }
        }

        public ExplorerViewModel(AquilaService aquila)
        {
            _aquila = aquila;
        }

        public Task InitializeAsync()
        {
            var hw = _aquila.State.Hardware;
            var list = new List<ExplorerGroupedHardware>
            {
                MapNode(hw.Motherboard, hw.Motherboard.Name, "Motherboard"),
                MapNode(hw.Cpu, hw.Cpu.Name, "Cpu"),
                MapNode(hw.Memory, hw.Memory.Name, "Memory")
            };

            foreach (var gpu in hw.Gpus) list.Add(MapNode(gpu, gpu.Name, "Gpu"));
            foreach (var drv in hw.Drives) list.Add(MapNode(drv, drv.Name, "Storage"));
            foreach (var net in hw.NetworkAdapters) list.Add(MapNode(net, net.Name, "Network"));

            GroupedHardware = list.Where(l => l.SensorGroups.Count > 0).ToList();
            return Task.CompletedTask;
        }

        private static ExplorerGroupedHardware MapNode(object node, string name, string type)
        {
            var category = new ExplorerGroupedSensor { CategoryName = "Metrics" };
            
            foreach (var prop in node.GetType().GetProperties())
            {
                if (prop.PropertyType == typeof(SensorNode) || prop.PropertyType.IsSubclassOf(typeof(SensorNode)))
                {
                    if (prop.GetValue(node) is SensorNode s && s.Value.HasValue)
                        category.Sensors.Add(s);
                }
                else if (typeof(IEnumerable).IsAssignableFrom(prop.PropertyType) && prop.PropertyType.IsGenericType)
                {
                    var genericArg = prop.PropertyType.GetGenericArguments()[0];
                    if (genericArg == typeof(SensorNode) || genericArg.IsSubclassOf(typeof(SensorNode)))
                    {
                        if (prop.GetValue(node) is IEnumerable collection)
                        {
                            foreach (var item in collection)
                            {
                                if (item is SensorNode s && s.Value.HasValue)
                                    category.Sensors.Add(s);
                            }
                        }
                    }
                }
            }

            return new ExplorerGroupedHardware 
            { 
                HardwareName = string.IsNullOrEmpty(name) ? type : name, 
                HardwareType = type, 
                SensorGroups = new List<ExplorerGroupedSensor> { category } 
            };
        }

        [RelayCommand]
        private static void CopyIdentifier(string name) => Clipboard.SetText(name);

        [RelayCommand]
        private Task ExportToHtmlAsync() => Task.CompletedTask;
    }
}