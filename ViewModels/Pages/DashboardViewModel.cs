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

namespace Aquila.ViewModels.Pages
{
    public partial class DashboardViewModel : ObservableObject
    {
        private readonly HardwareMonitorService _monitorService;
        private const int HistorySize = 60;

        public ComputerData Computer => _monitorService.ComputerData;

        [ObservableProperty] private float  _effectiveCpuClock;
        [ObservableProperty] private double _cpuGaugeValue;
        [ObservableProperty] private double _gpuGaugeValue;
        [ObservableProperty] private double _ramGaugeValue;
        [ObservableProperty] private float  _totalPower;

        // ── History ring buffers ─────────────────────────────────────────
        public ObservableCollection<double> CpuUsageHistory { get; } = new(Enumerable.Repeat(0.0, HistorySize));
        public ObservableCollection<double> GpuUsageHistory { get; } = new(Enumerable.Repeat(0.0, HistorySize));

        // ── Chart series & axes ──────────────────────────────────────────
        public ISeries[] CpuHistorySeries { get; }
        public ISeries[] GpuHistorySeries { get; }

        // ── RAM gauge label paint (used by XamlGaugeSeries in XAML) ─────
        public SolidColorPaint RamGaugeLabelPaint { get; } = new SolidColorPaint(SKColors.White) { SKTypeface = SKTypeface.FromFamilyName("Segoe UI") };

        public Axis[] SparklineXAxes  { get; } = [new Axis { IsVisible = false, MinLimit = 0, MaxLimit = HistorySize - 1 }];
        public Axis[] CpuHistoryYAxes { get; } = [new Axis { IsVisible = false, MinLimit = 0, MaxLimit = 100 }];
        public Axis[] GpuHistoryYAxes { get; } = [new Axis { IsVisible = false, MinLimit = 0, MaxLimit = 100 }];

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

        // ── Network sensors ──────────────────────────────────────────────
        public string?       NetworkName                   => Computer.HardwareList.FirstOrDefault(h => h.HardwareType == HardwareType.Network)?.Name;
        public DataSensor?   NetworkUploadSpeedSensor      => SensorLocator.NetworkUploadSpeed(Computer);
        public DataSensor?   NetworkDownloadSpeedSensor    => SensorLocator.NetworkDownloadSpeed(Computer);
        public DataSensor?   NetworkDataUploadedSensor     => SensorLocator.NetworkDataUploaded(Computer);
        public DataSensor?   NetworkDataDownloadedSensor   => SensorLocator.NetworkDataDownloaded(Computer);

        public DashboardViewModel(HardwareMonitorService monitorService)
        {
            _monitorService = monitorService;


            var cpuColor = new SKColor(0x60, 0xCD, 0xFF);
            var gpuColor = new SKColor(0x9D, 0x6E, 0xF5);


            CpuHistorySeries =
            [
                new LineSeries<double>
                {
                    Values          = CpuUsageHistory,
                    Fill            = new SolidColorPaint(cpuColor.WithAlpha(35)),
                    Stroke          = new SolidColorPaint(cpuColor) { StrokeThickness = 1.5f },
                    GeometryFill    = null,
                    GeometryStroke  = null,
                    GeometrySize    = 0,
                    LineSmoothness  = 0.5,
                    AnimationsSpeed = TimeSpan.FromMilliseconds(200),
                    IsHoverable     = false
                }
            ];

            GpuHistorySeries =
            [
                new LineSeries<double>
                {
                    Values          = GpuUsageHistory,
                    Fill            = new SolidColorPaint(gpuColor.WithAlpha(35)),
                    Stroke          = new SolidColorPaint(gpuColor) { StrokeThickness = 1.5f },
                    GeometryFill    = null,
                    GeometryStroke  = null,
                    GeometrySize    = 0,
                    LineSmoothness  = 0.5,
                    AnimationsSpeed = TimeSpan.FromMilliseconds(200),
                    IsHoverable     = false
                }
            ];

            _monitorService.DataUpdated += OnDataUpdated;
        }

