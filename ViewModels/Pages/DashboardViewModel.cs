using Aquila.Models;
using Aquila.Models.Nodes;
using Aquila.Services;
using Aquila.Views.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.DependencyInjection;
using Wpf.Ui.Controls;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Threading;

namespace Aquila.ViewModels.Pages;

public partial class DashboardViewModel : ObservableObject, IDisposable
{
    private readonly AquilaService _aquila;
    private readonly SettingsService _settings;
    private readonly DispatcherTimer _clockTimer;

    public HardwareNode Hardware => _aquila.State.Hardware;

    [ObservableProperty] private double _ramGaugeValue;
    [ObservableProperty] private List<CoreBarItem> _cpuCoreItems = [];
    [ObservableProperty] private List<GpuCardData> _gpuCards = [];
    [ObservableProperty] private List<FanRowItem> _fanRows = [];

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

    public Visibility DashboardControls    => _settings.Current.DashboardMode         ? Visibility.Visible : Visibility.Collapsed;
    public Visibility ShowCpuCard          => _settings.Current.ShowCpuCard          ? Visibility.Visible : Visibility.Collapsed;
    public Visibility ShowMemoryCard       => _settings.Current.ShowMemoryCard       ? Visibility.Visible : Visibility.Collapsed;
    public Visibility ShowNetworkCard      => _settings.Current.ShowNetworkCard      ? Visibility.Visible : Visibility.Collapsed;
    public Visibility ShowTemperaturesCard => _settings.Current.ShowTemperaturesCard ? Visibility.Visible : Visibility.Collapsed;
    public Visibility ShowPowerCard        => _settings.Current.ShowPowerCard        ? Visibility.Visible : Visibility.Collapsed;
    public Visibility ShowFansCard         => _settings.Current.ShowFansCard         ? Visibility.Visible : Visibility.Collapsed;

    public bool IsDashboardWindowVisible =>
        Application.Current.Windows.OfType<DashboardWindow>().Any(w => w.IsVisible);

    public SymbolRegular DashboardToggleIcon =>
        IsDashboardWindowVisible ? SymbolRegular.Dismiss24 : SymbolRegular.ArrowExpand24;

    public string DashboardToggleTooltip =>
        IsDashboardWindowVisible ? "Close dashboard" : "Open dashboard";

    [RelayCommand]
    private void ToggleDashboard()
    {
        var dw = App.Services.GetRequiredService<DashboardWindow>();
        if (dw.IsVisible) dw.Hide(); else dw.Show();
        NotifyDashboardToggle();
    }

    private void NotifyDashboardToggle()
    {
        OnPropertyChanged(nameof(IsDashboardWindowVisible));
        OnPropertyChanged(nameof(DashboardToggleIcon));
        OnPropertyChanged(nameof(DashboardToggleTooltip));
    }

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
        var cpu = Hardware.Cpus.FirstOrDefault();
        RamGaugeValue = Math.Round(Hardware.Memory.Load.Total.Value ?? 0);
        CpuCoreItems = cpu?.Load.Cores
            .Where(c => c.Value.HasValue)
            .Select((c, i) => new CoreBarItem($"#{i + 1}", c.Value ?? 0))
            .ToList() ?? [];

        UpdateGpus();
        UpdateFans();

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
        NotifyDashboardToggle();
    }

    private double MemPool =>
        (Hardware.Memory.Data.Used.Value ?? 0) +
        (Hardware.Memory.Data.Available.Value ?? 0) +
        (Hardware.Memory.Virtual.Used.Value ?? 0) +
        (Hardware.Memory.Virtual.Available.Value ?? 0);

    public double MemPhysUsedFrac => MemPool > 0 ? (Hardware.Memory.Data.Used.Value ?? 0) / MemPool : 0;
    public double MemPhysFreeFrac => MemPool > 0 ? (Hardware.Memory.Data.Available.Value ?? 0) / MemPool : 0;
    public double MemVirtUsedFrac => MemPool > 0 ? (Hardware.Memory.Virtual.Used.Value ?? 0) / MemPool : 0;

    private void UpdateFans()
    {
        var mb = Hardware.Motherboard;
        FanRows = mb.Fan
            .Where(f => f.Value > 0)
            .Select(f => new FanRowItem(f, mb.Control[f.Name ?? string.Empty]))
            .ToList();
    }

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
        OnPropertyChanged(nameof(DashboardControls));
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

public sealed class FanRowItem(SensorNode fan, SensorNode? control)
{
    public SensorNode  Fan        => fan;
    public SensorNode? Control    => control;
    public double      BarValue   => control?.Value ?? fan.Value ?? 0;
    public double      BarMaximum => control != null ? 100 : (fan.Max ?? 3000);

    public string? DutyText
    {
        get
        {
            if (control?.Value is float cv) return $"{cv:F0}%";
            if (fan.Value is float v && fan.Max is float max && max > 0)
                return $"{v / max * 100:F0}%";
            return null;
        }
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
