using Aquila.Models.Api;
using Aquila.Services;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Threading;
using Wpf.Ui.Appearance;
using System;
using CommunityToolkit.Mvvm.ComponentModel;
using System.Windows;

namespace Aquila.ViewModels.Pages
{
    public partial class DashboardViewModel : ObservableObject, IDisposable
    {
        private const int HistorySize = 60;

        private readonly AquilaService _aquila;
        private readonly DispatcherTimer _clockTimer;
        private bool _suspended;

        public HardwareNodes Aquila => _aquila.State.Hardware;

        [ObservableProperty] private double _cpuGaugeValue;
        [ObservableProperty] private double _ramGaugeValue;
        [ObservableProperty] private float _effectiveCpuClock;

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

        public void Suspend() => _suspended = true;
        public void Resume() => _suspended = false;

        public DashboardViewModel(AquilaService aquila)
        {
            _aquila = aquila;

            _aquila.DataUpdated += OnDataUpdated;
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
            UpdateGpuCards();
            UpdateHistorySeries();
            RefreshMetricLists();
            NotifyDerivedProperties();
        }

        private void UpdateCpuSection()
        {
            CpuGaugeValue = Aquila.Cpu.Load.Value ?? 0;
            EffectiveCpuClock = Aquila.Cpu.Clock.Value ?? 0;
            RamGaugeValue = Math.Round(Aquila.Memory.Load.Value ?? 0);

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

            PushHistorySample(CpuUsageHistory, Aquila.Cpu.Load.Value ?? 0);
            PushHistorySample(RamUsageHistory, Aquila.Memory.Load.Value ?? 0);
            
            var primaryNet = Aquila.NetworkAdapters.FirstOrDefault();
            PushHistorySample(NetworkDownloadHistory, primaryNet?.DownloadSpeed.Value ?? 0);
            PushHistorySample(NetworkUploadHistory, primaryNet?.UploadSpeed.Value ?? 0);
        }

        private void RefreshMetricLists()
        {
            var temps = new List<LabelledMetric>();
            if (Aquila.Motherboard.SystemTemperature.Value.HasValue) temps.Add(new LabelledMetric("System", Aquila.Motherboard.SystemTemperature));
            if (Aquila.Motherboard.VrmTemperature.Value.HasValue) temps.Add(new LabelledMetric("VRM", Aquila.Motherboard.VrmTemperature));
            if (Aquila.Motherboard.ChipsetTemperature.Value.HasValue) temps.Add(new LabelledMetric("Chipset", Aquila.Motherboard.ChipsetTemperature));
            SystemTemperatures = temps;

            var fans = new List<FanMetricItem>();
            foreach (var fan in Aquila.Motherboard.Fans)
            {
                fans.Add(new FanMetricItem(fan.Name, fan));
            }
            SystemFans = fans;

            StorageCards = Aquila.Drives
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
            _aquila.DataUpdated -= OnDataUpdated;
            ApplicationThemeManager.Changed -= OnThemeChanged;
        }
    }

    public sealed class GpuCardData : ObservableObject
    {
        private const int HistorySize = 60;
        private GpuNode _gpu;

        public GpuCardData(GpuNode gpu)
        {
            _gpu = gpu;
            UsageHistory = new ObservableCollection<double>(Enumerable.Repeat(0.0, HistorySize));
        }

        public string Name => _gpu.Name;
        public SensorNode Temperature => _gpu.Temperature;
        public SensorNode Load => _gpu.Load;
        public SensorNode Clock => _gpu.Clock;
        public SensorNode Power => _gpu.Power;
        public FanNode? Fan1 => _gpu.Fans.Count > 0 ? _gpu.Fans[0] : null;
        public FanNode? Fan2 => _gpu.Fans.Count > 1 ? _gpu.Fans[1] : null;
        
        public SensorNode VramUsed => _gpu.VramUsed;
        public SensorNode VramTotal => _gpu.VramTotal;
        public double VramPercent => (_gpu.VramTotal.Value > 0) ? ((_gpu.VramUsed.Value ?? 0) / _gpu.VramTotal.Value.Value) * 100.0 : 0;

        public ObservableCollection<double> UsageHistory { get; }

        public void Update(GpuNode gpu)
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

    public sealed class CoreBarItem(string label, double value)
    {
        private const double MaxHeight = 92.0;
        public string Label => label;
        public double Value => value;
        public double BarHeight => MaxHeight * (value / 100.0);
        public string ValueText => $"{value:F0}%";
    }

    public sealed class LabelledMetric(string label, SensorNode metric)
    {
        public string Label => label;
        public SensorNode Metric => metric;
    }

    public sealed class FanMetricItem(string name, SensorNode metric)
    {
        public string Name => name;
        public SensorNode Metric => metric;
    }

    public sealed class StorageDriveData(StorageNode drive)
    {
        public string Name => drive.Name;
        public SensorNode Temperature => drive.Temperature;
        public SensorNode ReadRate => drive.ReadRate;
        public SensorNode WriteRate => drive.WriteRate;
        public SensorNode UsedSpace => drive.UsedPercent;
    }
}