        private void OnDataUpdated()
        {
            CalculateEffectiveCpuClock();

            var cpuLoad = CpuUsageSensor?.Value ?? 0;

            // Rebuild GPU cards first so Gpu1 is available for gauge values
            var newCards = SensorLocator.AllGpus(Computer)
                .Select(gpu => new GpuCardData(gpu))
                .ToList();

            // Carry over history from previous cards by position
            for (int i = 0; i < newCards.Count; i++)
            {
                if (i < _gpuCards.Count)
                    newCards[i].TakeHistory(_gpuCards[i]);
            }

            GpuCards = newCards;
            OnPropertyChanged(nameof(Gpu1));
            OnPropertyChanged(nameof(Gpu2));

            var gpuLoad = _gpuCards.Count > 0 ? (_gpuCards[0].LoadSensor?.Value ?? 0) : 0f;

            CpuGaugeValue = cpuLoad;
            GpuGaugeValue = gpuLoad;
            RamGaugeValue = Math.Round(MemoryUsageSensor?.Value ?? 0);

            CpuUsageHistory.RemoveAt(0);
            CpuUsageHistory.Add(cpuLoad);

            // Push load into every GPU card's own history
            foreach (var card in _gpuCards)
                card.PushHistory();

            GpuUsageHistory.RemoveAt(0);
            GpuUsageHistory.Add(gpuLoad);

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

            OnPropertyChanged(nameof(CpuName));
            OnPropertyChanged(nameof(NetworkName));
            OnPropertyChanged(nameof(MemoryUsedSensor));
            OnPropertyChanged(nameof(MemoryAvailableSensor));
            OnPropertyChanged(nameof(MemoryPowerSensor));
            OnPropertyChanged(nameof(VirtualMemoryUsedSensor));
            OnPropertyChanged(nameof(VirtualMemoryAvailableSensor));
            OnPropertyChanged(nameof(NetworkUploadSpeedSensor));
            OnPropertyChanged(nameof(NetworkDownloadSpeedSensor));
            OnPropertyChanged(nameof(NetworkDataUploadedSensor));
            OnPropertyChanged(nameof(NetworkDataDownloadedSensor));

            NotifySensorReferences();
        }

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
                var coreNum = clockSensor.Name.Replace("Core #", "");
                var loadSensor = loadSensors.FirstOrDefault(s => s.Name.EndsWith(coreNum, StringComparison.Ordinal));
                if (loadSensor != null)
                {
                    float ec = clockSensor.Value * (loadSensor.Value / 100);
                    if (ec > maxEffectiveClock) maxEffectiveClock = ec;
                }
            }
            EffectiveCpuClock = maxEffectiveClock;
        }

        private void NotifySensorReferences()
        {
            OnPropertyChanged(nameof(CpuTemperatureSensor));
            OnPropertyChanged(nameof(CpuUsageSensor));
            OnPropertyChanged(nameof(CpuEnergySensor));
            OnPropertyChanged(nameof(CpuFanSpeed1Sensor));
            OnPropertyChanged(nameof(CpuFanSpeed2Sensor));
            OnPropertyChanged(nameof(MemoryUsageSensor));
        }
    }

    // ── Per-GPU card data with own sparkline ─────────────────────────────────
    public class GpuCardData(DataHardware gpu)
    {
        private const int HistorySize = 60;

        public string      Name        => gpu.Name;
        public DataSensor? TempSensor  => SensorLocator.GpuTemperatureFor(gpu);
        public DataSensor? LoadSensor  => SensorLocator.GpuLoadFor(gpu);
        public DataSensor? ClockSensor => SensorLocator.GpuClockFor(gpu);
        public DataSensor? PowerSensor => SensorLocator.GpuPowerFor(gpu);
        public DataSensor? Fan1Sensor  => SensorLocator.GpuFanFor(gpu, 0);
        public DataSensor? Fan2Sensor  => SensorLocator.GpuFanFor(gpu, 1);

        public ObservableCollection<double> UsageHistory { get; } =
            new(Enumerable.Repeat(0.0, HistorySize));

        public ISeries[] SparkSeries { get; } = BuildSpark();
        public Axis[] SparkXAxes { get; } = [new Axis { IsVisible = false, MinLimit = 0, MaxLimit = HistorySize - 1 }];
        public Axis[] SparkYAxes { get; } = [new Axis { IsVisible = false, MinLimit = 0, MaxLimit = 100 }];

        private static ISeries[] BuildSpark()
        {
            var col = new ObservableCollection<double>(Enumerable.Repeat(0.0, HistorySize));
            var gpuColor = new SKColor(0x9D, 0x6E, 0xF5);
            return
            [
                new LineSeries<double>
                {
                    Values          = col,
                    Fill            = new SolidColorPaint(gpuColor.WithAlpha(35)),
                    Stroke          = new SolidColorPaint(gpuColor) { StrokeThickness = 1.5f },
                    GeometryFill    = null,
                    GeometryStroke  = null,
                    GeometrySize    = 0,
                    LineSmoothness  = 0.5,
                    AnimationsSpeed = TimeSpan.FromMilliseconds(200),
                    IsHoverable     = false
                }
            ];
        }

        /// <summary>Push current load into the card's own history buffer.</summary>
        public void PushHistory()
        {
            var collection = (ObservableCollection<double>)SparkSeries[0].Values!;
            collection.RemoveAt(0);
            collection.Add(LoadSensor?.Value ?? 0);
        }

        /// <summary>Copy the history from a previous card instance (avoids reset on list rebuild).</summary>
        public void TakeHistory(GpuCardData previous)
        {
            var src  = (ObservableCollection<double>)previous.SparkSeries[0].Values!;
            var dest = (ObservableCollection<double>)SparkSeries[0].Values!;
            for (int i = 0; i < src.Count && i < dest.Count; i++)
                dest[i] = src[i];
        }
    }

    // ── CPU core bar ─────────────────────────────────────────────────────────
    public class CoreBarItem(string label, DataSensor sensor)
    {
        private const double MaxHeight = 64.0;
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