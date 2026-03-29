using Aquila.Models;
using Aquila.Services;
using LibreHardwareMonitor.Hardware;
using Microsoft.Win32;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace Aquila.ViewModels.Pages
{
    // ViewModel para a página Explorer
    public partial class ExplorerGroupedHardware : ObservableObject
    {
        public string HardwareName { get; set; } = string.Empty;
        public HardwareType HardwareType { get; set; }
        public List<ExplorerGroupedSensor> SensorGroups { get; set; } = [];

        [ObservableProperty] private bool _isExpanded;
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

        private IReadOnlyList<(string Name, HardwareType Type)> _hwSignature = [];

        public async Task InitializeAsync()
        {
            var snapshot = _monitorService.ComputerData.HardwareList.ToList();

            // Skip rebuild if hardware composition hasn't changed — prevents flicker on re-navigation
            var signature = snapshot.Select(hw => (hw.Name, hw.HardwareType)).ToList();
            if (GroupedHardware.Count > 0 && _hwSignature.SequenceEqual(signature))
                return;

            _hwSignature = signature;

            // Preserve expand/collapse state across rebuilds (4.12)
            var prevExpanded = GroupedHardware
                .Where(h => h.IsExpanded)
                .Select(h => h.HardwareName)
                .ToHashSet();

            GroupedHardware = await Task.Run(() =>
                snapshot
                    .Select(hw => new ExplorerGroupedHardware
                    {
                        HardwareName = hw.Name,
                        HardwareType = hw.HardwareType,
                        IsExpanded   = prevExpanded.Contains(hw.Name),
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

        [RelayCommand]
        private static void CopyIdentifier(string identifier) => Clipboard.SetText(identifier);

        [RelayCommand]
        private async Task ExportToHtmlAsync()
        {
            var snapshot = GroupedHardware;
            if (snapshot.Count == 0)
                return;

            var dialog = new SaveFileDialog
            {
                Title = "Export Sensor Reference",
                Filter = "HTML Files (*.html)|*.html",
                FileName = $"aquila-sensors-{DateTime.Now:yyyy-MM-dd}.html",
                DefaultExt = "html"
            };

            if (dialog.ShowDialog() != true)
                return;

            var path = dialog.FileName;
            var html = await Task.Run(() => BuildHtml(snapshot));
            await File.WriteAllTextAsync(path, html);
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        }

        private static string BuildHtml(List<ExplorerGroupedHardware> hardware)
        {
            var now = DateTime.Now;
            var totalSensors = hardware.Sum(h => h.SensorGroups.Sum(g => g.Sensors.Count));
            var sb = new StringBuilder(65536);

            sb.Append("""
                <!DOCTYPE html>
                <html lang="en">
                <head>
                  <meta charset="UTF-8">
                  <meta name="viewport" content="width=device-width, initial-scale=1.0">
                  <title>Aquila &#8212; Sensor Reference</title>
                  <style>
                    *, *::before, *::after { box-sizing: border-box; margin: 0; padding: 0; }
                    body { background: #0d0d0d; color: #d0d0d0; font-family: 'Segoe UI', system-ui, sans-serif; padding: 40px 48px; line-height: 1.5; }
                    header { margin-bottom: 40px; padding-bottom: 20px; border-bottom: 1px solid #1e1e1e; }
                    header h1 { font-size: 22px; font-weight: 600; color: #fff; letter-spacing: -0.3px; }
                    header p { font-size: 13px; color: #555; margin-top: 5px; }
                    .hw { margin-bottom: 40px; }
                    .hw-header { display: flex; align-items: baseline; gap: 10px; margin-bottom: 18px; padding-bottom: 10px; border-bottom: 1px solid #1e1e1e; }
                    .hw-name { font-size: 15px; font-weight: 600; color: #fff; }
                    .hw-type { font-size: 10px; font-weight: 600; text-transform: uppercase; letter-spacing: 0.4px; background: #1a1a1a; border: 1px solid #2a2a2a; border-radius: 3px; padding: 2px 7px; color: #60cdff; }
                    .hw-count { font-size: 12px; color: #444; margin-left: auto; }
                    .grp { margin-bottom: 20px; }
                    .grp-label { font-size: 10px; font-weight: 700; text-transform: uppercase; letter-spacing: 0.6px; color: #444; margin-bottom: 6px; padding-left: 4px; }
                    table { width: 100%; border-collapse: collapse; font-size: 13px; table-layout: fixed; }
                    thead th { font-size: 10px; font-weight: 600; text-transform: uppercase; letter-spacing: 0.4px; color: #444; padding: 5px 10px; border-bottom: 1px solid #1a1a1a; text-align: left; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
                    thead th.r { text-align: right; }
                    tbody td { padding: 7px 10px; border-bottom: 1px solid #111; vertical-align: middle; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
                    tbody td.r { text-align: right; }
                    tbody tr:last-child td { border-bottom: none; }
                    tbody tr:hover td { background: #111; }
                    .n { color: #d0d0d0; }
                    .id { font-family: 'Cascadia Code', Consolas, monospace; font-size: 11px; color: #666; }
                    .v { font-weight: 600; color: #60cdff; }
                    .mn, .mx { color: #888; }
                    .u { color: #555; font-size: 11px; }
                  </style>
                </head>
                <body>
                """);

            sb.AppendLine("  <header>");
            sb.AppendLine("    <h1>Aquila &#8212; Sensor Reference</h1>");
            sb.AppendLine($"    <p>Generated {now:yyyy-MM-dd HH:mm:ss} &nbsp;&middot;&nbsp; {totalSensors} sensors &middot;&nbsp; {hardware.Count} hardware components</p>");
            sb.AppendLine("  </header>");

            foreach (var hw in hardware)
            {
                var hwCount = hw.SensorGroups.Sum(g => g.Sensors.Count);
                sb.AppendLine($"  <section class=\"hw\">");
                sb.AppendLine($"    <div class=\"hw-header\">");
                sb.AppendLine($"      <span class=\"hw-name\">{WebUtility.HtmlEncode(hw.HardwareName)}</span>");
                sb.AppendLine($"      <span class=\"hw-type\">{WebUtility.HtmlEncode(hw.HardwareType.ToString())}</span>");
                sb.AppendLine($"      <span class=\"hw-count\">{hwCount} sensors</span>");
                sb.AppendLine("    </div>");

                foreach (var grp in hw.SensorGroups)
                {
                    sb.AppendLine($"    <div class=\"grp\">");
                    sb.AppendLine($"      <div class=\"grp-label\">{WebUtility.HtmlEncode(grp.CategoryName)}</div>");
                    sb.AppendLine("      <table>");
                    sb.AppendLine("        <colgroup><col style=\"width:30%\"><col style=\"width:38%\"><col style=\"width:9%\"><col style=\"width:9%\"><col style=\"width:9%\"><col style=\"width:5%\"></colgroup>");
                    sb.AppendLine("        <thead><tr><th>Name</th><th>Identifier</th><th class=\"r\">Value</th><th class=\"r\">Min</th><th class=\"r\">Max</th><th class=\"r\">Unit</th></tr></thead>");
                    sb.AppendLine("        <tbody>");

                    foreach (var s in grp.Sensors)
                        sb.AppendLine($"          <tr><td class=\"n\">{WebUtility.HtmlEncode(s.Name)}</td><td class=\"id\">{WebUtility.HtmlEncode(s.Identifier)}</td><td class=\"v r\">{s.Value:F1}</td><td class=\"mn r\">{s.Min:F1}</td><td class=\"mx r\">{s.Max:F1}</td><td class=\"u r\">{WebUtility.HtmlEncode(s.Unit ?? string.Empty)}</td></tr>");

                    sb.AppendLine("        </tbody></table>");
                    sb.AppendLine("    </div>");
                }

                sb.AppendLine("  </section>");
            }

            sb.AppendLine("</body>");
            sb.AppendLine("</html>");
            return sb.ToString();
        }
    }
}