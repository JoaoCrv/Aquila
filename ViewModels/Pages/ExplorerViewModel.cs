using System.Collections.Generic;
using System.Linq;
using Aquila.Models;
using Aquila.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Wpf.Ui;
using Wpf.Ui.Controls;

namespace Aquila.ViewModels.Pages;

/// <summary>
/// Live view of Aquila's own sensors (from SensorCatalog), grouped by component. Unlike the raw
/// LHM explorer (now under Settings), these are the sensors the app actually models, updating each
/// tick — the source for pinning widgets later.
/// </summary>
public partial class ExplorerViewModel : ObservableObject
{
    private readonly AquilaService _aquila;
    private readonly DesktopWidgetService _widgets;
    private readonly ISnackbarService? _snackbar;

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

    public ExplorerViewModel(AquilaService aquila, DesktopWidgetService widgets, ISnackbarService? snackbar = null)
    {
        _aquila = aquila;
        _widgets = widgets;
        _snackbar = snackbar;
        Refresh();
    }

    /// <summary>
    /// Puts a sensor straight onto the desktop (#24). The widget appears with sensible defaults; moving and
    /// dressing it is edit mode's job, on the Widgets page.
    ///
    /// It is confirmed with a snackbar rather than silently, because the desktop is very likely behind this
    /// window — without a word here, clicking pin would look like it did nothing at all.
    /// </summary>
    [RelayCommand]
    private void Pin(SensorEntry? entry)
    {
        var identifier = entry?.Sensor.Identifier;
        if (string.IsNullOrEmpty(identifier)) return;

        var title = _widgets.PinWidget(identifier);

        _snackbar?.Show(
            title is null ? "Could not pin" : "Pinned to desktop",
            title is null
                ? "That sensor is no longer being reported."
                : $"{title} is now on the desktop. Use Widgets to move or restyle it.",
            title is null ? ControlAppearance.Caution : ControlAppearance.Success,
            new SymbolIcon { Symbol = title is null ? SymbolRegular.Warning24 : SymbolRegular.Pin24 },
            TimeSpan.FromSeconds(5));
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
