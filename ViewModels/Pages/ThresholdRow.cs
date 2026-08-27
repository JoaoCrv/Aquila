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

    /// <summary>Whether this family is running on limits the user set rather than the built-in ones — what
    /// decides if Reset has anything to do.</summary>
    [ObservableProperty]
    private bool _isCustom;

    // The bounds each number gives its neighbours. A degree apart, so two steps can be adjacent without
    // being the same number — three limits that all read 85 would describe no ramp at all.
    public double ElevatedMax => Alert - 1;
    public double AlertMin => Elevated + 1;
    public double AlertMax => Critical - 1;
    public double CriticalMin => Alert + 1;

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

        // Never null here: the form only builds rows for kinds that have a preset.
        var steps = _monitor.For(_key) ?? Thresholds.Percent;
        Elevated = steps.Elevated;
        Alert = steps.Alert;
        Critical = steps.Critical;
        IsCustom = _monitor.IsCustom(_key);

        _loading = false;
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
    }
}
