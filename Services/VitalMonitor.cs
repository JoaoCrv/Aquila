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
    /// Limits the user has set, keyed by FAMILY — "Temperature", "Percent" — not by individual reading.
    ///
    /// That is the whole point of one place: a family named once covers the CPU's temperature, the GPU's,
    /// and the eleven XAML bindings that pass the same name to IntensityBrushConverter. Keyed per reading,
    /// changing "temperature" would have meant editing each one and would still have missed every card.
    ///
    /// Only what was changed is stored, so "never configured" stays distinguishable from "set back to the
    /// default by hand" — which is the distinction Reset needs.
    /// </summary>
    private readonly Dictionary<string, Thresholds> _custom = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>The limits in force for a family.</summary>
    public Thresholds For(string? family) =>
        family is not null && _custom.TryGetValue(family, out var custom) ? custom : Thresholds.Preset(family);

    /// <summary>The limits in force for a watched reading, through the family it names.</summary>
    public Thresholds For(Vital vital) => For(vital.Family);

    /// <summary>Whether a family is running on limits the user chose rather than the built-in ones.</summary>
    public bool IsCustom(string family) => _custom.ContainsKey(family);

    /// <summary>
    /// Raised when a family's limits change.
    ///
    /// Most of the app fixes itself within a second without this, because readings move and moving values
    /// re-run their bindings. Two places do not: a machine at rest leaves the title bar's ribbon and pills
    /// sitting on numbers that never change, wearing the old limits' colours until something disturbs them.
    /// The same reason AppearanceService has to announce a profile change.
    /// </summary>
    public event Action? Changed;

    public void Set(string family, Thresholds thresholds)
    {
        _custom[family] = thresholds;
        Persist();
        Changed?.Invoke();
    }

    /// <summary>Puts a family back on the built-in limits by forgetting the override, not by copying the
    /// default into it — a stored copy would stop following the built-in if it were ever revised.</summary>
    public void Reset(string family)
    {
        _custom.Remove(family);
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

        foreach (var (family, text) in settings.Current.Thresholds)
            if (Thresholds.Parse(text) is { } parsed && Thresholds.Families.Contains(family))
                _custom[family] = parsed;
    }

    private void Persist()
    {
        // Invariant, always. Thresholds.Parse splits on commas and reads invariant, so writing "75,5" for
        // seventy-five and a half on a Portuguese machine would turn three numbers into four and lose the
        // family on the next load.
        settings.Current.Thresholds = _custom.ToDictionary(
            pair => pair.Key,
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
    public string RoleFor(double value, string? family) => For(family).Role(value);

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
    public Brush BrushFor(double value, string? family) => Brush(RoleFor(value, family));

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
            Level: scale.Level(value),
            Role: scale.Role(value));
    }
}
