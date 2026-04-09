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
        private const int HistorySize = 60;

        private readonly HardwareMonitorService _monitorService;
        private readonly DispatcherTimer _clockTimer;
        private bool _suspended;

        public AquilaSnapshot Aquila => _monitorService.CurrentSnapshot;

        [ObservableProperty] private float _effectiveCpuClock;
        [ObservableProperty] private double _cpuGaugeValue;
        [ObservableProperty] private double _ramGaugeValue;

        [ObservableProperty]
        private SolidColorPaint _ramGaugeLabelPaint = CreateLabelPaint();

        [ObservableProperty] private List<CoreBarItem> _cpuCoreItems = [];
        [ObservableProperty] private List<GpuCardData> _gpuCards = [];
        [ObservableProperty] private List<LabelledMetric> _systemTemperatures = [];
        [ObservableProperty] private List<FanMetricItem> _systemFans = [];
        [ObservableProperty] private List<StorageDriveData> _storageCards = [];

        public ObservableCollection<double> CpuUsageHistory { get; } = new(Enumerable.Repeat(0.0, HistorySize));
        public ObservableCollection<double> RamUsageHistory { get; } = new(Enumerable.Repeat(0.0, HistorySize));
        public ObservableCollection<double> NetworkDownloadHistory { get; } = new(Enumerable.Repeat(0.0, HistorySize));
        public ObservableCollection<double> NetworkUploadHistory { get; } = new(Enumerable.Repeat(0.0, HistorySize));

        public GpuCardData? Gpu1 => GpuCards.Count > 0 ? GpuCards[0] : null;
        public GpuCardData? Gpu2 => GpuCards.Count > 1 ? GpuCards[1] : null;

        /// <summary>Cache weight for the segmented bar (as % of total RAM).</summary>
        public double CacheBarWeight => Aquila.Memory.TotalVisibleGb > 0
            ? Aquila.Memory.CacheGb / Aquila.Memory.TotalVisibleGb * 100.0
            : 0;

        /// <summary>Free weight for the segmented bar (as % of total RAM).</summary>
        public double FreeBarWeight => Math.Max(0, 100.0 - RamGaugeValue - CacheBarWeight);

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

            OnDataUpdated();
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
            if (_suspended)
                return;

            OnPropertyChanged(nameof(Aquila));

            UpdateCpuSection();
            UpdatePrimaryGpuCard();
            UpdateHistorySeries();
            RefreshMetricLists();
            NotifyDerivedProperties();
        }

        private void UpdateCpuSection()
        {
            EffectiveCpuClock = (float)(Aquila.Cpu.EffectiveClock.Value ?? 0);
            CpuGaugeValue = Aquila.Cpu.Load.Value ?? 0;
            RamGaugeValue = Math.Round(Aquila.Memory.LoadPercent.Value ?? 0);

            CpuCoreItems = Aquila.Cpu.Cores
                .Select(core => new CoreBarItem(core.Label, core.Load.Value ?? 0))
                .ToList();
        }

        private void UpdatePrimaryGpuCard()
        {
            var primaryGpu = Aquila.Gpu.Primary;

            if (primaryGpu is null)
            {
                if (GpuCards.Count == 0)
                    return;

                GpuCards = [];
                OnPropertyChanged(nameof(Gpu1));
                OnPropertyChanged(nameof(Gpu2));
                return;
            }

            if (GpuCards.Count == 1 &&
                string.Equals(GpuCards[0].Identifier, primaryGpu.Identifier, StringComparison.Ordinal))
            {
                GpuCards[0].Update(primaryGpu);
                return;
            }

            var existingCard = GpuCards.FirstOrDefault(card =>
                string.Equals(card.Identifier, primaryGpu.Identifier, StringComparison.Ordinal));

            if (existingCard is not null)
                existingCard.Update(primaryGpu);

            GpuCards = [existingCard ?? new GpuCardData(primaryGpu)];
            OnPropertyChanged(nameof(Gpu1));
            OnPropertyChanged(nameof(Gpu2));
        }

        private void UpdateHistorySeries()
        {
            foreach (var card in GpuCards)
                card.PushHistory();

            PushHistorySample(CpuUsageHistory, Aquila.Cpu.Load.Value ?? 0);
            PushHistorySample(RamUsageHistory, Aquila.Memory.LoadPercent.Value ?? 0);
            PushHistorySample(NetworkDownloadHistory, Aquila.Network.DownloadSpeed.Value ?? 0);
            PushHistorySample(NetworkUploadHistory, Aquila.Network.UploadSpeed.Value ?? 0);
        }

        private void RefreshMetricLists()
        {
            SystemTemperatures = Aquila.Temperatures
                .Select(item => new LabelledMetric(item.Label, item.Value))
                .ToList();

            SystemFans = Aquila.Fans
                .Select(item => new FanMetricItem(item.Name, item.Speed))
                .ToList();

            StorageCards = Aquila.Storage
                .Select(drive => new StorageDriveData(drive))
                .ToList();
        }

        private void NotifyDerivedProperties()
        {
            OnPropertyChanged(nameof(CacheBarWeight));
            OnPropertyChanged(nameof(FreeBarWeight));
        }

        private static void PushHistorySample(ObservableCollection<double> history, double value)
        {
            if (history.Count > 0)
                history.RemoveAt(0);

            history.Add(value);
        }

        public void Dispose()
        {
            _clockTimer.Stop();
            _monitorService.DataUpdated -= OnDataUpdated;
            ApplicationThemeManager.Changed -= OnThemeChanged;
        }

    }

    // ── Per-GPU card data (sensors + own sparkline history) ──
    public sealed class GpuCardData : ObservableObject
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
            OnPropertyChanged(nameof(Name));
            OnPropertyChanged(nameof(Temperature));
            OnPropertyChanged(nameof(Load));
            OnPropertyChanged(nameof(Clock));
            OnPropertyChanged(nameof(Power));
            OnPropertyChanged(nameof(Fan1));
            OnPropertyChanged(nameof(Fan2));
            OnPropertyChanged(nameof(VramUsed));
            OnPropertyChanged(nameof(VramTotal));
            OnPropertyChanged(nameof(VramPercent));
        }

        public void PushHistory()
        {
            UsageHistory.RemoveAt(0);
            UsageHistory.Add(_gpu.Load.Value ?? 0);
        }
    }

    // ── CPU core bar ─────────────────────────────────────────────────────────
    public sealed class CoreBarItem(string label, double value)
    {
        private const double MaxHeight = 92.0;
        public string Label => label;
        public double Value => value;
        public double BarHeight => MaxHeight * (value / 100.0);
        public string ValueText => $"{value:F0}%";
    }

    // ── Labelled metric (temperatures list) ──────────────────────────────────
    public sealed class LabelledMetric(string label, MetricValue metric)
    {
        public string Label => label;
        public MetricValue Metric => metric;
    }

    // ── Fan metric row ───────────────────────────────────────────────────────
    public sealed class FanMetricItem(string name, MetricValue metric)
    {
        public string Name => name;
        public MetricValue Metric => metric;
    }

    // ── Per-storage drive data ────────────────────────────────────────────────
    public sealed class StorageDriveData(StorageDeviceSnapshot drive)
    {
        public string Name => drive.Name ?? "Drive";
        public MetricValue Temperature => drive.Temperature;
        public MetricValue ReadRate => drive.ReadRate;
        public MetricValue WriteRate => drive.WriteRate;
        public MetricValue UsedSpace => drive.UsedSpace;
    }
}