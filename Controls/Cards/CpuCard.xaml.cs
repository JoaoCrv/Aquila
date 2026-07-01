using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Aquila.Models.Nodes;

namespace Aquila.Controls.Cards;

/// <summary>
/// Self-contained CPU dashboard card. Give it the <see cref="Cpu"/> node and it renders everything —
/// header, stat boxes, per-core load and usage sparkline. The live summary and core list are computed
/// here, so the card has no ViewModel dependency and can be dropped into the dashboard or a future
/// widget alike. (Fans live in the dedicated fan card.)
/// </summary>
public partial class CpuCard : UserControl
{
    public CpuCard()
    {
        InitializeComponent();
        Loaded += (_, _) => Refresh();
    }

    public static readonly DependencyProperty CpuProperty =
        DependencyProperty.Register(nameof(Cpu), typeof(CpuNode), typeof(CpuCard),
            new PropertyMetadata(null, OnCpuChanged));

    public static readonly DependencyProperty SummaryProperty =
        DependencyProperty.Register(nameof(Summary), typeof(string), typeof(CpuCard),
            new PropertyMetadata(null));

    public static readonly DependencyProperty CoresProperty =
        DependencyProperty.Register(nameof(Cores), typeof(IReadOnlyList<CoreBarItem>), typeof(CpuCard),
            new PropertyMetadata(null));

    /// <summary>The CPU to display. Drives the whole card.</summary>
    public CpuNode? Cpu
    {
        get => (CpuNode?)GetValue(CpuProperty);
        set => SetValue(CpuProperty, value);
    }

    /// <summary>Live "&lt;temp&gt;°C • &lt;power&gt; W" line, computed from the CPU each tick.</summary>
    public string? Summary
    {
        get => (string?)GetValue(SummaryProperty);
        private set => SetValue(SummaryProperty, value);
    }

    /// <summary>Per-core load bars, computed from the CPU each tick.</summary>
    public IReadOnlyList<CoreBarItem>? Cores
    {
        get => (IReadOnlyList<CoreBarItem>?)GetValue(CoresProperty);
        private set => SetValue(CoresProperty, value);
    }

    private static void OnCpuChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var card = (CpuCard)d;
        // The CPU's total-load sensor updates every tick — use it as the card's heartbeat.
        if (e.OldValue is CpuNode old) old.Load.Total.PropertyChanged -= card.OnTick;
        if (e.NewValue is CpuNode now) now.Load.Total.PropertyChanged += card.OnTick;
        card.Refresh();
    }

    private void OnTick(object? sender, PropertyChangedEventArgs e)
    {
        if (!Dispatcher.CheckAccess()) { Dispatcher.Invoke(Refresh); return; }
        Refresh();
    }

    // Recompute the two live bits that aren't plain sensor bindings. Everything else (stat values,
    // sparkline, fans) binds straight to the SensorNodes and updates through their own INPC.
    private void Refresh()
    {
        if (Cpu is null)
        {
            Summary = null;
            Cores = [];
            return;
        }

        var temp = Cpu.Temperature.Primary.Value;
        var power = Cpu.Power.Package.Value;
        Summary = temp.HasValue && power.HasValue ? $"{temp:F0}°C  •  {power:F0} W" : null;

        Cores = Cpu.Load.Cores
            .Where(c => c.Value.HasValue)
            .Select((c, i) => new CoreBarItem($"#{i + 1}", c.Value ?? 0))
            .ToList();
    }
}

/// <summary>One per-core load bar (label + value), used by the CPU card's core-load row.</summary>
public sealed class CoreBarItem(string label, double value)
{
    private const double MaxHeight = 92.0;
    public string Label     => label;
    public double Value     => value;
    public double BarHeight => MaxHeight * (value / 100.0);
    public string ValueText => $"{value:F0}%";
}
