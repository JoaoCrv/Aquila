using Aquila.Models;
using Aquila.Models.Nodes;
using Aquila.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Threading;

namespace Aquila.ViewModels.Pages;

public partial class DashboardViewModel : ObservableObject, IDisposable
{
    private const int HistorySize = 60;

    private readonly AquilaService _aquila;
    private readonly DispatcherTimer _clockTimer;
    private bool _suspended;

    // ── Acesso directo à árvore — UI pode fazer binding aqui ─────────
    public HardwareNode Hardware => _aquila.State.Hardware;

    // ── Propriedades calculadas para a UI ────────────────────────────
    [ObservableProperty] private double _cpuGaugeValue;
    [ObservableProperty] private double _ramGaugeValue;
    [ObservableProperty] private float _effectiveCpuClock;
    [ObservableProperty] private List<CoreBarItem> _cpuCoreItems = [];
    [ObservableProperty] private List<GpuCardData> _gpuCards = [];

    [ObservableProperty]
    private SolidColorPaint _gaugeLabelPaint = CreateLabelPaint();

    // ── Histórico para gráficos ───────────────────────────────────────
    public ObservableCollection<double> CpuHistory { get; } = History();
    public ObservableCollection<double> RamHistory { get; } = History();
    public ObservableCollection<double> NetDownHistory { get; } = History();
    public ObservableCollection<double> NetUpHistory { get; } = History();

    // ── Atalhos para GPU ─────────────────────────────────────────────
    public GpuCardData? Gpu1 => GpuCards.Count > 0 ? GpuCards[0] : null;
    public GpuCardData? Gpu2 => GpuCards.Count > 1 ? GpuCards[1] : null;

    // ── Propriedades derivadas ────────────────────────────────────────
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

    // ── Controlo de suspensão (navegação) ────────────────────────────
    public void Suspend() => _suspended = true;
    public void Resume() => _suspended = false;

    // ── Construtor ───────────────────────────────────────────────────
    public DashboardViewModel(AquilaService aquila)
    {
        _aquila = aquila;
        _aquila.DataUpdated += OnDataUpdated;

        Wpf.Ui.Appearance.ApplicationThemeManager.Changed += OnThemeChanged;

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

    // ── Poll ─────────────────────────────────────────────────────────
    private void OnDataUpdated()
    {
        if (_suspended) return;

        UpdateCpu();
        UpdateGpus();
        UpdateHistory();

        OnPropertyChanged(nameof(FreeBarWeight));
        OnPropertyChanged(nameof(CpuSummary));
        OnPropertyChanged(nameof(Gpu1));
        OnPropertyChanged(nameof(Gpu2));
    }

    private void UpdateCpu()
    {
        var cpu = Hardware.Cpus.FirstOrDefault();
        if (cpu is null) return;

        CpuGaugeValue = cpu.Load.Total.Value ?? 0;
        EffectiveCpuClock = cpu.Clock.CoresAverage.Value ?? 0;
        RamGaugeValue = Math.Round(Hardware.Memory.Load.Total.Value ?? 0);

        CpuCoreItems = cpu.Load.Cores
            .Where(c => c.Value.HasValue)
            .Select((c, i) => new CoreBarItem($"#{i + 1}", c.Value ?? 0))
            .ToList();
    }

    private void UpdateGpus()
    {
        var cards = new List<GpuCardData>();
        foreach (var gpu in Hardware.Gpus)
        {
            var existing = GpuCards.FirstOrDefault(c => c.Name == gpu.Name);
            if (existing is not null)
            {
                existing.Refresh();
                cards.Add(existing);
            }
            else
            {
                cards.Add(new GpuCardData(gpu));
            }
        }
        GpuCards = cards;
    }

    private void UpdateHistory()
    {
        var cpu = Hardware.Cpus.FirstOrDefault();
        Push(CpuHistory, cpu?.Load.Total.Value ?? 0);
        Push(RamHistory, Hardware.Memory.Load.Total.Value ?? 0);

        // agrega todas as interfaces activas
        Push(NetDownHistory, Hardware.Networks.Sum(n => n.Throughput.Download.Value ?? 0));
        Push(NetUpHistory, Hardware.Networks.Sum(n => n.Throughput.Upload.Value ?? 0));

        foreach (var card in GpuCards)
            card.PushHistory();
    }

    // ── Helpers ──────────────────────────────────────────────────────
    private static ObservableCollection<double> History()
        => new(Enumerable.Repeat(0.0, HistorySize));

    private static void Push(ObservableCollection<double> history, double value)
    {
        history.RemoveAt(0);
        history.Add(value);
    }

    private static bool IsLight
        => Wpf.Ui.Appearance.ApplicationThemeManager.GetAppTheme()
        == Wpf.Ui.Appearance.ApplicationTheme.Light;

    private static SolidColorPaint CreateLabelPaint() => new(
        IsLight ? SKColors.Black : SKColors.White)
    { SKTypeface = SKTypeface.FromFamilyName("Segoe UI") };

    private void OnThemeChanged(
        Wpf.Ui.Appearance.ApplicationTheme _,
        System.Windows.Media.Color __)
        => Application.Current?.Dispatcher.Invoke(()
            => GaugeLabelPaint = CreateLabelPaint());

    public void Dispose()
    {
        _clockTimer.Stop();
        _aquila.DataUpdated -= OnDataUpdated;
        Wpf.Ui.Appearance.ApplicationThemeManager.Changed -= OnThemeChanged;
    }
}

// ── GpuCardData ──────────────────────────────────────────────────────
public sealed class GpuCardData(GpuNode gpu) : ObservableObject
{
    private const int HistorySize = 60;

    // referência directa aos SensorNodes — o binding funciona
    // automaticamente via INotifyPropertyChanged do SensorNode
    public string? Name => gpu.Name;
    public SensorNode Temperature => gpu.Temperature.Primary;
    public SensorNode Hotspot => gpu.Temperature.Secondary;
    public SensorNode Load => gpu.Load.Core;
    public SensorNode Clock => gpu.Clock.Core;
    public SensorNode Power => gpu.Power.Package;
    public SensorNode Fan1 => gpu.Fan.Primary;
    public SensorNode Fan2 => gpu.Fan.Secondary;
    public SensorNode VramUsed => gpu.Data.Used;
    public SensorNode VramTotal => gpu.Data.Total;

    public double VramPercent
    {
        get
        {
            var used = VramUsed.Value ?? 0;
            var total = VramTotal.Value ?? 0;
            return total > 0 ? used / total * 100.0 : 0;
        }
    }

    public ObservableCollection<double> UsageHistory { get; }
        = new(Enumerable.Repeat(0.0, HistorySize));

    // chamado quando o gpu node é o mesmo mas os valores mudaram
    public void Refresh()
        => OnPropertyChanged(nameof(VramPercent));

    public void PushHistory()
    {
        UsageHistory.RemoveAt(0);
        UsageHistory.Add(Load.Value ?? 0);
    }
}

// ── CoreBarItem ───────────────────────────────────────────────────────
public sealed class CoreBarItem(string label, double value)
{
    private const double MaxHeight = 92.0;
    public string Label => label;
    public double Value => value;
    public double BarHeight => MaxHeight * (value / 100.0);
    public string ValueText => $"{value:F0}%";
}