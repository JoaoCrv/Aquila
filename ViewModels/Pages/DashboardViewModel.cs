using Aquila.Models.Api;
using Aquila.Services;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System;
using CommunityToolkit.Mvvm.ComponentModel;
using System.Windows;
using System.Windows.Threading;

namespace Aquila.ViewModels.Pages
{
    public partial class DashboardViewModel : ObservableObject, IDisposable
    {
        private const int HistorySize = 60;

        private readonly AquilaService _aquila;
        private readonly DispatcherTimer _clockTimer;
        private bool _suspended;

        public AquilaSemanticState Aquila => _aquila.State.Semantic;

        [ObservableProperty] private double _cpuGaugeValue;
        [ObservableProperty] private double _ramGaugeValue;
        [ObservableProperty] private float _effectiveCpuClock;

        [ObservableProperty]
        private SolidColorPaint _ramGaugeLabelPaint = CreateLabelPaint();

        [ObservableProperty] private List<CoreBarItem> _cpuCoreItems = [];
        [ObservableProperty] private List<GpuCardData> _gpuCards = [];

        public ObservableCollection<double> CpuUsageHistory { get; } = new(Enumerable.Repeat(0.0, HistorySize));
        public ObservableCollection<double> RamUsageHistory { get; } = new(Enumerable.Repeat(0.0, HistorySize));
        public ObservableCollection<double> NetworkDownloadHistory { get; } = new(Enumerable.Repeat(0.0, HistorySize));
        public ObservableCollection<double> NetworkUploadHistory { get; } = new(Enumerable.Repeat(0.0, HistorySize));

        public GpuCardData? Gpu1 => GpuCards.Count > 0 ? GpuCards[0] : null;
        public GpuCardData? Gpu2 => GpuCards.Count > 1 ? GpuCards[1] : null;

        public double CacheBarWeight => 0;
        public double FreeBarWeight => Math.Max(0, 100.0 - RamGaugeValue);

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

        public string? CpuSummary => Aquila.Cpu.Temperature.Package?.Value is float temp && Aquila.Cpu.Power.Package?.Value is float power
            ? $"{temp:F0}°C • {power:F0} W"
            : null;

        public double MemoryTotalVisibleGb => Aquila.Memory.Data.Total?.Value ?? 0;
        public double MemoryCacheGb => Aquila.Memory.Data.Cache?.Value ?? 0;

        public double TotalPowerValue => (Aquila.Cpu.Power.Package?.Value ?? 0) + (Aquila.Memory.Power.Total?.Value ?? 0) + GpuCards.Sum(card => card.Power?.Value ?? 0);

        public void Suspend() => _suspended = true;
        public void Resume() => _suspended = false;

        public DashboardViewModel(AquilaService aquila)
        {
            _aquila = aquila;

            _aquila.DataUpdated += OnDataUpdated;
            Wpf.Ui.Appearance.ApplicationThemeManager.Changed += OnThemeChanged;

            _clockTimer = new DispatcherTimer(System.Windows.Threading.DispatcherPriority.Background)
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

        private static bool IsLight => Wpf.Ui.Appearance.ApplicationThemeManager.GetAppTheme() == Wpf.Ui.Appearance.ApplicationTheme.Light;

        private static SolidColorPaint CreateLabelPaint()
        {
            var color = IsLight ? SKColors.Black : SKColors.White;
            return new SolidColorPaint(color) { SKTypeface = SKTypeface.FromFamilyName("Segoe UI") };
        }

        private void OnThemeChanged(Wpf.Ui.Appearance.ApplicationTheme currentTheme, System.Windows.Media.Color _)
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
            UpdateGpuCards();
            UpdateHistorySeries();
            NotifyDerivedProperties();
        }

        private void UpdateCpuSection()
        {
            var totalLoad = Aquila.Cpu.Load.Total;
            CpuGaugeValue = totalLoad?.Value ?? 0;

            var avgClock = Aquila.Cpu.Clock.Effective;
            EffectiveCpuClock = avgClock?.Value ?? 0;

            var ramLoad = Aquila.Memory.Load.Total;
            RamGaugeValue = Math.Round(ramLoad?.Value ?? 0);

            CpuCoreItems = Aquila.Cpu.Cores
                .Select(core => new CoreBarItem(core.Name, core.Value ?? 0))
                .ToList();
        }

