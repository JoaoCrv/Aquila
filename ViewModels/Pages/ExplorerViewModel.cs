using System.Collections.Generic;
using System.Linq;
using Aquila.Models;
using Aquila.Services;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Aquila.ViewModels.Pages;

/// <summary>
/// Live view of Aquila's own sensors (from SensorCatalog), grouped by component. Unlike the raw
/// LHM explorer (now under Settings), these are the sensors the app actually models, updating each
/// tick — the source for pinning widgets later.
/// </summary>
public partial class ExplorerViewModel : ObservableObject
{
    private readonly AquilaService _aquila;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FilteredComponents))]
    private IReadOnlyList<SensorComponent> _components = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FilteredComponents))]
    [NotifyPropertyChangedFor(nameof(IsFiltering))]
    private string _searchText = string.Empty;

    /// <summary>Drives the components open while a search is active: what survives a filter is what the
    /// user is looking for, so making them open it by hand would be busywork. Clearing the box closes them
    /// again.</summary>
    public bool IsFiltering => !string.IsNullOrWhiteSpace(SearchText);

    public ExplorerViewModel(AquilaService aquila)
    {
        _aquila = aquila;
        Refresh();
    }

    // Build the component/sensor list once. The SensorNodes are live objects, so their values keep
    // updating through INPC without rebuilding the list each tick. Call again only if the set of
    // sensors changes (e.g. hardware added) — revisit if needed.
    public void Refresh() =>
        Components = SensorCatalog.GetComponents(_aquila.State.Hardware);

    public IEnumerable<SensorComponent> FilteredComponents
    {
        get
        {
            if (string.IsNullOrWhiteSpace(SearchText))
                return Components;

            return Components
                .Select(c => new SensorComponent(
                    c.Name,
                    c.Sensors
                        .Where(s => s.Label.Contains(SearchText, System.StringComparison.OrdinalIgnoreCase)
                                 || c.Name.Contains(SearchText, System.StringComparison.OrdinalIgnoreCase))
                        .ToList()))
                .Where(c => c.Sensors.Count > 0)
                .ToList();
        }
    }
}
