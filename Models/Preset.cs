using System.Text.Json.Serialization;

namespace Aquila.Models;

/// <summary>
/// Everything a widget looks like, in one shareable file.
///
/// The visual half of a widget, separated from the content half. The widget JSON says what it reads, where
/// it sits and how big it is; the preset says how all of that is dressed. Neither knows the other's
/// business, which is what lets one preset clothe a CPU gauge and a network chart without editing.
///
/// It also never contains thresholds. Swapping a preset must not change when something counts as critical —
/// that lives in settings, keyed by what a reading IS (see <see cref="MetricKey"/>).
///
/// The rule that holds the format together: <b>anything showing the state of data names a ramp, never a
/// colour</b>. A fixed colour is a ramp whose four stops are equal, so there is one mechanism and no bypass.
/// Frame parts — backgrounds, borders, titles — carry a colour directly, because they represent nothing.
/// </summary>
public sealed class Preset
{
    /// <summary>File name stem by convention, and the id a widget stores. A user preset with the same id as
    /// a built-in replaces it — that is how one of ours gets customised.</summary>
    public string Id { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;
    public string? Author { get; set; }
    public string? Description { get; set; }

    /// <summary>
    /// The colour scales, by name. <c>primary</c> is expected and is the fallback for anything that asks
    /// for a ramp this preset does not have — a chart with three lines and two ramps draws, rather than
    /// throwing or inventing.
    ///
    /// Local to the preset rather than drawn from a shared library. The benefit of a library
    /// (edit-one-update-all) belongs to a mature marketplace; its cost — ids, import collisions, dangling
    /// references, deleting a ramp still in use — is immediate. The SHAPE is kept intact, so promoting a
    /// local ramp to a global one later is a copy and needs no migration.
    /// </summary>
    public Dictionary<string, Ramp> Ramps { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public PresetFill Background { get; set; } = new();
    public PresetBorder Border { get; set; } = new();
    public PresetGauge Gauge { get; set; } = new();
    public PresetLine Line { get; set; } = new();
    public PresetBar Bar { get; set; } = new();
    public PresetNumber Number { get; set; } = new();
    public PresetText Title { get; set; } = new() { Size = 11, Opacity = 0.6 };
    public PresetText Value { get; set; } = new() { Size = 18 };

    /// <summary>True for presets shipped inside the app. They can be duplicated but never overwritten, so a
    /// bad edit is always recoverable — and one of them is the app's own identity.</summary>
    [JsonIgnore]
    public bool IsBuiltIn { get; set; }

    [JsonIgnore]
    public string DisplayName => IsBuiltIn ? Name : $"{Name} (custom)";

    /// <summary>The named ramp, or <c>primary</c>, or a neutral one. Never null: a widget asking for a ramp
    /// that is not here should draw in the wrong colour rather than not draw.</summary>
    public Ramp RampFor(string? name) =>
        name is not null && Ramps.TryGetValue(name, out var ramp) ? ramp
        : Ramps.TryGetValue(Ramp.Primary, out var primary) ? primary
        : Ramp.Neutral;
}

/// <summary>
/// A colour per state: what a reading looks like as it goes from ordinary to critical.
///
/// Four colours and no numbers — WHEN each applies is a threshold, and thresholds are not in the preset.
/// A fixed colour is four equal stops, which is why nothing in the format needs a "don't follow the value"
/// switch: not following IS a ramp that goes nowhere.
/// </summary>
public sealed class Ramp
{
    public const string Primary = "primary";

    public string Normal { get; set; } = "#60CDFF";
    public string Elevated { get; set; } = "#F5A623";
    public string Alert { get; set; } = "#FF6B35";
    public string Critical { get; set; } = "#FF4444";

    /// <summary>Stands in when a preset names no ramp at all, so a malformed file still renders.</summary>
    public static Ramp Neutral => new();

    /// <summary>The colour for a state, by the role names the rest of the app already uses.</summary>
    public string this[string role] => role switch
    {
        "Critical" => Critical,
        "Alert" => Alert,
        "Elevated" => Elevated,
        _ => Normal,
    };
}

/// <summary>A colour and how see-through it is, kept apart so changing one never resets the other.</summary>
public class PresetFill
{
    public string Color { get; set; } = "#000000";
    public double Opacity { get; set; } = 0.6;
}

public sealed class PresetBorder : PresetFill
{
    public PresetBorder() { Color = "#FFFFFF"; Opacity = 0.25; }

    public double Thickness { get; set; }
    public double CornerRadius { get; set; } = 8;
}

/// <summary>Words: how big, how faint, and in what family. The family falls back to the app's own when the
/// machine importing a preset does not have it installed.</summary>
public sealed class PresetText
{
    public string? FontFamily { get; set; }
    public double Size { get; set; } = 14;
    public double Opacity { get; set; } = 1;
    public TextAlign Align { get; set; } = TextAlign.Center;
}

public sealed class PresetGauge
{
    public string Ramp { get; set; } = Models.Ramp.Primary;
    public double Thickness { get; set; } = 14;
    public double Corner { get; set; }
    public GaugeSweep Sweep { get; set; } = GaugeSweep.Dial;

    /// <summary>The unfilled part of the arc.</summary>
    public PresetFill Track { get; set; } = new() { Color = "#FFFFFF", Opacity = 0.08 };
}

public sealed class PresetLine
{
    /// <summary>One per series, in order. A chart with more lines than ramps falls back to
    /// <see cref="Models.Ramp.Primary"/> for the rest.</summary>
    public List<string> Ramps { get; set; } = [Models.Ramp.Primary];

    public double Thickness { get; set; } = 1.5;
    public ChartFill Fill { get; set; } = ChartFill.Gradient;
    public double FillOpacity { get; set; } = 0.30;
    public double Smoothness { get; set; } = 0.5;
    public double PointSize { get; set; }
}

public sealed class PresetBar
{
    public string Ramp { get; set; } = Models.Ramp.Primary;
    public double Thickness { get; set; } = 6;
    public double Corner { get; set; } = 3;
    public MeterLayout Layout { get; set; } = MeterLayout.Beside;
}

public sealed class PresetNumber
{
    public string Ramp { get; set; } = Models.Ramp.Primary;
    public double Size { get; set; } = 20;
    public double UnitSize { get; set; } = 13;

    /// <summary>Whether the number carries a rounded fill of its own. Off suits a desktop widget, which
    /// already has a panel — two of them read as a box inside a box.</summary>
    public bool Panel { get; set; }
}
