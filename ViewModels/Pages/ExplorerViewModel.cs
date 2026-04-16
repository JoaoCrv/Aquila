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
            if (_aquilaService.Computer is not { } computer)
            {
                GroupedHardware = [];
                return Task.CompletedTask;
            }

            var list = new List<ExplorerGroupedHardware>();
            foreach (var hardware in computer.Hardware)
            {
                AddHardwareRecursive(list, hardware, parentPath: null);
            }

            GroupedHardware = list.Where(item => item.SensorGroups.Count > 0).ToList();
            return Task.CompletedTask;
        }

        private static void AddHardwareRecursive(List<ExplorerGroupedHardware> target, IHardware hardware, string? parentPath)
        {
            var displayName = string.IsNullOrWhiteSpace(parentPath)
                ? hardware.Name
                : $"{parentPath} / {hardware.Name}";

            var groups = hardware.Sensors
                .Where(sensor => sensor.Value.HasValue || sensor.Min.HasValue || sensor.Max.HasValue)
                .GroupBy(sensor => sensor.SensorType)
                .Select(group => new ExplorerGroupedSensor
                {
                    CategoryName = GetCategoryName(group.Key),
                    Sensors = group.Select(MapSensor).OrderBy(sensor => sensor.Name).ToList(),
                })
                .OrderBy(group => group.CategoryName)
                .ToList();

            target.Add(new ExplorerGroupedHardware
            {
                HardwareName = string.IsNullOrWhiteSpace(displayName) ? hardware.HardwareType.ToString() : displayName,
                HardwareType = hardware.HardwareType,
                SensorGroups = groups,
                IsExpanded = false,
            });

            foreach (var subHardware in hardware.SubHardware)
            {
                AddHardwareRecursive(target, subHardware, displayName);
            }
        }

        private static SensorNode MapSensor(ISensor sensor)
        {
            return new SensorNode(sensor.Name)
            {
                Identifier = sensor.Identifier.ToString(),
                Unit = GetUnit(sensor.SensorType),
                Value = sensor.Value,
                Min = sensor.Min,
                Max = sensor.Max,
            };
        }

        private static string GetCategoryName(SensorType sensorType)
        {
            return sensorType switch
            {
                SensorType.Temperature => "Temperatures",
                SensorType.Load => "Loads",
                SensorType.Clock => "Clocks",
                SensorType.Power => "Powers",
                SensorType.Voltage => "Voltages",
                SensorType.Data or SensorType.SmallData => "Data",
                SensorType.Throughput => "Throughput",
                SensorType.Control => "Controls",
                SensorType.Fan => "Fans",
                SensorType.Current => "Current",
                SensorType.Level => "Level",
                SensorType.Factor => "Factor",
                SensorType.TimeSpan => "TimeSpan",
                SensorType.Energy => "Energy",
                _ => sensorType.ToString(),
            };
        }

        private static string GetUnit(SensorType sensorType)
        {
            return sensorType switch
            {
                SensorType.Temperature => "°C",
                SensorType.Load or SensorType.Control or SensorType.Level => "%",
                SensorType.Clock => "MHz",
                SensorType.Power => "W",
                SensorType.Voltage => "V",
                SensorType.Current => "A",
                SensorType.Fan => "RPM",
                SensorType.Throughput => "B/s",
                SensorType.TimeSpan => "s",
                SensorType.Energy => "mWh",
                SensorType.Data => "GB",
                SensorType.SmallData => "MB",
                _ => string.Empty,
            };
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