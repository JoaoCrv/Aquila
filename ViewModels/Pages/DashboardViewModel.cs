using Aquila.Helpers;
using Aquila.Models;
using Aquila.Services;
using LibreHardwareMonitor.Hardware;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Threading;
using Wpf.Ui.Appearance;

namespace Aquila.ViewModels.Pages
{
    public partial class DashboardViewModel : ObservableObject, IDisposable
    {
        private readonly HardwareMonitorService _monitorService;
        private const int HistorySize = 60;

        public ComputerData Computer => _monitorService.ComputerData;

        [ObservableProperty] private float  _effectiveCpuClock;
        [ObservableProperty] private double _cpuGaugeValue;
        [ObservableProperty] private double _ramGaugeValue;
        [ObservableProperty] private float  _totalPower;

        // ── History ring buffers ─────────────────────────────────────────
        public ObservableCollection<double> CpuUsageHistory  { get; } = new(Enumerable.Repeat(0.0, HistorySize));

        // ── Chart series & axes ──────────────────────────────────────────
        public ISeries[] CpuHistorySeries  { get; }

        // ── RAM gauge label paint (theme-aware) ─────────────────────────
        [ObservableProperty]
        private SolidColorPaint _ramGaugeLabelPaint = CreateLabelPaint();

        public Axis[] SparklineXAxes  { get; } = [new Axis { IsVisible = false, MinLimit = 0, MaxLimit = HistorySize - 1 }];
        public Axis[] CpuHistoryYAxes { get; } = [new Axis { IsVisible = false, MinLimit = 0, MaxLimit = 100 }];

        // ── Dynamic lists (refreshed every tick) ─────────────────────────
        [ObservableProperty] private List<CoreBarItem>    _cpuCoreItems       = [];
        [ObservableProperty] private List<GpuCardData>    _gpuCards           = [];
        [ObservableProperty] private List<LabelledSensor> _systemTemperatures = [];
        [ObservableProperty] private List<DataSensor>     _systemFans         = [];

        // ── Gpu1 / Gpu2 convenience ──────────────────────────────────────
        public GpuCardData? Gpu1 => _gpuCards.Count > 0 ? _gpuCards[0] : null;
        public GpuCardData? Gpu2 => _gpuCards.Count > 1 ? _gpuCards[1] : null;

        // ── CPU sensors ──────────────────────────────────────────────────
        public string?       CpuName              => Computer.HardwareList.FirstOrDefault(h => h.HardwareType == HardwareType.Cpu)?.Name;
        public string?       CpuSummary           => BuildCpuSummary();
        public DataSensor?   CpuTemperatureSensor => SensorLocator.CpuTemperature(Computer);
        public DataSensor?   CpuUsageSensor       => SensorLocator.CpuLoad(Computer);
        public DataSensor?   CpuEnergySensor      => SensorLocator.CpuPower(Computer);
        public DataSensor?   CpuFanSpeed1Sensor   => SensorLocator.CpuFan(Computer, 0);
        public DataSensor?   CpuFanSpeed2Sensor   => SensorLocator.CpuFan(Computer, 1);

        // ── RAM sensors ──────────────────────────────────────────────────
        public DataSensor?   MemoryUsageSensor            => SensorLocator.MemoryLoad(Computer);
        public DataSensor?   MemoryUsedSensor             => SensorLocator.MemoryUsed(Computer);
        public DataSensor?   MemoryAvailableSensor        => SensorLocator.MemoryAvailable(Computer);
        public DataSensor?   MemoryPowerSensor            => SensorLocator.MemoryPower(Computer);
        public DataSensor?   VirtualMemoryUsedSensor      => SensorLocator.VirtualMemoryUsed(Computer);
        public DataSensor?   VirtualMemoryAvailableSensor => SensorLocator.VirtualMemoryAvailable(Computer);

        public float RamTotalGb  =>
            (MemoryUsedSensor?.Value ?? 0) + (MemoryAvailableSensor?.Value ?? 0);

        // ── RAM Windows extras ───────────────────────────────────────────
        public float PageReadsPerSec  => _monitorService.PageReadsPerSec;
        public float PageWritesPerSec => _monitorService.PageWritesPerSec;
        public float CacheGb          => _monitorService.CacheBytes / 1_073_741_824f;

        /// <summary>Cache weight for the segmented bar (as % of total RAM).</summary>
        public double CacheBarWeight => RamTotalGb > 0 ? CacheGb / RamTotalGb * 100.0 : 0;
        /// <summary>Free weight for the segmented bar (as % of total RAM).</summary>
        public double FreeBarWeight  => Math.Max(0, 100.0 - _ramGaugeValue - CacheBarWeight);

