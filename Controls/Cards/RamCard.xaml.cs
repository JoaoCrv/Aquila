using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using Aquila.Models.Nodes;

namespace Aquila.Controls.Cards;

/// <summary>
/// Self-contained RAM dashboard card. Give it the <see cref="Memory"/> node and it renders
/// everything — usage header, the physical/virtual composition bar, legend, usage sparkline and
/// per-DIMM temperatures. The pool fractions that drive the composition bar are computed here, so the
/// card has no ViewModel dependency and can be dropped into the dashboard or a future widget alike.
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

    public static readonly DependencyProperty PhysUsedFracProperty =
        DependencyProperty.Register(nameof(PhysUsedFrac), typeof(double), typeof(RamCard),
            new PropertyMetadata(0.0));

    public static readonly DependencyProperty PhysFreeFracProperty =
        DependencyProperty.Register(nameof(PhysFreeFrac), typeof(double), typeof(RamCard),
            new PropertyMetadata(0.0));

    public static readonly DependencyProperty VirtUsedFracProperty =
        DependencyProperty.Register(nameof(VirtUsedFrac), typeof(double), typeof(RamCard),
            new PropertyMetadata(0.0));

    /// <summary>The memory to display. Drives the whole card.</summary>
    public MemoryNode? Memory
    {
        get => (MemoryNode?)GetValue(MemoryProperty);
        set => SetValue(MemoryProperty, value);
    }

    /// <summary>Physical-used share of the memory pool (0..1), computed each tick.</summary>
    public double PhysUsedFrac
    {
        get => (double)GetValue(PhysUsedFracProperty);
        private set => SetValue(PhysUsedFracProperty, value);
    }

    /// <summary>Physical-free share of the memory pool (0..1), computed each tick.</summary>
    public double PhysFreeFrac
    {
        get => (double)GetValue(PhysFreeFracProperty);
        private set => SetValue(PhysFreeFracProperty, value);
    }

    /// <summary>Virtual-used share of the memory pool (0..1), computed each tick.</summary>
    public double VirtUsedFrac
    {
        get => (double)GetValue(VirtUsedFracProperty);
        private set => SetValue(VirtUsedFracProperty, value);
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

    // Recompute the pool fractions that drive the composition bar. Everything else binds straight to
    // the SensorNodes and updates through their own INPC.
    private void Refresh()
    {
        if (Memory is null)
        {
            PhysUsedFrac = PhysFreeFrac = VirtUsedFrac = 0;
            return;
        }

        double physUsed = Memory.Data.Used.Value ?? 0;
        double physFree = Memory.Data.Available.Value ?? 0;
        double virtUsed = Memory.Virtual.Used.Value ?? 0;
        double virtFree = Memory.Virtual.Available.Value ?? 0;
        double pool = physUsed + physFree + virtUsed + virtFree;

        PhysUsedFrac = pool > 0 ? physUsed / pool : 0;
        PhysFreeFrac = pool > 0 ? physFree / pool : 0;
        VirtUsedFrac = pool > 0 ? virtUsed / pool : 0;
    }
}
