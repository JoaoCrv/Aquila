using System.ComponentModel;
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
public sealed class Preset : INotifyPropertyChanged
{
    /// <summary>
    /// The format this code writes. Raise it when a field is renamed, moved or given a new meaning — not
    /// when one is merely added, because an older file simply lacks it and takes the default — and add the
    /// step that carries a preset from the previous number to this one in PresetService.Upgrade.
    /// </summary>
    public const int CurrentFormat = 1;

    /// <summary>
    /// Which version of the format this file is written in. First in the class so it is the first line of
    /// the file.
    ///
    /// ABSENT MEANS 1, and must go on meaning 1 for ever: every preset written before this field existed is
    /// the first format, built-ins included. That is why the default is the literal and not CurrentFormat —
    /// the day CurrentFormat becomes 2, an old file must not start claiming to be new.
    ///
    /// It exists because presets now leave the machine. A file exported today will be imported by a later
    /// Aquila and a later file by this one; without a number, neither can tell, and the difference arrives
    /// as fields silently ignored or misread. The reader that recognises "newer than me" has to ship BEFORE
    /// the newer format exists, or the version that most needs it will not have it.
    /// </summary>
    public int Format { get; set; } = 1;

    /// <summary>File name stem by convention, and the id a widget stores. A user preset with the same id as
    /// a built-in replaces it — that is how one of ours gets customised.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// What the preset is called. The only part of its identity a user edits — <see cref="Id"/> stays as
    /// it was, because that is what every widget stores, and renaming something should not silently
    /// undress the widgets wearing it.
    ///
    /// Watchable, and the only reason this class raises anything at all: the picker and the name box sit
    /// two lines apart in the editor, and a picker still showing the old name reads as a rename that did
    /// not take.
    /// </summary>
    public string Name
    {
        get => _name;
        set
        {
            if (_name == value) return;
            _name = value;
            Raise(nameof(Name));
        }
    }

    private string _name = string.Empty;

    public event PropertyChangedEventHandler? PropertyChanged;

