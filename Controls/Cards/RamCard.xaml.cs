using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Aquila.Models.Nodes;

namespace Aquila.Controls.Cards;

/// <summary>
/// Self-contained RAM dashboard card. Give it the <see cref="Memory"/> node and it renders
/// everything — usage header, the physical/virtual composition bar (a <see cref="StackedBar"/>),
/// legend, usage sparkline and per-DIMM temperatures. The composition segments are computed here, so
/// the card has no ViewModel dependency and can be dropped into the dashboard or a future widget alike.
/// </summary>
public partial class RamCard : UserControl
{
    public RamCard()
    {
        InitializeComponent();
        Loaded += (_, _) => Refresh();
    }

    public static readonly DependencyProperty MemoryProperty =
        DependencyProperty.Register(nameof(Memory), typeof(MemoryNode), typeof(RamCard),
            new PropertyMetadata(null, OnMemoryChanged));

    public static readonly DependencyProperty SegmentsProperty =
        DependencyProperty.Register(nameof(Segments), typeof(IReadOnlyList<BarSegment>), typeof(RamCard),
            new PropertyMetadata(null));

    /// <summary>The memory to display. Drives the whole card.</summary>
    public MemoryNode? Memory
    {
        get => (MemoryNode?)GetValue(MemoryProperty);
        set => SetValue(MemoryProperty, value);
    }

    /// <summary>Physical/virtual pool composition, computed each tick for the StackedBar.</summary>
    public IReadOnlyList<BarSegment>? Segments
    {
        get => (IReadOnlyList<BarSegment>?)GetValue(SegmentsProperty);
        private set => SetValue(SegmentsProperty, value);
    }

    private static void OnMemoryChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var card = (RamCard)d;
        // The total-load sensor updates every tick — use it as the card's heartbeat.
        if (e.OldValue is MemoryNode old) old.Load.Total.PropertyChanged -= card.OnTick;
        if (e.NewValue is MemoryNode now) now.Load.Total.PropertyChanged += card.OnTick;
        card.Refresh();
    }

    private void OnTick(object? sender, PropertyChangedEventArgs e)
    {
        if (!Dispatcher.CheckAccess()) { Dispatcher.Invoke(Refresh); return; }
        Refresh();
    }

    // Recompute the composition segments. Everything else binds straight to the SensorNodes and
    // updates through their own INPC.
    private void Refresh()
    {
        // TryFindResource needs the control connected to the visual tree; Loaded catches up once it is
        // (same guard as RadialGauge/SensorBar).
        if (!IsLoaded || Memory is null)
        {
            Segments = [];
            return;
        }

        double physUsed = Memory.Data.Used.Value ?? 0;
        double physFree = Memory.Data.Available.Value ?? 0;
        double virtUsed = Memory.Virtual.Used.Value ?? 0;
        double virtFree = Memory.Virtual.Available.Value ?? 0;
        double pool = physUsed + physFree + virtUsed + virtFree;
        if (pool <= 0) { Segments = []; return; }

        var cpu = TryFindResource("Aquila.Cpu") as Brush ?? Brushes.Transparent;
        var ram = TryFindResource("Aquila.Ram") as Brush ?? Brushes.Transparent;

        Segments =
        [
            new(physUsed / pool, cpu),
            new(physFree / pool, cpu, Opacity: 0.18),
            new(virtUsed / pool, ram),
            new(virtFree / pool, ram, Opacity: 0.18),
        ];
    }
}
