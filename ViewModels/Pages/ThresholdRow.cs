using Aquila.Models;
using Aquila.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Aquila.ViewModels.Pages;

/// <summary>
/// One family of limits in the settings form — three numbers, edited in one place and in force everywhere
/// that judges a reading of that family.
///
/// The three cannot cross. Rather than correcting the user after the fact, each number's own floor and
/// ceiling follow its neighbours, so an out-of-order state is never reachable: pushing Alert up stops at
/// Critical instead of silently rewriting it. The same approach as the dial's arc rounding, which is capped
/// by its thickness.
/// </summary>
public partial class ThresholdRow : ObservableObject
{
    private readonly VitalMonitor _monitor;
    private readonly MetricKey _key;
    private bool _loading;

    public ThresholdRow(VitalMonitor monitor, MetricKey key)
    {
        _monitor = monitor;
        _key = key;

        (Name, Detail) = Thresholds.Describe(key);
        Load();
    }

    /// <summary>What this family is called in front of a person, e.g. "Processor temperature".</summary>
    public string Name { get; }

    /// <summary>What it covers, so the row explains its own scope without a manual.</summary>
    public string Detail { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AlertMin))]
    private double _elevated;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ElevatedMax))]
    [NotifyPropertyChangedFor(nameof(CriticalMin))]
    private double _alert;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AlertMax))]
    private double _critical;

    /// <summary>Whether this row is running on limits the user set rather than the ones underneath — what
    /// decides if Reset has anything to do.</summary>
    [ObservableProperty]
    private bool _isCustom;

    /// <summary>Said aloud when the numbers underneath came from the hardware itself. A drive's own warning
    /// temperature is a better default than ours, and the user should be able to tell that is what they are
    /// looking at before deciding to overrule it.</summary>
    [ObservableProperty]
    private string? _source;

    // The bounds each number gives its neighbours. A degree apart, so two steps can be adjacent without
    // being the same number — three limits that all read 85 would describe no ramp at all.
    //
    // Lifted while a whole row is being replaced. The three are written one at a time, so a Reset from
    // 30/35/40 back to 75/85/95 would put 75 into Elevated while Alert still said 35 — and the box's own
    // ceiling would refuse it, leaving only the last of the three to survive. Mid-replacement the
    // intermediate states are meaningless, so the bounds step aside until all three have landed.
    public double ElevatedMax => _loading ? Unbounded : Alert - 1;
    public double AlertMin => _loading ? 0 : Elevated + 1;
    public double AlertMax => _loading ? Unbounded : Critical - 1;
    public double CriticalMin => _loading ? 0 : Alert + 1;

    /// <summary>Higher than any temperature or percentage will ever be, which is all "no ceiling" has to
    /// mean here.</summary>
    private const double Unbounded = 10_000;

    partial void OnElevatedChanged(double value) => Apply();
    partial void OnAlertChanged(double value) => Apply();
    partial void OnCriticalChanged(double value) => Apply();

    /// <summary>Back to the built-in limits. Forgets the override rather than storing a copy of the
    /// default, so this family follows the built-in again if it is ever revised.</summary>
    [RelayCommand]
    private void Reset()
    {
        _monitor.Reset(_key);
        Load();
    }

    private void Load()
    {
        _loading = true;
        RaiseBounds();

        // Never null here: the form only builds rows for kinds that have a preset.
        var steps = _monitor.For(_key) ?? Thresholds.Percent;
        Elevated = steps.Elevated;
        Alert = steps.Alert;
        Critical = steps.Critical;
        IsCustom = _monitor.IsCustom(_key);
        Source = _monitor.IsReported(_key) ? "Reported by the hardware" : null;

        _loading = false;
        RaiseBounds();
    }

    /// <summary>Tells the boxes to re-read their limits — on the way into a replacement, so they let go of
    /// them, and on the way out, so they take the new neighbours.</summary>
    private void RaiseBounds()
    {
        OnPropertyChanged(nameof(ElevatedMax));
        OnPropertyChanged(nameof(AlertMin));
        OnPropertyChanged(nameof(AlertMax));
        OnPropertyChanged(nameof(CriticalMin));
    }

    /// <summary>
    /// Writes the three numbers through, which persists them and repaints anything judging this family.
    ///
    /// Suppressed while <see cref="Load"/> is filling the form: each setter fires this, so a Reset would
    /// otherwise write the built-in values straight back as an override and undo itself.
    /// </summary>
    private void Apply()
    {
        if (_loading) return;

        _monitor.Set(_key, new Thresholds(Elevated, Alert, Critical));
        IsCustom = true;
        Source = null;
    }
}
