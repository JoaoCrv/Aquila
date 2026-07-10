using System.Windows;
using System.Windows.Controls;
using Aquila.Models.Nodes;

namespace Aquila.Controls.Cards;

/// <summary>
/// Self-contained storage dashboard card. Bind <see cref="Storage"/> to a <see cref="StorageNode"/>
/// and it renders temp/used stat boxes (reusing <see cref="StatBox"/>), read/write throughput rows
/// with bars, and a read+write I/O sparkline. Pure bindings — no computed state or ViewModel
/// dependency.
/// </summary>
public partial class StorageCard : UserControl
{
    public StorageCard() => InitializeComponent();

    public static readonly DependencyProperty StorageProperty =
        DependencyProperty.Register(nameof(Storage), typeof(StorageNode), typeof(StorageCard),
            new PropertyMetadata(null));

    /// <summary>The storage drive to display. Drives the whole card.</summary>
    public StorageNode? Storage
    {
        get => (StorageNode?)GetValue(StorageProperty);
        set => SetValue(StorageProperty, value);
    }
}
