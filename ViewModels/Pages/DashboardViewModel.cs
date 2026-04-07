using Aquila.Models;
using Aquila.Services;
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

        public AquilaSnapshot Aquila => _monitorService.CurrentSnapshot;

        [ObservableProperty] private float _effectiveCpuClock;
        [ObservableProperty] private double _cpuGaugeValue;
        [ObservableProperty] private double _ramGaugeValue;

        // ── History ring buffers ─────────────────────────────────────────
        public ObservableCollection<double> CpuUsageHistory { get; } = new(Enumerable.Repeat(0.0, HistorySize));
        public ObservableCollection<double> RamUsageHistory { get; } = new(Enumerable.Repeat(0.0, HistorySize));
        public ObservableCollection<double> NetworkDownloadHistory { get; } = new(Enumerable.Repeat(0.0, HistorySize));
        public ObservableCollection<double> NetworkUploadHistory { get; } = new(Enumerable.Repeat(0.0, HistorySize));

        // ── RAM gauge label paint (theme-aware) ─────────────────────────
        [ObservableProperty]
        private SolidColorPaint _ramGaugeLabelPaint = CreateLabelPaint();

        // ── Dynamic lists (refreshed every tick) ─────────────────────────
        [ObservableProperty] private List<CoreBarItem> _cpuCoreItems = [];
        [ObservableProperty] private List<GpuCardData> _gpuCards = [];
        [ObservableProperty] private List<LabelledMetric> _systemTemperatures = [];
        [ObservableProperty] private List<FanMetricItem> _systemFans = [];
        [ObservableProperty] private List<StorageDriveData> _storageCards = [];

        // ── Gpu1 / Gpu2 convenience ──────────────────────────────────────
        public GpuCardData? Gpu1 => GpuCards.Count > 0 ? GpuCards[0] : null;
        public GpuCardData? Gpu2 => GpuCards.Count > 1 ? GpuCards[1] : null;

        /// <summary>Cache weight for the segmented bar (as % of total RAM).</summary>
        public double CacheBarWeight => Aquila.Memory.TotalVisibleGb > 0 ? Aquila.Memory.CacheGb / Aquila.Memory.TotalVisibleGb * 100.0 : 0;

        /// <summary>Free weight for the segmented bar (as % of total RAM).</summary>
        public double FreeBarWeight => Math.Max(0, 100.0 - RamGaugeValue - CacheBarWeight);

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

        public void Suspend() => _suspended = true;
        public void Resume() => _suspended = false;

        public DashboardViewModel(HardwareMonitorService monitorService)
        {
            _monitorService = monitorService;

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

        // ── Theme-aware SkiaSharp helpers (RAM gauge only) ─────────────

        private static bool IsLight => ApplicationThemeManager.GetAppTheme() == ApplicationTheme.Light;

        private static SolidColorPaint CreateLabelPaint()
        {
            var color = IsLight ? SKColors.Black : SKColors.White;
            return new SolidColorPaint(color) { SKTypeface = SKTypeface.FromFamilyName("Segoe UI") };
        }

        private void OnThemeChanged(ApplicationTheme currentTheme, System.Windows.Media.Color _)
        {
            Application.Current?.Dispatcher.Invoke(() =>
            {
                RamGaugeLabelPaint = CreateLabelPaint();
            });
        }

        private void OnDataUpdated()
        {
            if (_suspended) return;

            CalculateEffectiveCpuClock();
            OnPropertyChanged(nameof(Aquila));

            var cpuLoad = (float)(Aquila.Cpu.Load.Value ?? 0);

            // Dashboard shows only the primary GPU card.
            var primaryGpu = Aquila.Gpu.Primary;
            if (primaryGpu is null)
            {
                if (GpuCards.Count > 0)
                {
                    GpuCards = [];
                    OnPropertyChanged(nameof(Gpu1));
                    OnPropertyChanged(nameof(Gpu2));
                }
            }
            else if (GpuCards.Count != 1 || !string.Equals(GpuCards[0].Identifier, primaryGpu.Identifier, StringComparison.Ordinal))
            {
                var existingCard = GpuCards.FirstOrDefault(card =>
                    string.Equals(card.Identifier, primaryGpu.Identifier, StringComparison.Ordinal));

                if (existingCard is not null)
                    existingCard.Update(primaryGpu);

                GpuCards = [existingCard ?? new GpuCardData(primaryGpu)];
                OnPropertyChanged(nameof(Gpu1));
                OnPropertyChanged(nameof(Gpu2));
            }
            else
            {
                GpuCards[0].Update(primaryGpu);
            }

            foreach (var card in GpuCards)
                card.PushHistory();

            CpuGaugeValue = cpuLoad;
            RamGaugeValue = Math.Round(Aquila.Memory.LoadPercent.Value ?? 0);

            CpuUsageHistory.RemoveAt(0);
            CpuUsageHistory.Add(cpuLoad);

            RamUsageHistory.RemoveAt(0);
            RamUsageHistory.Add(Aquila.Memory.LoadPercent.Value ?? 0);

            // Network throughput history
            NetworkDownloadHistory.RemoveAt(0);
            NetworkDownloadHistory.Add(Aquila.Network.DownloadSpeed.Value ?? 0);
            NetworkUploadHistory.RemoveAt(0);
            NetworkUploadHistory.Add(Aquila.Network.UploadSpeed.Value ?? 0);

            // Per-core CPU loads
            CpuCoreItems = Aquila.Cpu.Cores
                .Select(core => new CoreBarItem(core.Label, core.Load.Value ?? 0))
                .ToList();

            // System temperatures
            SystemTemperatures = Aquila.Temperatures
                .Select(item => new LabelledMetric(item.Label, item.Value))
                .ToList();

            // System fans
            SystemFans = Aquila.Fans
                .Select(item => new FanMetricItem(item.Name, item.Speed))
                .ToList();

            // Storage cards
            StorageCards = Aquila.Storage.Select(drive => new StorageDriveData(drive)).ToList();

            // Derived values that depend on non-observable sources — notify every tick
            OnPropertyChanged(nameof(CacheBarWeight));
            OnPropertyChanged(nameof(FreeBarWeight));
        }

        // ── Effective CPU clock (maximum weighted by core load) ─────────────────────────────────
        private void CalculateEffectiveCpuClock()
        {
            EffectiveCpuClock = (float)(Aquila.Cpu.EffectiveClock.Value ?? 0);
        }

        public void Dispose()
        {
            _clockTimer.Stop();
            _monitorService.DataUpdated -= OnDataUpdated;
            ApplicationThemeManager.Changed -= OnThemeChanged;
        }

    }

    // ── Per-GPU card data (sensors + own sparkline history) ──
    public class GpuCardData
    {
        private const int HistorySize = 60;
        private GpuSnapshot _gpu;

        public GpuCardData(GpuSnapshot gpu)
        {
            _gpu = gpu;
            UsageHistory = new ObservableCollection<double>(Enumerable.Repeat(0.0, HistorySize));
        }

        public string Identifier => _gpu.Identifier ?? string.Empty;
        public string Name => _gpu.Name ?? "GPU";
        public MetricValue Temperature => _gpu.Temperature;
        public MetricValue Load => _gpu.Load;
        public MetricValue Clock => _gpu.Clock;
        public MetricValue Power => _gpu.Power;
        public MetricValue Fan1 => _gpu.FanRpm;
        public MetricValue Fan2 => _gpu.Fan2Rpm;
        public MetricValue VramUsed => _gpu.VramUsed;
        public MetricValue VramTotal => _gpu.VramTotal;
        public double VramPercent => _gpu.VramPercent;

        public ObservableCollection<double> UsageHistory { get; }

        public void Update(GpuSnapshot gpu)
        {
            _gpu = gpu;
        }

        public void PushHistory()
        {
            UsageHistory.RemoveAt(0);
            UsageHistory.Add(_gpu.Load.Value ?? 0);
        }
    }

    // ── CPU core bar ─────────────────────────────────────────────────────────
    public class CoreBarItem(string label, double value)
    {
        private const double MaxHeight = 92.0;
        public string Label => label;
        public double Value => value;
        public double BarHeight => MaxHeight * (value / 100.0);
        public string ValueText => $"{value:F0}%";
    }

    // ── Labelled metric (temperatures list) ──────────────────────────────────
    public class LabelledMetric(string label, MetricValue metric)
    {
        public string Label => label;
        public MetricValue Metric => metric;
    }

    // ── Fan metric row ───────────────────────────────────────────────────────
    public class FanMetricItem(string name, MetricValue metric)
    {
        public string Name => name;
        public MetricValue Metric => metric;
    }

    // ── Per-storage drive data ────────────────────────────────────────────────
    public class StorageDriveData(StorageDeviceSnapshot drive)
    {
        public string Name => drive.Name ?? "Drive";
        public MetricValue Temperature => drive.Temperature;
        public MetricValue ReadRate => drive.ReadRate;
        public MetricValue WriteRate => drive.WriteRate;
        public MetricValue UsedSpace => drive.UsedSpace;
    }
}