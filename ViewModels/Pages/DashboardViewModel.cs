using Aquila.Models;
using Aquila.Models.Nodes;
using Aquila.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Threading;
using System.Windows;

namespace Aquila.ViewModels.Pages;

public partial class DashboardViewModel : ObservableObject, IDisposable
{
    private readonly AquilaService _aquila;
    private readonly SettingsService _settings;
    private readonly DispatcherTimer _clockTimer;
    private bool _suspended;

    public HardwareNode Hardware => _aquila.State.Hardware;

    [ObservableProperty] private double _ramGaugeValue;
    [ObservableProperty] private List<CoreBarItem> _cpuCoreItems = [];
    [ObservableProperty] private List<GpuCardData> _gpuCards = [];

    private bool GpuVisible     => _settings.Current.ShowGpuCard;
    private bool StorageVisible => _settings.Current.ShowStorageCard;

    public GpuCardData? Gpu1 => GpuVisible && GpuCards.Count > 0 ? GpuCards[0] : null;
    public GpuCardData? Gpu2 => GpuVisible && GpuCards.Count > 1 ? GpuCards[1] : null;

    public StorageNode? Storage1 => StorageVisible && Hardware.Storages.Count > 0 ? Hardware.Storages[0] : null;
    public StorageNode? Storage2 => StorageVisible && Hardware.Storages.Count > 1 ? Hardware.Storages[1] : null;
    public StorageNode? Storage3 => StorageVisible && Hardware.Storages.Count > 2 ? Hardware.Storages[2] : null;
    public StorageNode? Storage4 => StorageVisible && Hardware.Storages.Count > 3 ? Hardware.Storages[3] : null;

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

    public string? CpuSummary
    {
        get
        {
            var cpu = Hardware.Cpus.FirstOrDefault();
            var temp = cpu?.Temperature.Primary.Value;
            var power = cpu?.Power.Package.Value;
            return temp.HasValue && power.HasValue
                ? $"{temp:F0}°C  •  {power:F0} W"
                : null;
        }
    }

    public Visibility ShowCpuCard          => _settings.Current.ShowCpuCard          ? Visibility.Visible : Visibility.Collapsed;
    public Visibility ShowMemoryCard       => _settings.Current.ShowMemoryCard       ? Visibility.Visible : Visibility.Collapsed;
    public Visibility ShowNetworkCard      => _settings.Current.ShowNetworkCard      ? Visibility.Visible : Visibility.Collapsed;
    public Visibility ShowTemperaturesCard => _settings.Current.ShowTemperaturesCard ? Visibility.Visible : Visibility.Collapsed;
    public Visibility ShowPowerCard        => _settings.Current.ShowPowerCard        ? Visibility.Visible : Visibility.Collapsed;
    public Visibility ShowFansCard         => _settings.Current.ShowFansCard         ? Visibility.Visible : Visibility.Collapsed;

    public void Suspend() => _suspended = true;
    public void Resume() => _suspended = false;

    public DashboardViewModel(AquilaService aquila, SettingsService settings)
    {
        _aquila = aquila;
        _settings = settings;
        _aquila.DataUpdated += OnDataUpdated;
        _settings.Changed += OnSettingsChanged;

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

    private void OnDataUpdated()
    {
        if (_suspended) return;

        var cpu = Hardware.Cpus.FirstOrDefault();
        RamGaugeValue = Math.Round(Hardware.Memory.Load.Total.Value ?? 0);
        CpuCoreItems = cpu?.Load.Cores
            .Where(c => c.Value.HasValue)
            .Select((c, i) => new CoreBarItem($"#{i + 1}", c.Value ?? 0))
            .ToList() ?? [];

        UpdateGpus();

        OnPropertyChanged(nameof(CpuSummary));
        OnPropertyChanged(nameof(Gpu1));
        OnPropertyChanged(nameof(Gpu2));
        OnPropertyChanged(nameof(Storage1));
        OnPropertyChanged(nameof(Storage2));
        OnPropertyChanged(nameof(Storage3));
        OnPropertyChanged(nameof(Storage4));
        OnPropertyChanged(nameof(MemPhysUsedFrac));
        OnPropertyChanged(nameof(MemPhysFreeFrac));
        OnPropertyChanged(nameof(MemVirtUsedFrac));
    }

    private double MemPool =>
        (Hardware.Memory.Data.Used.Value ?? 0) +
        (Hardware.Memory.Data.Available.Value ?? 0) +
        (Hardware.Memory.Virtual.Used.Value ?? 0) +
        (Hardware.Memory.Virtual.Available.Value ?? 0);

    public double MemPhysUsedFrac => MemPool > 0 ? (Hardware.Memory.Data.Used.Value ?? 0) / MemPool : 0;
    public double MemPhysFreeFrac => MemPool > 0 ? (Hardware.Memory.Data.Available.Value ?? 0) / MemPool : 0;
    public double MemVirtUsedFrac => MemPool > 0 ? (Hardware.Memory.Virtual.Used.Value ?? 0) / MemPool : 0;

    private void UpdateGpus()
    {
        var cards = new List<GpuCardData>();
        foreach (var gpu in Hardware.Gpus)
        {
            var existing = GpuCards.FirstOrDefault(c => c.Name == gpu.Name);
            cards.Add(existing ?? new GpuCardData(gpu));
        }
        GpuCards = cards;
    }

    private void OnSettingsChanged()
    {
        OnPropertyChanged(nameof(ShowCpuCard));
        OnPropertyChanged(nameof(ShowMemoryCard));
        OnPropertyChanged(nameof(ShowNetworkCard));
        OnPropertyChanged(nameof(ShowTemperaturesCard));
        OnPropertyChanged(nameof(ShowPowerCard));
        OnPropertyChanged(nameof(ShowFansCard));
        OnPropertyChanged(nameof(Gpu1));
        OnPropertyChanged(nameof(Gpu2));
        OnPropertyChanged(nameof(Storage1));
        OnPropertyChanged(nameof(Storage2));
        OnPropertyChanged(nameof(Storage3));
        OnPropertyChanged(nameof(Storage4));
    }

    public void Dispose()
    {
        _clockTimer.Stop();
        _aquila.DataUpdated -= OnDataUpdated;
        _settings.Changed -= OnSettingsChanged;
    }
}

public sealed class GpuCardData(GpuNode gpu)
{
    public string?    Name        => gpu.Name;
    public SensorNode Temperature => gpu.Temperature.Primary;
    public SensorNode Load        => gpu.Load.Core;
    public SensorNode Clock       => gpu.Clock.Core;
    public SensorNode Power       => gpu.Power.Package;
    public SensorNode Fan1        => gpu.Fan.Primary;
    public SensorNode Fan2        => gpu.Fan.Secondary;
    public SensorNode VramUsed    => gpu.Data.Used;
    public SensorNode VramTotal   => gpu.Data.Total;
}

public sealed class CoreBarItem(string label, double value)
{
    private const double MaxHeight = 92.0;
    public string Label      => label;
    public double Value      => value;
    public double BarHeight  => MaxHeight * (value / 100.0);
    public string ValueText  => $"{value:F0}%";
}