        // ── Network sensors ──────────────────────────────────────────────
        public string?       NetworkName                   => Computer.HardwareList.FirstOrDefault(h => h.HardwareType == HardwareType.Network)?.Name;
        public DataSensor?   NetworkUploadSpeedSensor      => SensorLocator.NetworkUploadSpeed(Computer);
        public DataSensor?   NetworkDownloadSpeedSensor    => SensorLocator.NetworkDownloadSpeed(Computer);
        public DataSensor?   NetworkDataUploadedSensor     => SensorLocator.NetworkDataUploaded(Computer);
        public DataSensor?   NetworkDataDownloadedSensor   => SensorLocator.NetworkDataDownloaded(Computer);

        // ── Header — uptime & clock ───────────────────────────────────────────
        private readonly DispatcherTimer _clockTimer;

        public string SystemUptime
        {
            get
            {
                var t = TimeSpan.FromMilliseconds(Environment.TickCount64);
                return t.Days > 0
                    ? $"{t.Days}d {t.Hours:D2}h {t.Minutes:D2}m"
                    : $"{t.Hours:D2}h {t.Minutes:D2}m";
            }
        }

        public string CurrentDateTime => DateTime.Now.ToString("ddd, d MMM  HH:mm");

        private bool _suspended;

        // ── Sensor reference cache — notify XAML only when reference changes ────────
        private DataSensor? _prevCpuTemperatureSensor;
        private DataSensor? _prevCpuUsageSensor;
        private DataSensor? _prevCpuEnergySensor;
        private DataSensor? _prevCpuFanSpeed1Sensor;
        private DataSensor? _prevCpuFanSpeed2Sensor;
        private DataSensor? _prevMemoryUsageSensor;
        private DataSensor? _prevMemoryUsedSensor;
        private DataSensor? _prevMemoryAvailableSensor;
        private DataSensor? _prevMemoryPowerSensor;
        private DataSensor? _prevNetworkUploadSensor;
        private DataSensor? _prevNetworkDownloadSensor;
        private DataSensor? _prevNetworkDataUploadedSensor;
        private DataSensor? _prevNetworkDataDownloadedSensor;
        private string?     _prevCpuName;
        private string?     _prevCpuSummary;
        private string?     _prevNetworkName;

        public void Suspend() => _suspended = true;
        public void Resume()  => _suspended = false;

        public DashboardViewModel(HardwareMonitorService monitorService)
        {
            _monitorService = monitorService;

            CpuHistorySeries =
            [
                new LineSeries<double>
                {
                    Values          = CpuUsageHistory,
                    Fill            = new SolidColorPaint(CpuColor.WithAlpha(35)),
                    Stroke          = new SolidColorPaint(CpuColor) { StrokeThickness = 1.5f },
                    GeometryFill    = null,
                    GeometryStroke  = null,
                    GeometrySize    = 0,
                    LineSmoothness  = 0.5,
                    AnimationsSpeed = TimeSpan.Zero,
                    IsHoverable     = false
                }
            ];

            _monitorService.DataUpdated += OnDataUpdated;
            ApplicationThemeManager.Changed += OnThemeChanged;

            _clockTimer = new DispatcherTimer(DispatcherPriority.Background)
            {
                Interval = TimeSpan.FromMinutes(1)
            };
            _clockTimer.Tick += (_, _) =>
            {
                OnPropertyChanged(nameof(SystemUptime));
                OnPropertyChanged(nameof(CurrentDateTime));
            };
            _clockTimer.Start();
        }

        // ── Theme-aware SkiaSharp colors ─────────────────────────────────

        private static bool IsLight => ApplicationThemeManager.GetAppTheme() == ApplicationTheme.Light;
        internal static SKColor CpuColor => IsLight ? new SKColor(0xCA, 0x50, 0x10) : new SKColor(0xF5, 0xA6, 0x23);
        internal static SKColor GpuColor => IsLight ? new SKColor(0x7B, 0x4F, 0xBF) : new SKColor(0x9D, 0x6E, 0xF5);

        private static SolidColorPaint CreateLabelPaint()
        {
            var color = IsLight ? SKColors.Black : SKColors.White;
            return new SolidColorPaint(color) { SKTypeface = SKTypeface.FromFamilyName("Segoe UI") };
        }

        private void OnThemeChanged(ApplicationTheme currentTheme, System.Windows.Media.Color _)
        {
            Application.Current?.Dispatcher.Invoke(() =>
            {
                if (CpuHistorySeries[0] is LineSeries<double> cpuLine)
                {
                    cpuLine.Fill   = new SolidColorPaint(CpuColor.WithAlpha(35));
                    cpuLine.Stroke = new SolidColorPaint(CpuColor) { StrokeThickness = 1.5f };
                }

                // Update gauge label paint
                RamGaugeLabelPaint = CreateLabelPaint();
            });
        }

