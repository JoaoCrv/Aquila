using System.Windows;
using System.Windows.Controls;
using Aquila.Models.Nodes;

namespace Aquila.Controls.Cards;

/// <summary>
/// Self-contained network dashboard card. Give it the <see cref="Network"/> node and it renders
/// everything — adapter name, live down/up throughput, throughput sparkline and total received/sent.
/// Every value binds straight to the SensorNodes, so there is no computed state or ViewModel
/// dependency; it can be dropped into the dashboard or a future widget alike.
/// </summary>
public partial class NetworkCard : UserControl
{
    public NetworkCard() => InitializeComponent();

    public static readonly DependencyProperty NetworkProperty =
        DependencyProperty.Register(nameof(Network), typeof(NetworkNode), typeof(NetworkCard),
            new PropertyMetadata(null));

    /// <summary>The network adapter to display.</summary>
    public NetworkNode? Network
    {
        get => (NetworkNode?)GetValue(NetworkProperty);
        set => SetValue(NetworkProperty, value);
    }
}