    private void Raise(string property) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(property));
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

    // No FontFamily here any more. It lived at this level on the reasoning that two families are two
    // designs — which is an opinion rather than a constraint, and pairing a display face for the label
    // with a plain one for the number is ordinary typography. It also could not be found: somebody
    // looking for a font setting looks where the text settings are. Title and Value carry their own.

    public PresetFill Background { get; set; } = new();
    public PresetBorder Border { get; set; } = new();
    public PresetGauge Gauge { get; set; } = new();
    public PresetLine Line { get; set; } = new();
    public PresetBar Bar { get; set; } = new();
    public PresetNumber Number { get; set; } = new();
    public PresetTitle Title { get; set; } = new();
    public PresetText Value { get; set; } = new();

    /// <summary>True for presets shipped inside the app. They can be duplicated but never overwritten, so a
    /// bad edit is always recoverable — and one of them is the app's own identity.</summary>
    [JsonIgnore]
    public bool IsBuiltIn { get; set; }

    // No DisplayName. It used to append "(custom)" to the user's own presets, which marked the ordinary
    // case and left the restricted one unlabelled; the editor draws a padlock beside the locked ones
    // instead, which is the same information with the polarity the right way round.

    /// <summary>The named ramp, or <c>primary</c>, or a neutral one. Never null: a widget asking for a ramp
    /// that is not here should draw in the wrong colour rather than not draw.</summary>
    public Ramp RampFor(string? name) =>
        name is not null && Ramps.TryGetValue(name, out var ramp) ? ramp
        : Ramps.TryGetValue(Ramp.Primary, out var primary) ? primary
        : Ramp.Neutral;

    /// <summary>The ramp a reading in position <paramref name="index"/> is drawn in — see
    /// <see cref="RampNameFor"/>.</summary>
    public Ramp RampFor(string? chosen, int index) => RampFor(RampNameFor(chosen, index));

    /// <summary>
    /// Which ramp a reading is drawn in: the one it chose; having chosen none, the ramp in its position,
    /// because two lines in the same colour are one line; and primary past the ramps the preset declares,
    /// or for a name it does not have — a chart that draws is better than one that refuses because its
    /// preset was written for two lines. Null when there is no primary either, which
    /// <see cref="RampFor(string?)"/> answers with a neutral ramp.
    ///
    /// In the PRESET'S spelling. Names match case-insensitively, but the editor's picker selects by plain
    /// equality, and "Primary" would select nothing in a list holding "primary".
    ///
    /// The one statement of the rule. It was written out four times — twice in the renderer, once in the
    /// editor so its picker would show what the desktop draws, once in the dashboard's publish — and the
    /// editor's copy had drifted, answering a literal "primary" whether or not the preset spelled it so.
    /// </summary>
    public string? RampNameFor(string? chosen, int index)
    {
        var names = Ramps.Keys.ToList();

        if (string.IsNullOrEmpty(chosen))
        {
            if (index >= 0 && index < names.Count) return names[index];
            chosen = Ramp.Primary;
        }

        return names.FirstOrDefault(n => string.Equals(n, chosen, StringComparison.OrdinalIgnoreCase))
               ?? names.FirstOrDefault(n => string.Equals(n, Ramp.Primary, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Renames a ramp IN PLACE, keeping its position.
    ///
    /// The dictionary is rebuilt rather than re-keyed, because position carries meaning here: Publish maps
    /// Series1/2/3 onto the ramps in order, and a widget that names no ramp takes them the same way. Simply
    /// removing and re-adding would move the ramp to the end and silently repaint two other series.
    ///
    /// Refuses a blank name and refuses a collision, and says so rather than throwing: the caller is a text
    /// box, where a half-typed name is a normal thing to be holding for a moment.
    /// </summary>
    public bool RenameRamp(string from, string to)
    {
        to = to?.Trim() ?? string.Empty;

        if (to.Length == 0 || !Ramps.ContainsKey(from)) return false;
        if (string.Equals(from, to, StringComparison.OrdinalIgnoreCase)) return true;
        if (Ramps.ContainsKey(to)) return false;

        var rebuilt = new Dictionary<string, Ramp>(Ramps.Comparer);
        foreach (var (name, ramp) in Ramps)
            rebuilt[string.Equals(name, from, StringComparison.OrdinalIgnoreCase) ? to : name] = ramp;

        Ramps = rebuilt;
        return true;
    }
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

    /// <summary>The space between the panel and what it holds. Here rather than in a section of its own
    /// because thickness, corner radius and padding are three properties of the same box — the widget's
    /// backing Border — and a section holding one number would only be a place to lose it.
    ///
    /// A preset that says how round the panel is and not how much room is inside it can style a gauge and
    /// still crowd a stat, which is what a hardcoded 10 did to every preset ever written.</summary>
    public double Padding { get; set; } = 10;
}

/// <summary>
/// The reading itself: the number in a gauge, the big figure in a stat, the words in a Text widget.
///
/// Only how it is drawn. Whether it is drawn, and whether it carries its unit, moved to the widget: those
/// decide what is on the screen rather than how it looks, and a preset has to be safe to try on. See
/// <see cref="DesktopWidgetDefinition.ShowValue"/>.
/// </summary>
public sealed class PresetText
{
    /// <summary>
    /// The colour a reading is drawn in where nothing is judging it — a clock, a caption with no sensor
    /// behind it.
    ///
    /// A colour and not a ramp, and that is not a hole in the rule: a ramp answers "how is this doing",
    /// and a clock is not doing anything. Where there IS a reading to judge, the ramp is painted over
    /// this, so the two can never disagree about the same number.
    /// </summary>
    public string Color { get; set; } = "#FFFFFF";

    /// <summary>Null means the app's own face — also what an importing machine falls back to when it does
    /// not have the named one installed.</summary>
    public string? FontFamily { get; set; }

    public double Size { get; set; } = 18;
    public TextWeight Weight { get; set; } = TextWeight.Regular;
    public TextAlign Align { get; set; } = TextAlign.Center;
}

/// <summary>The widget's label. Faint by default and settable, because a title is a caption rather than a
/// reading — and it carries a placement the reading has no use for.</summary>
public sealed class PresetTitle
{
    /// <summary>A label represents nothing, so it carries a colour directly — the format's own rule for
    /// frame parts. It was a hardcoded white until now, and therefore invisible over a pale panel.</summary>
    public string Color { get; set; } = "#FFFFFF";

    public string? FontFamily { get; set; }

    public double Size { get; set; } = 11;
    public TextWeight Weight { get; set; } = TextWeight.Regular;
    public double Opacity { get; set; } = 0.6;

    /// <summary>Which end of its own line the label sits at. Only visible with the title above or below —
    /// docked to a side it is a column as wide as its text, and there is nothing to align it within.</summary>
    public TextAlign Align { get; set; } = TextAlign.Center;

    public TitlePlacement Placement { get; set; } = TitlePlacement.Top;
}

// A note that applies to all four of these: none of them names a ramp. The SERIES does, and one thing
// said in two places is a thing that ends up disagreeing with itself. What is left here is the shape.

public sealed class PresetGauge
{
    public double Thickness { get; set; } = 14;
    public double Corner { get; set; }
    public GaugeSweep Sweep { get; set; } = GaugeSweep.Dial;

    /// <summary>The unfilled part of the arc.</summary>
    public PresetFill Track { get; set; } = new() { Color = "#FFFFFF", Opacity = 0.08 };
}

public sealed class PresetLine
{
    public double Thickness { get; set; } = 1.5;
    public ChartFill Fill { get; set; } = ChartFill.Gradient;
    public double FillOpacity { get; set; } = 0.30;
    public double Smoothness { get; set; } = 0.5;
    public double PointSize { get; set; }
}

public sealed class PresetBar
{
    public double Thickness { get; set; } = 6;
    public double Corner { get; set; } = 3;
    public MeterLayout Layout { get; set; } = MeterLayout.Beside;
}

public sealed class PresetNumber
{
    // No Size. How big a reading is drawn is Value.Size, for every kind that draws one — a stat with a
    // size of its own meant the editor's "reading size" silently did nothing to the one widget that is
    // nothing but a reading.
    // No ShowUnit. It was here for one afternoon, on the reasoning that the unit belonged beside the
    // size that draws it — but the size is the stat's alone and the unit is every reading's, so the two
    // do not in fact belong together. It reads Value.ShowUnit now.

    /// <summary>How big the unit is drawn beside the number. The stat only: it is the one piece that draws
    /// the unit separately, so it is the one piece that can give it a size of its own.</summary>
    public double UnitSize { get; set; } = 13;

    /// <summary>Whether the number carries a rounded fill of its own. Off suits a desktop widget, which
    /// already has a panel — two of them read as a box inside a box.</summary>
    public bool Panel { get; set; }
}
