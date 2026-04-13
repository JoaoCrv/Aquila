using Aquila.Helpers;
using Aquila.Models.Api;
using Aquila.Services;
using LiveChartsCore.SkiaSharpView.Painting;
using Microsoft.Win32;
using SkiaSharp;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
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

        public string? CpuSummary => CpuTemperature?.Value is float temp && CpuPower?.Value is float power
            ? $"{temp:F0}°C • {power:F0} W"
            : null;

        public SensorNode? CpuTemperature => AquilaSensorSelector.FindCpuTemperature(Aquila.Cpu);
        public SensorNode? CpuPower => AquilaSensorSelector.FindCpuPower(Aquila.Cpu);
        public SensorNode? CpuFan1 => AquilaSensorSelector.FindCpuPrimaryFan(Aquila.Motherboard);
        public SensorNode? CpuFan2 => AquilaSensorSelector.FindCpuSecondaryFan(Aquila.Motherboard);

        public SensorNode? MemoryUsed => AquilaSensorSelector.FindMemoryUsed(Aquila.Memory);
        public SensorNode? MemoryAvailable => AquilaSensorSelector.FindMemoryAvailable(Aquila.Memory);
        public SensorNode? MemoryTotal => AquilaSensorSelector.FindMemoryTotal(Aquila.Memory);
        public double MemoryTotalVisibleGb => MemoryTotal?.Value ?? ((MemoryUsed?.Value ?? 0) + (MemoryAvailable?.Value ?? 0));
        public double MemoryCacheGb => AquilaSensorSelector.FindMemoryCache(Aquila.Memory)?.Value ?? 0;
        public SensorNode? MemoryPageReadsPerSec => AquilaSensorSelector.FindMemoryPageReads(Aquila.Memory);
        public SensorNode? MemoryPageWritesPerSec => AquilaSensorSelector.FindMemoryPageWrites(Aquila.Memory);
        public SensorNode? MemoryPower => AquilaSensorSelector.FindMemoryPower(Aquila.Memory);

        public NetworkNode? PrimaryNetwork => Aquila.NetworkAdapters.FirstOrDefault();
        public string PrimaryNetworkName => PrimaryNetwork?.Name ?? "Adapter";
        public SensorNode? NetworkDownloadSpeed => PrimaryNetwork is null ? null : AquilaSensorSelector.FindNetworkDownload(PrimaryNetwork);
        public SensorNode? NetworkUploadSpeed => PrimaryNetwork is null ? null : AquilaSensorSelector.FindNetworkUpload(PrimaryNetwork);
        public SensorNode? NetworkDataDownloaded => PrimaryNetwork is null ? null : AquilaSensorSelector.FindNetworkDataDownloaded(PrimaryNetwork);
        public SensorNode? NetworkDataUploaded => PrimaryNetwork is null ? null : AquilaSensorSelector.FindNetworkDataUploaded(PrimaryNetwork);

        public double TotalPowerValue => (CpuPower?.Value ?? 0) + (MemoryPower?.Value ?? 0) + GpuCards.Sum(card => card.Power?.Value ?? 0);

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
            RefreshMetricLists();
            NotifyDerivedProperties();
        }

        private void UpdateCpuSection()
        {
            var totalLoad = AquilaSensorSelector.FindCpuTotalLoad(Aquila.Cpu);
            CpuGaugeValue = totalLoad?.Value ?? 0;

            var avgClock = AquilaSensorSelector.FindCpuEffectiveClock(Aquila.Cpu);
            EffectiveCpuClock = avgClock?.Value ?? 0;

            var ramLoad = AquilaSensorSelector.FindMemoryLoad(Aquila.Memory);
            RamGaugeValue = Math.Round(ramLoad?.Value ?? 0);

            CpuCoreItems = AquilaSensorSelector.FindCpuCoreLoads(Aquila.Cpu)
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

            var totalLoad = AquilaSensorSelector.FindCpuTotalLoad(Aquila.Cpu);
            PushHistorySample(CpuUsageHistory, totalLoad?.Value ?? 0);

            var ramLoad = AquilaSensorSelector.FindMemoryLoad(Aquila.Memory);
            PushHistorySample(RamUsageHistory, ramLoad?.Value ?? 0);

            var primaryNet = Aquila.NetworkAdapters.FirstOrDefault();
            var netDown = primaryNet is null ? null : AquilaSensorSelector.FindNetworkDownload(primaryNet);
            var netUp = primaryNet is null ? null : AquilaSensorSelector.FindNetworkUpload(primaryNet);

            PushHistorySample(NetworkDownloadHistory, netDown?.Value ?? 0);
            PushHistorySample(NetworkUploadHistory, netUp?.Value ?? 0);
        }

        private void RefreshMetricLists()
        {
            SystemTemperatures = AquilaSensorSelector.GetMotherboardTemperatures(Aquila.Motherboard)
                .Select(sensor => new LabelledMetric(sensor.Name, sensor))
                .ToList();

            SystemFans = AquilaSensorSelector.GetMotherboardFans(Aquila.Motherboard)
                .Select(sensor => new FanMetricItem(sensor.Name, sensor))
                .ToList();

            StorageCards = Aquila.Drives
                .Select(drive => new StorageDriveData(drive))
                .ToList();
        }

        private void NotifyDerivedProperties()
        {
            OnPropertyChanged(nameof(CacheBarWeight));
            OnPropertyChanged(nameof(FreeBarWeight));
            OnPropertyChanged(nameof(CpuSummary));
            OnPropertyChanged(nameof(CpuTemperature));
            OnPropertyChanged(nameof(CpuPower));
            OnPropertyChanged(nameof(CpuFan1));
            OnPropertyChanged(nameof(CpuFan2));
            OnPropertyChanged(nameof(MemoryUsed));
            OnPropertyChanged(nameof(MemoryAvailable));
            OnPropertyChanged(nameof(MemoryTotal));
            OnPropertyChanged(nameof(MemoryTotalVisibleGb));
            OnPropertyChanged(nameof(MemoryCacheGb));
            OnPropertyChanged(nameof(MemoryPageReadsPerSec));
            OnPropertyChanged(nameof(MemoryPageWritesPerSec));
            OnPropertyChanged(nameof(MemoryPower));
            OnPropertyChanged(nameof(PrimaryNetwork));
            OnPropertyChanged(nameof(PrimaryNetworkName));
            OnPropertyChanged(nameof(NetworkDownloadSpeed));
            OnPropertyChanged(nameof(NetworkUploadSpeed));
            OnPropertyChanged(nameof(NetworkDataDownloaded));
            OnPropertyChanged(nameof(NetworkDataUploaded));
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
        private GpuNode _gpu;

        public GpuCardData(GpuNode gpu)
        {
            _gpu = gpu;
            UsageHistory = new ObservableCollection<double>(Enumerable.Repeat(0.0, HistorySize));
        }

        public string Name => _gpu.Name;
        public SensorNode? Temperature => AquilaSensorSelector.FindGpuTemperature(_gpu);
        public SensorNode? Load => AquilaSensorSelector.FindGpuLoad(_gpu);
        public SensorNode? Clock => AquilaSensorSelector.FindGpuCoreClock(_gpu);
        public SensorNode? Power => AquilaSensorSelector.FindGpuPower(_gpu);
        public FanNode? Fan1 => _gpu.Fans.Count > 0 ? _gpu.Fans[0] : null;
        public FanNode? Fan2 => _gpu.Fans.Count > 1 ? _gpu.Fans[1] : null;

        public SensorNode? VramUsed => AquilaSensorSelector.FindGpuVramUsed(_gpu);
        public SensorNode? VramTotal => AquilaSensorSelector.FindGpuVramTotal(_gpu);
        public SensorNode? HotspotTemperature => AquilaSensorSelector.FindGpuHotspotTemperature(_gpu);

        public double VramPercent => (VramTotal != null && VramTotal.Value > 0 && VramUsed != null && VramUsed.Value.HasValue)
                                     ? ((VramUsed.Value.Value) / VramTotal.Value.Value) * 100.0 : 0;

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
        public SensorNode? Temperature => AquilaSensorSelector.FindStorageTemperature(drive);
        public SensorNode? ReadRate => AquilaSensorSelector.FindStorageReadRate(drive);
        public SensorNode? WriteRate => AquilaSensorSelector.FindStorageWriteRate(drive);
        public SensorNode? UsedSpace => AquilaSensorSelector.FindStorageUsedSpace(drive);
    }
}