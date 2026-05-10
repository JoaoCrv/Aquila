using Aquila.Models;
using Aquila.Models.Nodes;
using Aquila.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Threading;

namespace Aquila.ViewModels.Pages;

public partial class DashboardViewModel : ObservableObject, IDisposable
{
    private const int HistorySize = 60;

    private readonly AquilaService _aquila;
    private readonly DispatcherTimer _clockTimer;
    private bool _suspended;

    public HardwareNode Hardware => _aquila.State.Hardware;

    [ObservableProperty] private double _ramGaugeValue;
    [ObservableProperty] private List<CoreBarItem> _cpuCoreItems = [];
    [ObservableProperty] private List<GpuCardData> _gpuCards = [];

    public ObservableCollection<double> CpuHistory     { get; } = History();
    public ObservableCollection<double> RamHistory     { get; } = History();
    public ObservableCollection<double> NetDownHistory { get; } = History();
    public ObservableCollection<double> NetUpHistory   { get; } = History();

    public GpuCardData? Gpu1 => GpuCards.Count > 0 ? GpuCards[0] : null;
    public GpuCardData? Gpu2 => GpuCards.Count > 1 ? GpuCards[1] : null;

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

    public void Suspend() => _suspended = true;
    public void Resume() => _suspended = false;

    public DashboardViewModel(AquilaService aquila)
    {
        _aquila = aquila;
        _aquila.DataUpdated += OnDataUpdated;

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
        UpdateHistory();

        OnPropertyChanged(nameof(CpuSummary));
        OnPropertyChanged(nameof(Gpu1));
        OnPropertyChanged(nameof(Gpu2));
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

    private void UpdateHistory()
    {
        var cpu = Hardware.Cpus.FirstOrDefault();
        Push(CpuHistory,     cpu?.Load.Total.Value ?? 0);
        Push(RamHistory,     Hardware.Memory.Load.Total.Value ?? 0);
        Push(NetDownHistory, Hardware.Networks.Sum(n => n.Throughput.Download.Value ?? 0));
        Push(NetUpHistory,   Hardware.Networks.Sum(n => n.Throughput.Upload.Value ?? 0));

        foreach (var card in GpuCards)
            card.PushHistory();
    }

    private static ObservableCollection<double> History()
        => new(Enumerable.Repeat(0.0, HistorySize));

    private static void Push(ObservableCollection<double> history, double value)
    {
        history.RemoveAt(0);
        history.Add(value);
    }

    public void Dispose()
    {
        _clockTimer.Stop();
        _aquila.DataUpdated -= OnDataUpdated;
    }
}

public sealed class GpuCardData(GpuNode gpu)
{
    private const int HistorySize = 60;

    public string? Name      => gpu.Name;
    public SensorNode Temperature => gpu.Temperature.Primary;
    public SensorNode Load        => gpu.Load.Core;
    public SensorNode Clock       => gpu.Clock.Core;
    public SensorNode Power       => gpu.Power.Package;
    public SensorNode Fan1        => gpu.Fan.Primary;
    public SensorNode Fan2        => gpu.Fan.Secondary;
    public SensorNode VramUsed    => gpu.Data.Used;
    public SensorNode VramTotal   => gpu.Data.Total;

    public ObservableCollection<double> UsageHistory { get; }
        = new(Enumerable.Repeat(0.0, HistorySize));

    public void PushHistory()
    {
        UsageHistory.RemoveAt(0);
        UsageHistory.Add(Load.Value ?? 0);
    }
}

public sealed class CoreBarItem(string label, double value)
{
    private const double MaxHeight = 92.0;
    public string Label      => label;
    public double Value      => value;
    public double BarHeight  => MaxHeight * (value / 100.0);
    public string ValueText  => $"{value:F0}%";
}
