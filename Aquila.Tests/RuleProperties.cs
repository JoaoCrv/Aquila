using System.Windows.Media;
using Aquila.Controls;
using Aquila.Helpers;
using Aquila.Models;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;

namespace Aquila.Tests;

/// <summary>
/// The small rules the rest of the app leans on — each written once, in one place, by the cleanup of
/// 2026-10-02, and each stated here as something that must hold for any input at all.
/// </summary>
public sealed class RuleProperties
{
    // ── HexBrush: every colour in a preset or widgets.json goes through it ─────────────────────────────

    /// <summary>Any text at all, at any opacity, gives a brush — transparent if it is not a colour — and never an
    /// exception. A hand-edited file must not be able to take the desktop down.</summary>
    [Property(MaxTest = 500)]
    public bool HexBrush_never_fails(string? text, double opacity) => HexBrush.From(text, opacity).IsFrozen;

    /// <summary>Six hex digits come back exactly as written, fully opaque.</summary>
    [Property]
    public bool HexBrush_reads_six_digits_exactly(byte r, byte g, byte b)
    {
        var colour = HexBrush.From($"#{r:X2}{g:X2}{b:X2}").Color;
        return colour == Color.FromArgb(255, r, g, b);
    }

    /// <summary>An opacity that is exactly n/255 becomes exactly n — so a preset's 0.6 is the 153 it means.</summary>
    [Property]
    public bool HexBrush_maps_opacity_onto_alpha_exactly(byte alpha) =>
        HexBrush.From("#FFFFFF", alpha / 255d).Color.A == alpha;

    // ── Thresholds: when a reading is in trouble ───────────────────────────────────────────────────────

    private static readonly string[] Roles = ["Normal", "Elevated", "Alert", "Critical"];

    /// <summary>Limits as the threshold editor can produce them: in order, possibly equal.</summary>
    private static Arbitrary<Thresholds> OrderedLimits() =>
        (from elevated in Gen.Choose(0, 200)
         from toAlert in Gen.Choose(0, 50)
         from toCritical in Gen.Choose(0, 50)
         select new Thresholds(elevated, elevated + toAlert, elevated + toAlert + toCritical))
        .ToArbitrary();

    private static Arbitrary<(double, double)> TwoReadings() =>
        (from a in Gen.Choose(-50_000, 300_000)
         from b in Gen.Choose(-50_000, 300_000)
         select (Math.Min(a, b) / 1000d, Math.Max(a, b) / 1000d))
        .ToArbitrary();

    /// <summary>A hotter reading is never judged calmer: the role never steps down as the value rises.</summary>
    [Property(MaxTest = 500)]
    public Property A_rising_reading_never_earns_a_calmer_role() =>
        Prop.ForAll(OrderedLimits(), TwoReadings(), (limits, readings) =>
            Array.IndexOf(Roles, limits.Role(readings.Item1)) <= Array.IndexOf(Roles, limits.Role(readings.Item2)));

    /// <summary>Level — what the pressure ribbon and the pills draw — stays within 0..1 and never steps down.</summary>
    [Property(MaxTest = 500)]
    public Property Level_stays_in_range_and_never_falls() =>
        Prop.ForAll(OrderedLimits(), TwoReadings(), (limits, readings) =>
        {
            var low = limits.Level(readings.Item1);
            var high = limits.Level(readings.Item2);
            return low is >= 0 and <= 1 && high is >= 0 and <= 1 && low <= high;
        });

    // ── Preset.RampNameFor: which colours a line is drawn in ──────────────────────────────────────────

    private static Arbitrary<Preset> PresetsWithRamps() =>
        (from names in Gen.Elements("primary", "Primary", "secondary", "accent", "spare").ListOf()
         select WithRamps(names))
        .ToArbitrary();

    private static Preset WithRamps(IEnumerable<string> names)
    {
        var preset = new Preset { Id = "test" };
        preset.Ramps.Clear();
        foreach (var name in names) preset.Ramps.TryAdd(name, new Ramp());
        return preset;
    }

    /// <summary>The answer is always one of the preset's own ramps, in the preset's own spelling — or nothing,
    /// when the preset has neither the one asked for nor a primary.</summary>
    [Property(MaxTest = 500)]
    public Property The_ramp_named_is_always_one_the_preset_has() =>
        Prop.ForAll(PresetsWithRamps(), ArbMap.Default.ArbFor<string?>(), ArbMap.Default.ArbFor<int>(),
            (preset, chosen, index) =>
                preset.RampNameFor(chosen, index) is not { } name || preset.Ramps.Keys.Contains(name));

    // ── SensorFormat: how every reading is written ────────────────────────────────────────────────────

    /// <summary>Any reading in any unit is written without failing; a missing one is "--".</summary>
    [Property(MaxTest = 500)]
    public bool Any_reading_is_written(float? value, string? unit)
    {
        var (number, written) = SensorFormat.Parts(value, unit);
        return number is not null && written is not null && (value is not null || number == "--");
    }

    /// <summary>Throughput always lands in one of its three units, whatever the magnitude.</summary>
    [Property(MaxTest = 500)]
    public bool Throughput_lands_in_a_known_unit(float bytesPerSecond) =>
        SensorFormat.Parts(bytesPerSecond, SensorFormat.BytesPerSecond).Unit is "B/s" or "KB/s" or "MB/s";
}
