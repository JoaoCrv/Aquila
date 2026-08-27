using System.Globalization;
using System.Windows;
using System.Windows.Media;
using Aquila.Models;

namespace Aquila.Services;

/// <summary>
/// Where a watched reading's limits live, and the one place that judges a reading against them.
///
/// Two jobs, and they belong together: the limits are the only thing the judgement needs, and every caller
/// that wants one wants the other. Splitting them would mean each surface fetching thresholds and then
/// applying them itself — which is exactly the arrangement that let the title bar and the pressure ribbon
/// disagree about the same sensor.
///
/// What it deliberately does NOT do:
///
/// - It does not aggregate. Taking the worst of several readings and naming it is
///   <see cref="SystemPressure"/>'s job, and "the maximum, never an average" is a decision that deserves
///   its own home rather than being buried in a class about limits.
/// - It does not decide what anything looks like. The colour profile says what "Alert" looks like; this
///   says when a reading is alerting. Keeping them apart is why restyling cannot silently change meaning.
///
/// Later, this is where alerts hang: reacting to a high temperature needs a TRANSITION (normal → alert),
/// not the current state, so it needs somewhere that remembers the previous tick. That is here, and it will
/// need hysteresis when it arrives — a reading oscillating either side of a limit would otherwise fire
/// continuously, which is how a monitor teaches its user to ignore it.
/// </summary>
public sealed class VitalMonitor(SettingsService settings)
{
    /// <summary>
    /// The instance the XAML converter reads, set once by the composition root.
    ///
    /// IntensityBrushConverter is created by XAML and never sees the container, so it cannot be given this
    /// the ordinary way — and without it the cards would keep colouring by the built-in limits while
    /// everything else followed the user's. Same shape as the colour profile, which publishes itself into
    /// Application.Resources for the same reason: XAML needs a global to reach.
    /// </summary>
    public static VitalMonitor? Current { get; set; }

    /// <summary>
    /// Limits the user has set, keyed by what a reading IS — the part and the metric together.
    ///
    /// One entry covers every reading of that kind: setting CPU temperature covers the die on this machine
    /// and on the next, plus every binding in the app that names the same key. And it can say what a single
    /// collapsed "Temperature" never could — that a GPU at 83 °C is ordinary and a CPU at 83 °C is warm.
    ///
    /// Only what was changed is stored, so "never configured" stays distinguishable from "set back to the
    /// default by hand" — which is the distinction Reset needs.
    /// </summary>
    private readonly Dictionary<MetricKey, Thresholds> _custom = [];

    /// <summary>The limits in force for a kind of reading, or null when it has no scale to be judged on.</summary>
    public Thresholds? For(MetricKey key) =>
        _custom.TryGetValue(key, out var custom) ? custom : Thresholds.Preset(key);

    /// <summary>The limits in force for a watched reading.</summary>
    public Thresholds? For(Vital vital) => For(vital.Metric);

    /// <summary>Whether a kind is running on limits the user chose rather than the built-in ones.</summary>
    public bool IsCustom(MetricKey key) => _custom.ContainsKey(key);

    /// <summary>
    /// Raised when a family's limits change.
    ///
    /// Most of the app fixes itself within a second without this, because readings move and moving values
    /// re-run their bindings. Two places do not: a machine at rest leaves the title bar's ribbon and pills
    /// sitting on numbers that never change, wearing the old limits' colours until something disturbs them.
    /// The same reason AppearanceService has to announce a profile change.
    /// </summary>
    public event Action? Changed;

    public void Set(MetricKey key, Thresholds thresholds)
    {
        _custom[key] = thresholds;
        Persist();
        Changed?.Invoke();
    }

    /// <summary>Puts a family back on the built-in limits by forgetting the override, not by copying the
    /// default into it — a stored copy would stop following the built-in if it were ever revised.</summary>
    public void Reset(MetricKey key)
    {
        _custom.Remove(key);
        Persist();
        Changed?.Invoke();
    }

    /// <summary>
    /// Reads the stored limits. Called once at startup, before anything renders.
    ///
    /// A malformed entry is skipped rather than rejected: settings.json is meant to be hand-editable, and a
    /// typo in one family should cost that family its custom limits, not the app its colours.
    /// </summary>
    public void Load()
    {
        _custom.Clear();

        foreach (var (name, text) in settings.Current.Thresholds)
            if (MetricKey.Parse(name) is { } key && Thresholds.Parse(text) is { } parsed)
                _custom[key] = parsed;
    }

    private void Persist()
    {
        // Invariant, always. Thresholds.Parse splits on commas and reads invariant, so writing "75,5" for
        // seventy-five and a half on a Portuguese machine would turn three numbers into four and lose the
        // family on the next load.
        settings.Current.Thresholds = _custom.ToDictionary(
            pair => pair.Key.ToString(),
            pair => string.Format(CultureInfo.InvariantCulture, "{0},{1},{2}",
                pair.Value.Elevated, pair.Value.Alert, pair.Value.Critical));

        settings.Save();
    }

    /// <summary>
    /// Which state a reading is in — Normal, Elevated, Alert or Critical, the names the colour profile
    /// uses for its roles.
    ///
    /// This and <see cref="BrushFor"/> are the whole point of the arrangement: a caller says WHAT it is
    /// reading and HOW MUCH, and never has to know which colour that deserves. Nothing outside this class
    /// should be comparing a reading against a limit.
    /// </summary>
    public string RoleFor(double value, MetricKey key) => For(key)?.Role(value) ?? "Normal";

    /// <summary>
    /// The colour a reading has earned.
    ///
    /// Both halves meet here and nowhere else: this class says WHEN a value is alerting, the colour profile
    /// says what alerting LOOKS like. Neither knows the other's business, which is why restyling cannot
    /// change meaning and a shared profile cannot impose someone else's limits.
    ///
    /// Grey when the profile has not been published yet — a colour that is obviously wrong beats throwing
    /// during a first frame.
    /// </summary>
    public Brush BrushFor(double value, MetricKey key) => Brush(RoleFor(value, key));

    /// <summary>The profile's colour for a role name, without judging anything. For a caller that already
    /// knows the state — a <see cref="VitalReading"/> carries one.</summary>
    public static Brush Brush(string role) =>
        Application.Current?.TryFindResource($"Aquila.Scheme.{role}") as Brush ?? Brushes.Gray;

    /// <summary>
    /// Reads a vital and judges it in one go — the block the title bar's strip used to spell out inline.
    /// </summary>
    public VitalReading Read(Vital vital, HardwareNode hardware)
    {
        var node = vital.Pick(hardware);

        // Absent is not idle: a machine with no discrete GPU has no GPU reading, which is a different
        // statement from one resting at zero, and surfaces need to be able to tell them apart.
        if (node?.Value is not float value) return VitalReading.Absent;

        var scale = For(vital);
        var unit = node.Unit ?? string.Empty;

        return new VitalReading(
            HasValue: true,
            Value: value,
            Unit: unit,
            Text: $"{value:F0}{unit}",
            Level: scale?.Level(value) ?? 0,
            Role: scale?.Role(value) ?? "Normal");
    }
}