        private void UpdateGpuCards()
        {
            var newCards = new List<GpuCardData>();
            foreach (var gpu in Aquila.Gpus)
            {
                var existing = GpuCards.FirstOrDefault(c => c.Name == gpu.Name);
                if (existing != null)
                {
                    existing.Update(gpu);
                    newCards.Add(existing);
                }
                else
                {
                    newCards.Add(new GpuCardData(gpu));
                }
            }
            GpuCards = newCards;
            OnPropertyChanged(nameof(Gpu1));
            OnPropertyChanged(nameof(Gpu2));
        }

        private void UpdateHistorySeries()
        {
            foreach (var card in GpuCards)
                card.PushHistory();

            var totalLoad = Aquila.Cpu.Load.Total;
            PushHistorySample(CpuUsageHistory, totalLoad?.Value ?? 0);

            var ramLoad = Aquila.Memory.Load.Total;
            PushHistorySample(RamUsageHistory, ramLoad?.Value ?? 0);

            var netDown = Aquila.Network.Throughput.Download;
            var netUp = Aquila.Network.Throughput.Upload;

            PushHistorySample(NetworkDownloadHistory, netDown?.Value ?? 0);
            PushHistorySample(NetworkUploadHistory, netUp?.Value ?? 0);
        }

        private void NotifyDerivedProperties()
        {
            OnPropertyChanged(nameof(CacheBarWeight));
            OnPropertyChanged(nameof(FreeBarWeight));
            OnPropertyChanged(nameof(CpuSummary));
            OnPropertyChanged(nameof(MemoryTotalVisibleGb));
            OnPropertyChanged(nameof(MemoryCacheGb));
            OnPropertyChanged(nameof(TotalPowerValue));
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
            _aquila.DataUpdated -= OnDataUpdated;
            Wpf.Ui.Appearance.ApplicationThemeManager.Changed -= OnThemeChanged;
        }
    }

    public sealed class GpuCardData : ObservableObject
    {
        private const int HistorySize = 60;
        private GpuSemanticNode _gpu;

        public GpuCardData(GpuSemanticNode gpu)
        {
            _gpu = gpu;
            UsageHistory = new ObservableCollection<double>(Enumerable.Repeat(0.0, HistorySize));
        }

        public string Name => _gpu.Name;
        public SensorNode? Temperature => _gpu.Temperature.Core;
        public SensorNode? Load => _gpu.Load.Total;
        public SensorNode? Clock => _gpu.Clock.Core;
        public SensorNode? Power => _gpu.Power.Package;
        public SensorNode? Fan1 => _gpu.Fan.Primary;
        public SensorNode? Fan2 => _gpu.Fan.Secondary;

        public SensorNode? VramUsed => _gpu.Data.Used;
        public SensorNode? VramTotal => _gpu.Data.Total;
        public SensorNode? HotspotTemperature => _gpu.Temperature.Hotspot;

        public double VramPercent => (VramTotal != null && VramTotal.Value > 0 && VramUsed != null && VramUsed.Value.HasValue)
                                     ? ((VramUsed.Value.Value) / VramTotal.Value.Value) * 100.0 : 0;

        public ObservableCollection<double> UsageHistory { get; }

        public void Update(GpuSemanticNode gpu)
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
            OnPropertyChanged(nameof(HotspotTemperature));
        }

        public void PushHistory()
        {
            UsageHistory.RemoveAt(0);
            var l = Load?.Value ?? 0;
            UsageHistory.Add(l);
        }
    }

    public sealed class CoreBarItem(string label, double value)
    {
        private const double MaxHeight = 92.0;
        public string Label => label;
        public double Value => value;
        public double BarHeight => MaxHeight * (value / 100.0);
        public string ValueText => $"{value:F0}%";
    }
}