        private void OnDataUpdated()
        {
            if (_suspended) return;

            CalculateEffectiveCpuClock();

            var cpuLoad = CpuUsageSensor?.Value ?? 0;

            // Update GPU cards — reuse existing instances to preserve history,
            // only create new ones for GPUs that weren't present before.
            var allGpus = SensorLocator.AllGpus(Computer).ToList();
            if (allGpus.Count != _gpuCards.Count)
            {
                var updated = allGpus
                    .Select((hw, i) => i < _gpuCards.Count ? _gpuCards[i] : new GpuCardData(hw))
                    .ToList();
                GpuCards = updated;
                OnPropertyChanged(nameof(Gpu1));
                OnPropertyChanged(nameof(Gpu2));
            }

            CpuGaugeValue = cpuLoad;
            RamGaugeValue = Math.Round(MemoryUsageSensor?.Value ?? 0);

            CpuUsageHistory.RemoveAt(0);
            CpuUsageHistory.Add(cpuLoad);

            // Per-core CPU loads
            var cores = SensorLocator.CpuCoreSensors(Computer);
            CpuCoreItems = cores
                .Select((s, i) => new CoreBarItem($"C{i + 1}", s))
                .ToList();

            // System temperatures
            SystemTemperatures = SensorLocator.SystemTemperatures(Computer)
                .Select(t => new LabelledSensor(t.Label, t.Sensor))
                .ToList();

            // System fans
            SystemFans = SensorLocator.MotherboardFans(Computer);

            // Total power
            var cpuW = CpuEnergySensor?.Value  ?? 0;
            var ramW = MemoryPowerSensor?.Value ?? 0;
            var gpuW = _gpuCards.Count > 0 ? (_gpuCards[0].PowerSensor?.Value ?? 0) : 0;
            TotalPower = cpuW + ramW + gpuW;

            // Derived values that depend on non-observable sources — notify every tick
            OnPropertyChanged(nameof(RamTotalGb));
            OnPropertyChanged(nameof(PageReadsPerSec));
            OnPropertyChanged(nameof(PageWritesPerSec));
            OnPropertyChanged(nameof(CacheGb));
            OnPropertyChanged(nameof(CacheBarWeight));
            OnPropertyChanged(nameof(FreeBarWeight));

            // Sensor references and computed strings — notify only when the value changes
            NotifySensorReferences();
        }

        // ── Effective CPU clock (maximum weighted by core load) ─────────────────────────────────
        private void CalculateEffectiveCpuClock()
        {
            var cpu = Computer.HardwareList.FirstOrDefault(h => h.HardwareType == HardwareType.Cpu);
            if (cpu == null) { EffectiveCpuClock = 0; return; }

            var clockSensors = cpu.Sensors
                .Where(s => s.SensorType == SensorType.Clock && s.Name.Contains("Core #"))
                .ToList();
            var loadSensors = cpu.Sensors
                .Where(s => s.SensorType == SensorType.Load && s.Name.Contains("CPU Core #"))
                .ToList();

            float maxEffectiveClock = 0;
            foreach (var clockSensor in clockSensors)
            {
                var coreNum = clockSensor.Name.Replace("Core #", "").Trim();
                var loadSensor = loadSensors.FirstOrDefault(s => s.Name.EndsWith(coreNum, StringComparison.Ordinal));
                float ec = loadSensor != null
                    ? clockSensor.Value * (loadSensor.Value / 100f)
                    : clockSensor.Value;
                if (ec > maxEffectiveClock) maxEffectiveClock = ec;
            }
            EffectiveCpuClock = maxEffectiveClock;
        }

