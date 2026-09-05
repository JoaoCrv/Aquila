using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Text.Json;
using System.Text.Json.Serialization;
using Aquila.Models;
using Microsoft.Extensions.Logging;

namespace Aquila.Services;

/// <summary>
/// Loads and saves presets — the visual half of a widget, one JSON file each.
///
/// Built-ins embedded in the assembly, the user's own in <c>Documents\Aquila\presets</c>, and a matching
/// id overriding a built-in so one of ours can be customised.
///
/// Resolution is PER WIDGET, because each may wear a different preset. The one exception is
/// <see cref="Publish"/>, which puts a preset's colours onto the Aquila.Scheme.* keys for the app's own
/// surfaces — those are a single surface sharing one preset, and publishing is simply how a static XAML
/// subtree gets dressed without every element resolving a preset in code.
///
/// Failures are never fatal. A missing or malformed preset costs that preset, not the app's ability to draw
/// — and <see cref="Fallback"/> means a widget always has something to wear.
/// </summary>
public sealed class PresetService(ILogger<PresetService> logger, SettingsService settings)
{
    /// <summary>The preset that carries the app's own identity. Shipped, never editable, never deleted:
    /// duplicating it is how variation starts, and it is the last thing left to fall back to.</summary>
    public const string BaseId = "ember";

    /// <summary>
    /// The preset a widget wears when it names none.
    ///
    /// A setting rather than <see cref="BaseId"/> directly, because "everything I make should look like
    /// this one" is a reasonable thing to want and there is nothing in the format standing in its way. The
    /// shipped one remains the floor: a default naming something that no longer exists resolves back to it
    /// rather than leaving widgets undressed.
    /// </summary>
    public string DefaultId
    {
        get
        {
            var wanted = settings.Current.DefaultPresetId;
            return !string.IsNullOrWhiteSpace(wanted) && _presets.Any(p =>
                string.Equals(p.Id, wanted, StringComparison.OrdinalIgnoreCase)) ? wanted : BaseId;
        }
    }

    public void SetDefault(Preset preset)
    {
        settings.Current.DefaultPresetId = preset.Id;
        settings.Save();
    }

    private static readonly JsonSerializerOptions _read = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private static readonly JsonSerializerOptions _write = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly List<Preset> _presets = [];

    public IReadOnlyList<Preset> Presets => _presets;

    /// <summary>Never null, so a caller never has to decide what to do without one.</summary>
    public static Preset Fallback { get; } = new()
    {
        Id = "fallback",
        Name = "Fallback",
        Ramps = { [Ramp.Primary] = Ramp.Neutral },
    };

    public void Load()
    {
        _presets.Clear();

        foreach (var preset in ReadEmbedded()) Register(preset);
        foreach (var preset in ReadUserFolder()) Register(preset);

        if (_presets.Count == 0)
        {
            logger.LogWarning("No preset could be loaded; using the built-in fallback");
            _presets.Add(Fallback);
        }

        Sort();
    }

