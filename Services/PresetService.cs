using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Aquila.Models;
using Microsoft.Extensions.Logging;

namespace Aquila.Services;

/// <summary>
/// Loads and saves presets — the visual half of a widget, one JSON file each.
///
/// Twin of <see cref="ColorProfileService"/> in shape, and its eventual replacement: built-ins embedded in
/// the assembly, the user's own in <c>Documents\Aquila\presets</c>, and a matching id overriding a built-in
/// so one of ours can be customised.
///
/// What it deliberately does NOT do is publish anything globally. A colour profile pushes its roles into
/// Application.Resources because one profile dresses the whole app; presets are resolved per widget, and a
/// global publish is exactly what would make "different presets on different widgets" impossible.
///
/// Failures are never fatal. A missing or malformed preset costs that preset, not the app's ability to draw
/// — and <see cref="Fallback"/> means a widget always has something to wear.
/// </summary>
public sealed class PresetService(ILogger<PresetService> logger)
{
    /// <summary>The preset that carries the app's own identity. Shipped, never editable, never deleted:
    /// duplicating it is how variation starts, and it is what everything falls back to.</summary>
    public const string BaseId = "ember";

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

        _presets.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.CurrentCultureIgnoreCase));
    }

    /// <summary>
    /// The preset a widget wears, by id — falling back to the base rather than to nothing.
    ///
    /// An empty id means "whatever the default is", which is what a widget that has never been dressed
    /// says. A NAMED id that is missing means the preset was deleted or came with a layout from another
    /// machine; both land on the base, because a widget drawn in the house colours is better than a widget
    /// not drawn.
    /// </summary>
    public Preset For(string? id) =>
        (string.IsNullOrWhiteSpace(id)
            ? null
            : _presets.FirstOrDefault(p => string.Equals(p.Id, id, StringComparison.OrdinalIgnoreCase)))
        ?? _presets.FirstOrDefault(p => p.Id == BaseId)
        ?? _presets.FirstOrDefault()
        ?? Fallback;

    /// <summary>
    /// Writes a copy into the user folder under a free id, and returns it.
    ///
    /// The only way to edit a built-in, and the way a variant is born: starting from something that already
    /// works beats starting from a blank file, and the original stays intact to go back to.
    /// </summary>
    public Preset Duplicate(Preset source)
    {
        var copy = Clone(source);

        copy.Id = FreeId($"{source.Id}-copy");
        copy.Name = $"{source.Name} (copy)";
        copy.IsBuiltIn = false;

        Save(copy);
        Register(copy);

        return copy;
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

    public void Delete(Preset preset)
    {
        if (preset.IsBuiltIn) return;

        try
        {
            var path = PathFor(preset.Id);
            if (File.Exists(path)) File.Delete(path);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Preset {Id} could not be deleted", preset.Id);
        }

        _presets.RemoveAll(p => p.Id == preset.Id);
    }

    /// <summary>A deep copy, through the serializer rather than by hand. A copy written field by field is a
    /// list that silently falls behind the format — which is exactly how a duplicated preset would start
    /// losing whatever was added last.</summary>
    public static Preset Clone(Preset source) =>
        JsonSerializer.Deserialize<Preset>(JsonSerializer.Serialize(source, _write), _read) ?? Fallback;

    private static string PathFor(string id) => Path.Combine(AquilaPaths.Presets, $"{id}.json");

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