        private void NotifySensorReferences()
        {
            NotifyIfChanged(ref _prevCpuName,       CpuName,       nameof(CpuName));
            NotifyIfChanged(ref _prevCpuSummary,    CpuSummary,    nameof(CpuSummary));
            NotifyIfChanged(ref _prevNetworkName,   NetworkName,   nameof(NetworkName));

            NotifyIfChanged(ref _prevCpuTemperatureSensor,        CpuTemperatureSensor,        nameof(CpuTemperatureSensor));
            NotifyIfChanged(ref _prevCpuUsageSensor,              CpuUsageSensor,              nameof(CpuUsageSensor));
            NotifyIfChanged(ref _prevCpuEnergySensor,             CpuEnergySensor,             nameof(CpuEnergySensor));
            NotifyIfChanged(ref _prevCpuFanSpeed1Sensor,          CpuFanSpeed1Sensor,          nameof(CpuFanSpeed1Sensor));
            NotifyIfChanged(ref _prevCpuFanSpeed2Sensor,          CpuFanSpeed2Sensor,          nameof(CpuFanSpeed2Sensor));
            NotifyIfChanged(ref _prevMemoryUsageSensor,           MemoryUsageSensor,           nameof(MemoryUsageSensor));
            NotifyIfChanged(ref _prevMemoryUsedSensor,            MemoryUsedSensor,            nameof(MemoryUsedSensor));
            NotifyIfChanged(ref _prevMemoryAvailableSensor,       MemoryAvailableSensor,       nameof(MemoryAvailableSensor));
            NotifyIfChanged(ref _prevMemoryPowerSensor,           MemoryPowerSensor,           nameof(MemoryPowerSensor));
            NotifyIfChanged(ref _prevNetworkUploadSensor,         NetworkUploadSpeedSensor,    nameof(NetworkUploadSpeedSensor));
            NotifyIfChanged(ref _prevNetworkDownloadSensor,       NetworkDownloadSpeedSensor,  nameof(NetworkDownloadSpeedSensor));
            NotifyIfChanged(ref _prevNetworkDataUploadedSensor,   NetworkDataUploadedSensor,   nameof(NetworkDataUploadedSensor));
            NotifyIfChanged(ref _prevNetworkDataDownloadedSensor, NetworkDataDownloadedSensor, nameof(NetworkDataDownloadedSensor));
        }

        private void NotifyIfChanged<T>(ref T? last, T? current, string name) where T : class
        {
            if (Equals(last, current)) return;
            last = current;
            OnPropertyChanged(name);
        }

        public void Dispose()
        {
            _clockTimer.Stop();
            _monitorService.DataUpdated -= OnDataUpdated;
            ApplicationThemeManager.Changed -= OnThemeChanged;
        }

        private string? BuildCpuSummary()
        {
            var cpu = Computer.HardwareList.FirstOrDefault(h => h.HardwareType == HardwareType.Cpu);
            if (cpu == null) return null;

            int cores = cpu.Sensors
                .Count(s => s.SensorType == SensorType.Clock
                         && s.Name.Contains("Core #")
                         && !s.Name.Contains("("));
            if (cores == 0) return null;

            int threads = cpu.Sensors
                .Count(s => s.SensorType == SensorType.Load && s.Name.Contains("Thread #"));

            return threads > 0 ? $"{cores}C / {threads}T" : $"{cores} Cores";
        }
    }

    // ── Per-GPU card data (sensors only — sparkline lives in DashboardViewModel) ──
    public class GpuCardData(DataHardware gpu)
    {
        public string      Name           => gpu.Name;
        public DataSensor? TempSensor     => SensorLocator.GpuTemperatureFor(gpu);
        public DataSensor? LoadSensor     => SensorLocator.GpuLoadFor(gpu);
        public DataSensor? ClockSensor    => SensorLocator.GpuClockFor(gpu);
        public DataSensor? PowerSensor    => SensorLocator.GpuPowerFor(gpu);
        public DataSensor? Fan1Sensor     => SensorLocator.GpuFanFor(gpu, 0);
        public DataSensor? Fan2Sensor     => SensorLocator.GpuFanFor(gpu, 1);
        public DataSensor? VramUsedSensor  => SensorLocator.GpuVramUsedFor(gpu);
        public DataSensor? VramTotalSensor => SensorLocator.GpuVramTotalFor(gpu);

        public float VramPercent =>
            VramTotalSensor?.Value > 0
                ? Math.Clamp((VramUsedSensor?.Value ?? 0) / VramTotalSensor!.Value * 100f, 0f, 100f)
                : 0f;
    }

    // ── CPU core bar ─────────────────────────────────────────────────────────
    public class CoreBarItem(string label, DataSensor sensor)
    {
        private const double MaxHeight = 120.0;
        public string     Label     => label;
        public DataSensor Sensor    => sensor;
        public double     BarHeight => MaxHeight * (sensor.Value / 100.0);
        public string     ValueText => $"{sensor.Value:F0}%";
    }

    // ── GPU core bar ──────────────────────────────────────────────────────────
    public class GpuCoreBarItem(string label, DataSensor sensor)
    {
        private const double MaxHeight = 80.0;
        public string     Label     => label;
        public DataSensor Sensor    => sensor;
        public double     BarHeight => MaxHeight * (sensor.Value / 100.0);
        public string     ValueText => $"{sensor.Value:F0}%";
    }

    // ── Labelled sensor (temperatures list) ──────────────────────────────────
    public class LabelledSensor(string label, DataSensor sensor)
    {
        public string     Label  => label;
        public DataSensor Sensor => sensor;
    }
}