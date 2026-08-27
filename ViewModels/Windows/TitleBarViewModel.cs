using System.Collections.ObjectModel;
using Aquila.Models;
using Aquila.Services;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Aquila.ViewModels.Windows;

/// <summary>
/// One reading in the title bar's vitals strip.
///
/// <see cref="Percent"/> and <see cref="Level"/> look alike and are not: the first is the raw reading, so
/// the bar's length matches the number printed above it, and the second is that reading normalised
/// against its own thresholds, so the colour means the same on a load as on a temperature. Two channels
/// carrying two different things — a bar whose length disagreed with its own number would be the exact
/// confusion the power card had.
/// </summary>
public sealed partial class VitalItem(string label) : ObservableObject
{
    public string Label { get; } = label;

    [ObservableProperty] private string _text = "--";
    [ObservableProperty] private double _percent;
    [ObservableProperty] private double _level;
    [ObservableProperty] private bool _hasValue;

    /// <summary>
    /// Re-raises the properties that feed a brush, so the colour is worked out again.
    ///
    /// Needed because IntensityBrushConverter resolves the brush when it converts and hands back a fixed
    /// one; the binding only runs again when its source value changes. A reading that is standing still —
    /// an integrated GPU parked at 0% — would keep the previous profile's colour indefinitely.
    /// </summary>
    public void Repaint() => OnPropertyChanged(nameof(Level));
}

/// <summary>
/// The data shown in the title bar.
///
/// Its own view model rather than more properties on <see cref="MainWindowViewModel"/>, because this is
/// the point where the title bar stops being chrome and starts being an instrument. That window's view
/// model has no business knowing about sensors.
/// </summary>
public partial class TitleBarViewModel : ObservableObject, IDisposable
{
    private readonly AquilaService _aquila;
    private readonly AppearanceService _appearance;
    private readonly VitalMonitor _vitals;

    /// <summary>
    /// What the strip shows. Four of the catalog's readings, because this is the most contested space in
    /// the window and the vitals are the first thing to hide when it narrows.
    ///
    /// WHICH four is a layout decision and stays here; what each one reads and how it is judged comes from
    /// the catalog, so this strip and the pressure ribbon beside it cannot disagree about the same sensor.
    /// </summary>
    private static readonly IReadOnlyList<Vital> _specs =
        VitalCatalog.Pick("cpu.load", "gpu.load", "memory.load", "cpu.temp");

    public ObservableCollection<VitalItem> Vitals { get; } = [];

    public TitleBarViewModel(AquilaService aquila, AppearanceService appearance, VitalMonitor vitals)
    {
        _aquila = aquila;
        _appearance = appearance;
        _vitals = vitals;

        foreach (var spec in _specs) Vitals.Add(new VitalItem(spec.Short));

        _aquila.DataUpdated += Refresh;
        _appearance.Changed += Repaint;
        _vitals.Changed += Repaint;
        Refresh();
    }

    /// <summary>
    /// Forces every colour here to be worked out again after the profile or theme changed.
    ///
    /// Elsewhere in the app this fixes itself within a second, because the readings move and moving
    /// values re-run their bindings. Up here two of them do not: a machine at rest leaves the ribbon and
    /// the quieter pills sitting on numbers that never change, wearing the old profile's colours until
    /// something happens to disturb them.
    /// </summary>
    private void Repaint()
    {
        OnPropertyChanged(nameof(PressurePercent));
        foreach (var item in Vitals) item.Repaint();
    }

    /// <summary>
    /// System pressure, 0–100, driving the ribbon's width and its colour.
    ///
    /// Already normalised by <see cref="Thresholds.Level"/>, so it is comparable across units and carries
    /// its own meaning: a third is elevated, two thirds is alert, full is critical. That is why it is
    /// coloured with the Pressure scale rather than judged a second time.
    /// </summary>
    [ObservableProperty]
    private double _pressurePercent;

    /// <summary>Names the reading responsible. The whole reason pressure is a maximum and not an average
    /// is that this sentence can be written at all — and it is what keeps the ribbon honest when the
    /// sensor behind it, such as GPU temperature, has no pill of its own in the strip.</summary>
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

        var hardware = _aquila.State.Hardware;

        // Read and judged by the monitor; this only copies the answer onto the pills. A machine with no
        // discrete GPU reports HasValue false, and that pill hides rather than resting at zero.
        for (var i = 0; i < _specs.Count; i++)
        {
            var reading = _vitals.Read(_specs[i], hardware);
            var item = Vitals[i];

            item.HasValue = reading.HasValue;
            if (!reading.HasValue) continue;

            item.Text = reading.Text;
            item.Percent = Math.Clamp(reading.Value, 0, 100);
            item.Level = reading.Percent;
        }
    }

    public void Dispose()
    {
        _aquila.DataUpdated -= Refresh;
        _appearance.Changed -= Repaint;
        _vitals.Changed -= Repaint;
    }
}
