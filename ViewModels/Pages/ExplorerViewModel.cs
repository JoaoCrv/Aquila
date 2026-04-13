using Aquila.Models.Api;
using Aquila.Services;
using LibreHardwareMonitor.Hardware;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Aquila.ViewModels.Pages
{
    public partial class ExplorerGroupedHardware : ObservableObject
    {
        public string HardwareName { get; set; } = string.Empty;
        public HardwareType HardwareType { get; set; }
        public List<ExplorerGroupedSensor> SensorGroups { get; set; } = [];

        [ObservableProperty] private bool _isExpanded;
    }

    public class ExplorerGroupedSensor
    {
        public string CategoryName { get; set; } = "Sensors";
        public List<SensorNode> Sensors { get; set; } = [];
    }

    public partial class ExplorerViewModel(UiService uiService, AquilaService aquilaService) : ObservableObject
    {
        private readonly UiService _uiService = uiService;
        private readonly AquilaService _aquilaService = aquilaService;

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

        public Task InitializeAsync()
        {
            var hw = _aquilaService.State.Hardware;
            var list = new List<ExplorerGroupedHardware>
            {
                MapNode(hw.Motherboard, hw.Motherboard.Name, HardwareType.Motherboard),
                MapNode(hw.Cpu, hw.Cpu.Name, HardwareType.Cpu),
                MapNode(hw.Memory, hw.Memory.Name, HardwareType.Memory),
            };

            foreach (var gpu in hw.Gpus)
            {
                var gpuType = Enum.TryParse<HardwareType>(gpu.Vendor, out var parsed) ? parsed : HardwareType.GpuNvidia;
                list.Add(MapNode(gpu, gpu.Name, gpuType));
            }

            foreach (var drv in hw.Drives) list.Add(MapNode(drv, drv.Name, HardwareType.Storage));
            foreach (var net in hw.NetworkAdapters) list.Add(MapNode(net, net.Name, HardwareType.Network));

            GroupedHardware = list.Where(l => l.SensorGroups.Count > 0).ToList();
            return Task.CompletedTask;
        }

        private static ExplorerGroupedHardware MapNode(BaseHardwareNode node, string name, HardwareType type)
        {
            var groups = new List<ExplorerGroupedSensor>();
            AddGroup(groups, "Temperatures", node.Temperatures);
            AddGroup(groups, "Loads", node.Loads);
            AddGroup(groups, "Clocks", node.Clocks);
            AddGroup(groups, "Powers", node.Powers);
            AddGroup(groups, "Voltages", node.Voltages);
            AddGroup(groups, "Data", node.Data);
            AddGroup(groups, "Throughput", node.Throughput);
            AddGroup(groups, "Controls", node.Controls);
            AddGroup(groups, "Fans", node.Fans);

            return new ExplorerGroupedHardware
            {
                HardwareName = string.IsNullOrEmpty(name) ? type.ToString() : name,
                HardwareType = type,
                SensorGroups = groups,
                IsExpanded = false,
            };
        }

        private static void AddGroup(List<ExplorerGroupedSensor> groups, string categoryName, IEnumerable<SensorNode> sensors)
        {
            var list = sensors.Where(s => s.Value.HasValue || s.Min.HasValue || s.Max.HasValue).ToList();
            if (list.Count > 0)
                groups.Add(new ExplorerGroupedSensor { CategoryName = categoryName, Sensors = list });
        }

        [RelayCommand]
        private static void CopyIdentifier(string? identifier)
        {
            if (!string.IsNullOrWhiteSpace(identifier))
                Clipboard.SetText(identifier);
        }

        [RelayCommand]
        private async Task ExportReportAsync()
        {
            try
            {
                _uiService.IsLoading = true;

                var report = BuildReport();
                if (string.IsNullOrWhiteSpace(report))
                    return;

                var dialog = new SaveFileDialog
                {
                    Title = "Export LibreHardwareMonitor report",
                    Filter = "Text Files (*.txt)|*.txt",
                    FileName = $"LibreHardwareMonitor.Report-{DateTime.Now:yyyy-MM-dd-HHmmss}.txt",
                    DefaultExt = "txt"
                };

                if (dialog.ShowDialog() != true)
                    return;

                await File.WriteAllTextAsync(dialog.FileName, report);
                TryOpenFile(dialog.FileName);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ExplorerViewModel] report export failed: {ex}");
                MessageBox.Show($"Failed to export the LibreHardwareMonitor report.\n\n{ex.Message}", "Aquila Explorer Export", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                _uiService.IsLoading = false;
            }
        }

        private string BuildReport()
        {
            if (GroupedHardware.Count == 0)
                return "No hardware items available.";

            var sb = new StringBuilder();
            foreach (var hw in GroupedHardware)
            {
                sb.AppendLine($"[{hw.HardwareType}] {hw.HardwareName}");
                foreach (var group in hw.SensorGroups)
                {
                    sb.AppendLine($"  {group.CategoryName}:");
                    foreach (var sensor in group.Sensors)
                    {
                        var val = sensor.Value.HasValue ? $"{sensor.Value.Value:F1} {sensor.Unit}" : "--";
                        var min = sensor.Min.HasValue ? $"{sensor.Min.Value:F1}" : "--";
                        var max = sensor.Max.HasValue ? $"{sensor.Max.Value:F1}" : "--";
                        sb.AppendLine($"    {sensor.Name}: {val}  (min: {min}, max: {max})  [{sensor.Identifier}]");
                    }
                }
                sb.AppendLine();
            }
            return sb.ToString();
        }

        private static void TryOpenFile(string path)
        {
            try
            {
                Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
            }
            catch
            {
                // ignore
            }
        }
    }
}