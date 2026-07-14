using Aquila.Models;
using Aquila.Services;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Aquila.ViewModels.Pages;

/// <summary>
/// Live preview of the single-sensor widget pieces (#23) — one example of each, reading real
/// sensors from the same AquilaService tick every other page already uses. No separate data
/// source: the SensorNodes update themselves via INPC, same as the dashboard cards.
/// </summary>
public partial class WidgetsViewModel : ObservableObject
{
    private readonly AquilaService _aquila;

    public WidgetsViewModel(AquilaService aquila) => _aquila = aquila;

    public HardwareNode Hardware => _aquila.State.Hardware;
}
