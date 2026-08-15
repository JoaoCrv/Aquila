using Aquila.Services;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Aquila.ViewModels.Windows;

/// <summary>
/// The data shown in the title bar.
///
/// Its own view model rather than more properties on <see cref="MainWindowViewModel"/>, because this is
/// the point where the title bar stops being chrome and starts being an instrument. It owns the ember
/// ribbon now and the vitals strip next; that window's view model has no business knowing about sensors.
/// </summary>
public partial class TitleBarViewModel : ObservableObject, IDisposable
{
    private readonly AquilaService _aquila;

    public TitleBarViewModel(AquilaService aquila)
    {
        _aquila = aquila;
        _aquila.DataUpdated += Refresh;
        Refresh();
    }

    /// <summary>
    /// System pressure, 0–100, driving the ribbon's width and its colour.
    ///
    /// Already normalised by <see cref="Models.Thresholds.Level"/>, so it is comparable across units and
    /// carries its own meaning: a third is elevated, two thirds is alert, full is critical. That is why
    /// it is coloured with the Pressure scale rather than judged a second time.
    /// </summary>
    [ObservableProperty]
    private double _pressurePercent;

    /// <summary>Names the reading responsible. The whole reason pressure is a maximum and not an average
    /// is that this sentence can be written at all.</summary>
    [ObservableProperty]
    private string _pressureTooltip = string.Empty;

    private void Refresh()
    {
        // AquilaService ticks on a DispatcherTimer, so this already arrives on the UI thread.
        var pressure = _aquila.Pressure;

        PressurePercent = pressure.Level * 100;
        PressureTooltip = string.IsNullOrEmpty(pressure.Source)
            ? "No sensors reporting"
            : $"{pressure.Source} — {pressure.Value:F0} {pressure.Unit}".TrimEnd();
    }

    public void Dispose() => _aquila.DataUpdated -= Refresh;
}