    private void Sort() =>
        _presets.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.CurrentCultureIgnoreCase));

    /// <summary>
    /// The preset a widget wears, by id — falling back to the base rather than to nothing.
    ///
    /// An empty id means "whatever the default is", which is what a widget that has never been dressed
    /// says. A NAMED id that is missing means the preset was deleted or came with a layout from another
    /// machine; both land on the base, because a widget drawn in the house colours is better than a widget
    /// not drawn.
    /// </summary>
    /// <summary>
    /// Whether a preset by this id is really installed.
    ///
    /// <see cref="For"/> cannot answer it: it exists never to return null, so it walks its fallbacks and
    /// hands back the base for a name nobody has. That is right for dressing a widget and useless for
    /// deciding whether to offer something — a theme suggesting a preset the user does not have should
    /// ask nothing at all.
    /// </summary>
    public bool Has(string? id) =>
        !string.IsNullOrWhiteSpace(id) &&
        _presets.Any(p => string.Equals(p.Id, id, StringComparison.OrdinalIgnoreCase));

    public Preset For(string? id) =>
        (string.IsNullOrWhiteSpace(id)
            ? null
            : _presets.FirstOrDefault(p => string.Equals(p.Id, id, StringComparison.OrdinalIgnoreCase)))
        ?? _presets.FirstOrDefault(p => string.Equals(p.Id, DefaultId, StringComparison.OrdinalIgnoreCase))
        ?? _presets.FirstOrDefault()
        ?? Fallback;

    // --- Edit sessions ---
    //
    // The same shape as DesktopWidgetService's snapshot, and for the same reason: what is kept aside is the
    // ORIGINAL, while the live object stays the one being edited. Every widget already resolves to that live
    // object, so an edit reaches all of them without anything being repointed — which is the whole benefit of
    // a preset over thirty fields per widget, and it would be lost if editing happened on a copy off to one
    // side. Non-empty IS a session, so there is no second flag to disagree with it.

    private readonly Dictionary<string, Preset> _originals = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _created = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Preset> _deleted = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Puts a preset's colours onto the <c>Aquila.Scheme.*</c> keys the app's own surfaces bind to.
    ///
    /// This is the one place a preset is published globally, and it is not a contradiction of resolving
    /// them per widget: a widget is dressed individually because each one may wear a different preset,
    /// while the app's own surfaces — the dashboard's cards, the pages, the pills — are a SINGLE surface
    /// and share one. Publishing is simply how a static XAML subtree gets dressed without every element
    /// resolving a preset in code.
    ///
    /// The three series slots map onto the preset's ramps in order, which is what a widget's own lines
    /// already do through DefaultRamp: a card asking for Series2 is asking for the second ramp. Below the
    /// count of ramps they fall back to primary, by the same rule as RampFor.
    ///
    /// WPF's resources are hierarchical, so a second surface with a preset of its own would publish the
    /// same keys into its own scope and override these for everything inside it. Nothing here has to
    /// change for that to work; there is simply only one surface today.
    /// </summary>
    public void Publish(string? id)
    {
        var preset = For(id);
        var resources = Application.Current?.Resources;
        if (resources is null) return;

        var ramps = preset.Ramps.Values.ToList();
        Ramp Nth(int i) => i < ramps.Count ? ramps[i] : preset.RampFor(Ramp.Primary);

        var primary = preset.RampFor(Ramp.Primary);

        resources["Aquila.Scheme.Normal"] = Brush(primary.Normal);
        resources["Aquila.Scheme.Elevated"] = Brush(primary.Elevated);
        resources["Aquila.Scheme.Alert"] = Brush(primary.Alert);
        resources["Aquila.Scheme.Critical"] = Brush(primary.Critical);

        resources["Aquila.Scheme.Series1"] = Brush(Nth(0).Normal);
        resources["Aquila.Scheme.Series2"] = Brush(Nth(1).Normal);
        resources["Aquila.Scheme.Series3"] = Brush(Nth(2).Normal);

        // A reading at rest, which is what an unjudged value shows.
        resources["Aquila.Scheme.Accent"] = Brush(primary.Normal);
        resources["Aquila.Scheme.Track"] = Brush(preset.Gauge.Track.Color, preset.Gauge.Track.Opacity);
    }

    private static SolidColorBrush Brush(string hex, double opacity = 1)
    {
        try
        {
            var colour = (Color)ColorConverter.ConvertFromString(hex);
            colour.A = (byte)Math.Clamp(opacity * 255, 0, 255);

            var brush = new SolidColorBrush(colour);
            brush.Freeze();
            return brush;
        }
        catch
        {
            return new SolidColorBrush(Colors.Transparent);
        }
    }

    /// <summary>The locked presets edited in this session — the ones whose changes have nowhere to go
    /// unless the user is asked. Empty is the normal case, and nothing is asked then.</summary>
    public IReadOnlyList<Preset> LockedDrafts =>
        [.. _presets.Where(p => p.IsBuiltIn && _originals.ContainsKey(p.Id))];

    /// <summary>The presets edited since the session began.</summary>
    public IReadOnlyList<Preset> Drafts => [.. _presets.Where(p => _originals.ContainsKey(p.Id))];

    /// <summary>
    /// Marks a preset as being edited and hands back the object to write into.
    ///
    /// Called on the first edit and never on selection: picking a preset to look at must not count as
    /// changing it, or simply reading down the list would end in being asked to save four of them.
    /// </summary>
    public Preset Draft(Preset preset)
    {
        if (!_originals.ContainsKey(preset.Id)) _originals[preset.Id] = Clone(preset);
        return preset;
    }

    /// <summary>Puts every edited preset back as it was and ends the session.</summary>
    public void Revert()
    {
        // Nothing was written for these, so forgetting them is the whole of undoing them.
        foreach (var id in _created) _presets.RemoveAll(p => string.Equals(p.Id, id, StringComparison.OrdinalIgnoreCase));
        _created.Clear();

        // These still have their files; putting them back in the list is all that was taken away.
        foreach (var preset in _deleted.Values) Register(preset);
        _deleted.Clear();
        Sort();

        if (_originals.Count == 0) return;

        foreach (var original in _originals.Values) Register(original);

        _originals.Clear();
        Sort();
    }

    /// <summary>
    /// Keeps the session's work: every edited preset is written, and every preset created during it.
    ///
    /// It no longer decides anything. Creating a variant is an action the user takes while editing, and a
    /// locked preset they edited without taking it has already been asked about by the time this runs —
    /// so all that is left here is carrying out what happened. Deciding here is what produced ember-copy2
    /// beside ember-copy: a fork on every save, because nobody had been asked.
    ///
    /// A locked preset still holding edits is put back as it was. That is the "discard" answer, and it is
    /// also the safe reading of any answer that never arrived.
    ///
    /// Returns true when something was put back, so the caller knows the widgets still wearing it are
    /// showing a look that no longer exists.
    /// </summary>
    public bool Commit()
    {
        var reverted = false;

        foreach (var (id, original) in _originals)
        {
            var draft = _presets.FirstOrDefault(p => string.Equals(p.Id, id, StringComparison.OrdinalIgnoreCase));
            if (draft is null) continue;

            if (original.IsBuiltIn)
            {
                Register(original);
                reverted = true;
                continue;
            }

            Save(draft);
        }

        foreach (var id in _deleted.Keys) Erase(id);
        _deleted.Clear();

        // Born this session, so they exist only in memory until now.
        foreach (var id in _created)
            if (_presets.FirstOrDefault(p => string.Equals(p.Id, id, StringComparison.OrdinalIgnoreCase)) is { } born)
                Save(born);

        _originals.Clear();
        _created.Clear();
        Sort();

        return reverted;
    }

    /// <summary>
    /// A new preset from an existing one, in memory.
    ///
    /// The way every preset is born. Starting from something that already works beats starting from a blank
    /// file, and the original is left intact to go back to — which is the whole of what "locked" protects.
    ///
    /// Not written to disk here. A preset created and then thrown away with the session should leave
    /// nothing behind, so the file appears when the session is kept and never otherwise.
    /// </summary>
    public Preset Duplicate(Preset source)
    {
        var copy = Clone(source);

        copy.Name = NextName(source.Name);
        copy.Id = FreeId(Slug(copy.Name, source.Id));
        copy.IsBuiltIn = false;

        Register(copy);
        _created.Add(copy.Id);
        Sort();

        return copy;
    }

    /// <summary>
    /// "Ember" becomes "Ember 2", and "Ember 2" becomes "Ember 3".
    ///
    /// Counting rather than "(copy)", and "(copy) (copy)" is why: a name that grows a suffix every time is
    /// unreadable by the third one, where a number stays the same length forever.
    /// </summary>
    private string NextName(string name)
    {
        var stem = name.TrimEnd(' ', '0', '1', '2', '3', '4', '5', '6', '7', '8', '9').Trim();
        if (stem.Length == 0) stem = name.Trim();

        for (var n = 2; ; n++)
        {
            var candidate = $"{stem} {n}";
            if (!_presets.Any(p => string.Equals(p.Name, candidate, StringComparison.CurrentCultureIgnoreCase)))
                return candidate;
        }
    }

    /// <summary>Writes a preset to the user folder. A built-in is never written over — the caller should
    /// have duplicated it, and silently editing one of ours would take away the thing to go back to.</summary>
    public void Save(Preset preset)
    {
        if (preset.IsBuiltIn)
        {
            logger.LogWarning("Refusing to overwrite the built-in preset {Id}", preset.Id);
            return;
        }

        try
        {
            Directory.CreateDirectory(AquilaPaths.Presets);
            File.WriteAllText(PathFor(preset.Id), JsonSerializer.Serialize(preset, _write));
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Preset {Id} could not be saved", preset.Id);
        }
    }

    /// <summary>
    /// Removes a preset from the list now and from disk when the session is kept.
    ///
    /// Deferred for the same reason a new preset is not written until then: an edit session is undoable in
    /// one gesture, and a deletion that had already happened would be the one thing Discard could not put
    /// back. Gone from the list immediately regardless, because a picker still offering something the user
    /// just removed is a picker arguing with them.
    ///
    /// Any edits it was carrying go with it, and so does its place in the created set — a preset both made
    /// and dropped inside one session should leave nothing behind at all, not a file written on the way out.
    /// </summary>
    public void Delete(Preset preset)
    {
        // A built-in has no file of ours to remove and is what everything falls back TO.
        if (preset.IsBuiltIn) return;

        _presets.RemoveAll(p => string.Equals(p.Id, preset.Id, StringComparison.OrdinalIgnoreCase));
        _originals.Remove(preset.Id);

        // Born this session and never written, so forgetting it is the whole of removing it.
        if (_created.Remove(preset.Id)) return;

        _deleted[preset.Id] = preset;
    }

    private void Erase(string id)
    {
        try
        {
            var path = PathFor(id);
            if (File.Exists(path)) File.Delete(path);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Preset {Id} could not be deleted", id);
        }
    }

    /// <summary>A deep copy, through the serializer rather than by hand. A copy written field by field is a
    /// list that silently falls behind the format — which is exactly how a duplicated preset would start
    /// losing whatever was added last.</summary>
    public static Preset Clone(Preset source)
    {
        var copy = JsonSerializer.Deserialize<Preset>(JsonSerializer.Serialize(source, _write), _read);
        if (copy is null) return Fallback;

        // [JsonIgnore], so it does not survive the round trip — and a copy that had forgotten it came from
        // a built-in would be writable straight over the file it was copied from.
        copy.IsBuiltIn = source.IsBuiltIn;
        return copy;
    }

    private static string PathFor(string id) => Path.Combine(AquilaPaths.Presets, $"{id}.json");

    /// <summary>
    /// A file-name stem from a preset's name: "My Dark Ember" becomes "my-dark-ember".
    ///
    /// So the folder is browsable — the whole point of presets being files people can send each other is
    /// that the file is recognisable when it arrives. Falls back to the source preset's id when a name has
    /// nothing usable in it, which a name written in a non-Latin script otherwise would not.
    /// </summary>
    private static string Slug(string name, string fallback)
    {
        var slug = new string([.. name.ToLowerInvariant()
            .Select(c => char.IsAsciiLetterOrDigit(c) ? c : '-')]).Trim('-');

        while (slug.Contains("--", StringComparison.Ordinal))
            slug = slug.Replace("--", "-", StringComparison.Ordinal);

        return slug.Length > 0 ? slug : $"{fallback}-copy";
    }

    private string FreeId(string wanted)
    {
        var id = wanted;
        var suffix = 2;

        while (_presets.Any(p => string.Equals(p.Id, id, StringComparison.OrdinalIgnoreCase))
               || File.Exists(PathFor(id)))
            id = $"{wanted}{suffix++}";

        return id;
    }

    /// <summary>Adds a preset, replacing any earlier one with the same id — which is how a user file
    /// customises a built-in without the built-in having to be writable.</summary>
    private void Register(Preset preset)
    {
        _presets.RemoveAll(p => string.Equals(p.Id, preset.Id, StringComparison.OrdinalIgnoreCase));
        _presets.Add(preset);
    }

    private IEnumerable<Preset> ReadEmbedded()
    {
        var assembly = typeof(PresetService).Assembly;

        foreach (var name in assembly.GetManifestResourceNames())
        {
            if (!name.StartsWith("Aquila.Presets.", StringComparison.Ordinal) ||
                !name.EndsWith(".json", StringComparison.Ordinal))
                continue;

            Preset? preset = null;
            try
            {
                using var stream = assembly.GetManifestResourceStream(name);
                if (stream is not null) preset = JsonSerializer.Deserialize<Preset>(stream, _read);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Built-in preset {Resource} could not be read", name);
            }

            if (preset is null) continue;
            preset.IsBuiltIn = true;

            if (Accept(preset, name)) yield return preset;
        }
    }

    private IEnumerable<Preset> ReadUserFolder()
    {
        if (!Directory.Exists(AquilaPaths.Presets)) yield break;

        foreach (var file in Directory.EnumerateFiles(AquilaPaths.Presets, "*.json"))
        {
            Preset? preset = null;
            try
            {
                preset = JsonSerializer.Deserialize<Preset>(File.ReadAllText(file), _read);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Preset {File} could not be read", file);
            }

            if (preset is null) continue;

            // The file name stands in for a missing id, so dropping a preset into the folder and naming it
            // sensibly is enough to make it appear.
            if (string.IsNullOrWhiteSpace(preset.Id))
                preset.Id = Path.GetFileNameWithoutExtension(file);

            if (Accept(preset, file)) yield return preset;
        }
    }

    /// <summary>
    /// Checks a preset is usable and fills in what it left out.
    ///
    /// Repaired rather than rejected wherever repair is honest: a preset with no name takes its id, and one
    /// with no ramps gets a neutral primary. The smallest useful preset really is four colours, and someone
    /// trying a palette they generated online should not have to learn the rest of the format first.
    /// </summary>
    private bool Accept(Preset preset, string source)
    {
        if (string.IsNullOrWhiteSpace(preset.Id))
        {
            logger.LogWarning("Preset {Source} has no id and was skipped", source);
            return false;
        }

        if (string.IsNullOrWhiteSpace(preset.Name)) preset.Name = preset.Id;

        // System.Text.Json builds its own dictionary and discards the comparer, so a preset asking for
        // "Primary" would miss a ramp stored as "primary". Rebuilt here, once, for the same reason the
        // colour profile has to do it.
        preset.Ramps = new Dictionary<string, Ramp>(preset.Ramps, StringComparer.OrdinalIgnoreCase);

        if (preset.Ramps.Count == 0)
        {
            logger.LogWarning("Preset {Source} declares no ramps; giving it a neutral one", source);
            preset.Ramps[Ramp.Primary] = Ramp.Neutral;
        }

        return true;
    }
}
