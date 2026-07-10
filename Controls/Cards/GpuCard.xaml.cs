using System.Windows;
using System.Windows.Controls;
using Aquila.Models.Nodes;

namespace Aquila.Controls.Cards;

/// <summary>
/// Self-contained GPU dashboard card. Bind <see cref="Gpu"/> to a <see cref="GpuNode"/> and it
/// renders usage/temp/clock/power stat boxes (reusing <see cref="StatBox"/>), a usage sparkline,
/// VRAM usage and fan speeds. Pure bindings — no computed state or ViewModel dependency.
/// </summary>
public partial class GpuCard : UserControl
{
    public GpuCard() => InitializeComponent();

    public static readonly DependencyProperty GpuProperty =
        DependencyProperty.Register(nameof(Gpu), typeof(GpuNode), typeof(GpuCard),
            new PropertyMetadata(null));

    /// <summary>The GPU to display. Drives the whole card.</summary>
    public GpuNode? Gpu
    {
        get => (GpuNode?)GetValue(GpuProperty);
        set => SetValue(GpuProperty, value);
    }
}
