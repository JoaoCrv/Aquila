using System.IO;
using System.Text.Json;
using System.Windows.Media;
using Aquila.Models;
using Microsoft.Extensions.Logging;

namespace Aquila.Services;

/// <summary>
/// Loads colour profiles and publishes the active one onto the <c>Aquila.Scheme.*</c> resource keys that
/// the whole UI binds to.
///
/// Two sources, in order: profiles shipped inside the app (embedded, so they can neither be corrupted nor
/// deleted), then <c>Documents\Aquila\profiles\*.json</c>. A user file with the same id replaces the
/// built-in of that name, which makes "start from one of theirs and change it" the natural path and needs
/// no inheritance machinery.
///
/// Nothing here is ever fatal. A malformed profile is logged and skipped rather than taken as a reason to
/// stop: these are files people hand-edit and swap, so bad input is expected traffic, not an accident.
/// </summary>
public sealed class ColorProfileService(ILogger<ColorProfileService> logger)
{
    /// <summary>The roles a profile answers. Adding one here and giving it a default in
    /// <see cref="DefaultRole"/> is all it takes — existing profiles keep working.</summary>
    public static readonly string[] Roles =
    [
        "Accent", "Normal", "Elevated", "Alert", "Critical",
        "Series1", "Series2", "Series3", "Track"
    ];

    public const string DefaultProfileId = "ember";

    private static readonly JsonSerializerOptions _read = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    private static readonly JsonSerializerOptions _write = new() { WriteIndented = true };

    private readonly List<ColorProfile> _profiles = [];

    public IReadOnlyList<ColorProfile> Profiles => _profiles;

    /// <summary>Never null: if every source fails, a neutral ramp stands in so the app still renders.</summary>
    public ColorProfile Active { get; private set; } = CreateFallback();

