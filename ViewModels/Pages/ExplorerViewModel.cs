using Aquila.Models.Api;
using Aquila.Services;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace Aquila.ViewModels.Pages
{
    public partial class ExplorerTreeItem : ObservableObject
    {
        public string Title { get; init; } = string.Empty;
        public string Subtitle { get; init; } = string.Empty;
        public string Identifier { get; init; } = string.Empty;
        public bool IsSensor { get; init; }
        public List<ExplorerTreeItem> Children { get; init; } = new();

        [ObservableProperty] private bool _isExpanded = true;
    }

    public partial class ExplorerViewModel(UiService uiService, AquilaService aquilaService) : ObservableObject
    {
        private readonly UiService _uiService = uiService;
        private readonly AquilaService _aquilaService = aquilaService;
        private Dictionary<string, string> _reportSections = new();

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(FilteredItems))]
        private List<ExplorerTreeItem> _items = new();

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(FilteredItems))]
        private string _searchText = string.Empty;

        public IEnumerable<ExplorerTreeItem> FilteredItems =>
            string.IsNullOrWhiteSpace(SearchText)
                ? Items
                : FilterItems(Items, SearchText.Trim());

        [ObservableProperty] private List<string> _reportSectionKeys = new();

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(SelectedReportText))]
        private string _selectedReportSection = string.Empty;

        [ObservableProperty] private string _selectedReportText = string.Empty;





        private static IEnumerable<ExplorerTreeItem> FilterItems(IEnumerable<ExplorerTreeItem> items, string term)
        {
            foreach (var item in items)
            {
                var itemMatches = MatchesItem(item, term);
                var matchingChildren = FilterItems(item.Children, term).ToList();

                if (!itemMatches && matchingChildren.Count == 0)
                    continue;

                yield return new ExplorerTreeItem
                {
                    Title = item.Title,
                    Subtitle = item.Subtitle,
                    Identifier = item.Identifier,
                    IsSensor = item.IsSensor,
                    Children = itemMatches ? item.Children : matchingChildren,
                    IsExpanded = true
                };
            }
        }

        private static bool MatchesItem(ExplorerTreeItem item, string term) =>
            item.Title.Contains(term, StringComparison.OrdinalIgnoreCase) ||
            item.Subtitle.Contains(term, StringComparison.OrdinalIgnoreCase) ||
            item.Identifier.Contains(term, StringComparison.OrdinalIgnoreCase);

        [RelayCommand]
        private static void CopyIdentifier(string? identifier)
        {
            if (!string.IsNullOrWhiteSpace(identifier))
                Clipboard.SetText(identifier);
        }

        partial void OnSelectedReportSectionChanged(string value)
        {
            SelectedReportText = string.IsNullOrEmpty(value) ? string.Empty : (_reportSections.TryGetValue(value, out var v) ? v : string.Empty);
        }

        public Task InitializeAsync()
        {
            Items = BuildItems();

            _reportSections = new Dictionary<string, string>
            {
                ["Raw Tree Summary"] = BuildDebugReport()
            };

            ReportSectionKeys = _reportSections.Keys.ToList();
            SelectedReportSection = ReportSectionKeys.FirstOrDefault() ?? string.Empty;

            return Task.CompletedTask;
        }

        private List<ExplorerTreeItem> BuildItems()
        {
            var hardware = _aquilaService.State.Hardware;
            var items = new List<ExplorerTreeItem>
            {
                CreateHardwareItem("Motherboard", hardware.Motherboard.Name, hardware.Motherboard),
                CreateHardwareItem("CPU", hardware.Cpu.Name, hardware.Cpu),
                CreateHardwareItem("Memory", hardware.Memory.Name, hardware.Memory)
            };

            items.AddRange(hardware.Gpus.Select(gpu => CreateHardwareItem("GPU", gpu.Name, gpu)));
            items.AddRange(hardware.Drives.Select(drive => CreateHardwareItem("Storage", drive.Name, drive)));
            items.AddRange(hardware.NetworkAdapters.Select(adapter => CreateHardwareItem("Network", adapter.Name, adapter)));

            return items.Where(item => item.Children.Count > 0 || !string.IsNullOrWhiteSpace(item.Subtitle)).ToList();
        }

        private static ExplorerTreeItem CreateHardwareItem(string type, string name, BaseHardwareNode node)
        {
            var children = new List<ExplorerTreeItem>();

            AddGroup(children, "Temperatures", node.Temperatures);
            AddGroup(children, "Loads", node.Loads);
            AddGroup(children, "Clocks", node.Clocks);
            AddGroup(children, "Powers", node.Powers);
            AddGroup(children, "Voltages", node.Voltages);
            AddGroup(children, "Data", node.Data);
            AddGroup(children, "Throughput", node.Throughput);
            AddGroup(children, "Controls", node.Controls);
            AddGroup(children, "Fans", node.Fans);

            return new ExplorerTreeItem
            {
                Title = string.IsNullOrWhiteSpace(name) ? type : name,
                Subtitle = type,
                Children = children,
                IsExpanded = true
            };
        }

        private static void AddGroup(List<ExplorerTreeItem> items, string title, IEnumerable<SensorNode> sensors)
        {
            var sensorChildren = sensors
                .Where(sensor => sensor.Value.HasValue || sensor.Min.HasValue || sensor.Max.HasValue)
                .Select(sensor => new ExplorerTreeItem
                {
                    Title = sensor.Name,
                    Subtitle = FormatSensorValue(sensor),
                    Identifier = sensor.Identifier,
                    IsSensor = true
                })
                .ToList();

            if (sensorChildren.Count == 0)
                return;

            items.Add(new ExplorerTreeItem
            {
                Title = title,
                Subtitle = $"{sensorChildren.Count} sensors",
                Children = sensorChildren,
                IsExpanded = true
            });
        }

        private static string FormatSensorValue(SensorNode sensor)
        {
            var unit = string.IsNullOrWhiteSpace(sensor.Unit) ? "" : $" {sensor.Unit}";
            var value = sensor.Value.HasValue ? $"{sensor.Value.Value:F1}{unit}" : "--";
            var min = sensor.Min.HasValue ? $"{sensor.Min.Value:F1}{unit}" : "--";
            var max = sensor.Max.HasValue ? $"{sensor.Max.Value:F1}{unit}" : "--";

            return $"{value} | Min: {min} | Max: {max}";
        }

        [RelayCommand]
        private async Task ExportReportAsync()
        {
            try
            {
                _uiService.IsLoading = true;

                var report = BuildDebugReport();
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

        private string BuildDebugReport()
        {
            if (Items.Count == 0)
                return "No hardware items available.";

            var builder = new StringBuilder();
            foreach (var item in Items)
            {
                AppendItemReport(builder, item, 0);
            }

            return builder.ToString();
        }

        private static void AppendItemReport(StringBuilder builder, ExplorerTreeItem item, int depth)
        {
            var indent = new string(' ', depth * 2);
            builder.AppendLine($"{indent}- {item.Title}" + (string.IsNullOrWhiteSpace(item.Subtitle) ? string.Empty : $" [{item.Subtitle}]"));

            if (!string.IsNullOrWhiteSpace(item.Identifier))
                builder.AppendLine($"{indent}  id: {item.Identifier}");

            foreach (var child in item.Children)
            {
                AppendItemReport(builder, child, depth + 1);
            }
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