    public void Load()
    {
        _profiles.Clear();

        foreach (var profile in ReadEmbedded()) Register(profile);
        foreach (var profile in ReadUserFolder()) Register(profile);

        if (_profiles.Count == 0)
        {
            logger.LogWarning("No colour profile could be loaded; using the built-in fallback");
            _profiles.Add(CreateFallback());
        }

        _profiles.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.CurrentCultureIgnoreCase));
    }

    /// <summary>Publishes a profile's colours onto the role keys XAML binds to. Two dimensions collapse
    /// into one lookup: which profile, and whether the active theme is light or dark.</summary>
    public void Apply(string profileId, bool dark)
    {
        Active =
            _profiles.FirstOrDefault(p => string.Equals(p.Id, profileId, StringComparison.OrdinalIgnoreCase))
            ?? _profiles.FirstOrDefault(p => p.Id == DefaultProfileId)
            ?? _profiles.FirstOrDefault()
            ?? CreateFallback();

        var palette = Active.Palette;
        var ramp = !dark && palette.Light.Count > 0 ? palette.Light : palette.Dark;
        var resources = Application.Current.Resources;

        foreach (var role in Roles)
        {
            var slot = Active.Roles.TryGetValue(role, out var value) ? value : DefaultRole(role);
            resources[$"Aquila.Scheme.{role}"] = Resolve(ramp, slot);
        }
    }

    /// <summary>Writes a copy of a profile into the user folder under a free id, and returns its path.
    /// Starting from something that already works beats starting from a blank file, and it is the only
    /// way to edit a built-in — those stay read-only so a bad edit is always recoverable.</summary>
    public string Duplicate(ColorProfile source)
    {
        Directory.CreateDirectory(AquilaPaths.Profiles);

        var id = $"{source.Id}-copy";
        var suffix = 2;
        while (File.Exists(Path.Combine(AquilaPaths.Profiles, $"{id}.json")))
            id = $"{source.Id}-copy{suffix++}";

        var copy = new ColorProfile
        {
            Id = id,
            Name = $"{source.Name} copy",
            Author = Environment.UserName,
            Description = source.Description,
            Palette = new ProfilePalette
            {
                Dark = [.. source.Palette.Dark],
                Light = [.. source.Palette.Light],
            },
            // Written out in full, including the defaults the original left implicit: someone editing a
            // copy should see every knob that exists, not have to know which ones were omitted.
            Roles = Roles.ToDictionary(
                role => role.ToLowerInvariant(),
                role => source.Roles.TryGetValue(role, out var value) ? value : DefaultRole(role)),
        };

        var path = Path.Combine(AquilaPaths.Profiles, $"{id}.json");
        File.WriteAllText(path, JsonSerializer.Serialize(copy, _write));

        Load();
        return path;
    }

    // ── Sources ────────────────────────────────────────────────────────────────────────────────────

    private void Register(ColorProfile profile)
    {
        _profiles.RemoveAll(p => string.Equals(p.Id, profile.Id, StringComparison.OrdinalIgnoreCase));
        _profiles.Add(profile);
    }

    private IEnumerable<ColorProfile> ReadEmbedded()
    {
        var assembly = typeof(ColorProfileService).Assembly;

        foreach (var name in assembly.GetManifestResourceNames())
        {
            if (!name.StartsWith("Aquila.Profiles.", StringComparison.Ordinal) ||
                !name.EndsWith(".json", StringComparison.Ordinal))
                continue;

            ColorProfile? profile = null;
            try
            {
                using var stream = assembly.GetManifestResourceStream(name);
                if (stream is not null)
                    profile = JsonSerializer.Deserialize<ColorProfile>(stream, _read);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Built-in colour profile {Resource} could not be read", name);
            }

            if (profile is null) continue;
            profile.IsBuiltIn = true;

            if (Accept(profile, name)) yield return profile;
        }
    }

    private IEnumerable<ColorProfile> ReadUserFolder()
    {
        if (!Directory.Exists(AquilaPaths.Profiles)) yield break;

        foreach (var file in Directory.EnumerateFiles(AquilaPaths.Profiles, "*.json"))
        {
            ColorProfile? profile = null;
            try
            {
                profile = JsonSerializer.Deserialize<ColorProfile>(File.ReadAllText(file), _read);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Colour profile {File} could not be read", file);
            }

            if (profile is null) continue;

            // The file name stands in for a missing id, so dropping a palette into the folder and giving
            // it a sensible name is enough to make it appear.
            if (string.IsNullOrWhiteSpace(profile.Id))
                profile.Id = Path.GetFileNameWithoutExtension(file);

            if (Accept(profile, file)) yield return profile;
        }
    }

    // ── Validation and resolution ──────────────────────────────────────────────────────────────────

    /// <summary>Rejects a profile that could not be rendered, rather than letting it fail later at a
    /// binding where the cause would be invisible. Every colour is parsed here, so
    /// <see cref="Resolve"/> cannot throw.</summary>
    private bool Accept(ColorProfile profile, string origin)
    {
        if (profile.Palette.Dark.Count == 0 && profile.Palette.Light.Count == 0)
        {
            logger.LogWarning("Colour profile {Origin} has no palette and was skipped", origin);
            return false;
        }

        if (profile.Palette.Dark.Count == 0) profile.Palette.Dark = [.. profile.Palette.Light];
        if (profile.Palette.Light.Count == 0) profile.Palette.Light = [.. profile.Palette.Dark];

        foreach (var colour in profile.Palette.Dark.Concat(profile.Palette.Light))
        {
            if (TryParse(colour) is not null) continue;
            logger.LogWarning("Colour profile {Origin} has an unreadable colour '{Colour}' and was skipped",
                origin, colour);
            return false;
        }

        // System.Text.Json builds dictionaries with the default comparer, discarding the case-insensitive
        // one on the model — so without this, roles written lowercase in the file would never match the
        // capitalised names looked up here, and every profile would silently render as defaults.
        profile.Roles = new Dictionary<string, RoleValue>(profile.Roles, StringComparer.OrdinalIgnoreCase);

        if (string.IsNullOrWhiteSpace(profile.Name)) profile.Name = profile.Id;
        return !string.IsNullOrWhiteSpace(profile.Id);
    }

    private static SolidColorBrush Resolve(List<string> ramp, RoleValue slot)
    {
        var index = Math.Clamp(slot.Step, 1, ramp.Count) - 1;
        var colour = TryParse(ramp[index]) ?? Colors.Gray;

        var opacity = Math.Clamp(slot.Opacity, 0, 1);
        if (opacity < 1) colour.A = (byte)Math.Round(colour.A * opacity);

        var brush = new SolidColorBrush(colour);
        brush.Freeze();
        return brush;
    }

    private static Color? TryParse(string value)
    {
        try { return (Color)ColorConverter.ConvertFromString(value); }
        catch { return null; }
    }

    /// <summary>What a role means when a profile does not mention it. These defaults are what lets the
    /// smallest useful profile be a palette and nothing else.</summary>
    private static RoleValue DefaultRole(string role) => role switch
    {
        "Normal" => new RoleValue { Step = 2 },
        "Alert" => new RoleValue { Step = 4 },
        "Critical" => new RoleValue { Step = 5 },
        "Series2" => new RoleValue { Step = 1 },
        "Series3" => new RoleValue { Step = 5 },
        "Track" => new RoleValue { Step = 3, Opacity = 0.13 },
        _ => new RoleValue { Step = 3 },   // Accent, Elevated, Series1
    };

    /// <summary>A last resort, in code rather than on disk, so that a build with no profiles at all — or a
    /// user folder that somehow shadowed every one of them — still draws something legible.</summary>
    private static ColorProfile CreateFallback() => new()
    {
        Id = "fallback",
        Name = "Fallback",
        IsBuiltIn = true,
        Palette = new ProfilePalette
        {
            Dark = ["#E6E6E6", "#C8C8C8", "#A0A0A0", "#7A7A7A", "#545454"],
            Light = ["#545454", "#6E6E6E", "#8A8A8A", "#A6A6A6", "#C2C2C2"],
        },
    };
}
