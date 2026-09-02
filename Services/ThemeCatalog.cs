using System.Text.Json;
using Aquila.Models;
using Microsoft.Extensions.Logging;

namespace Aquila.Services;

/// <summary>
/// Which themes exist, and which sides each one brings.
///
/// Discovered rather than listed: finding the manifests is finding the themes, so adding a folder is all
/// it takes and no list in code can fall behind.
///
/// Themes are compiled in for now, which is why this reads the assembly rather than a folder on disk. If
/// third-party themes ever happen, the tokens should become JSON and the rules stay compiled: XAML loaded
/// at runtime instantiates types, so opening a downloaded theme would be running code, not reading data —
/// which is exactly the difference between a theme and a preset today.
/// </summary>
public sealed class ThemeCatalog(ILogger<ThemeCatalog> logger)
{
    /// <summary>The one "theme" that is not a folder: plain WPF-UI, with the user's Windows accent. That
    /// accent is the point of offering it — our RULES still apply, or the app would sit crooked depending
    /// on which theme was chosen.</summary>
    public const string Fluent = "Fluent";

    private static readonly JsonSerializerOptions _read = new() { PropertyNameCaseInsensitive = true };

    private readonly List<ThemeManifest> _themes = [];

    public IReadOnlyList<ThemeManifest> Themes => _themes;

    public void Load()
    {
        _themes.Clear();

        var assembly = typeof(ThemeCatalog).Assembly;

        // Themes/<Folder>/theme.json becomes Aquila.Themes.<Folder>.theme.json. Finding the manifests IS
        // finding the themes: a folder without one has no name to be offered under, so it is not a theme
        // yet however many token files it holds.
        foreach (var resource in assembly.GetManifestResourceNames())
        {
            if (!resource.EndsWith(".theme.json", StringComparison.OrdinalIgnoreCase)) continue;

            ThemeManifest? manifest = null;
            try
            {
                using var stream = assembly.GetManifestResourceStream(resource);
                if (stream is not null) manifest = JsonSerializer.Deserialize<ThemeManifest>(stream, _read);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Theme manifest {Resource} could not be read", resource);
            }

            if (manifest is null || (!manifest.Dark && !manifest.Light)) continue;

            // Aquila.Themes.Aquila.theme.json -> Aquila
            var parts = resource.Split('.');
            manifest.Id = parts.Length >= 4 ? parts[^3] : resource;

            if (string.IsNullOrWhiteSpace(manifest.Name)) manifest.Name = manifest.Id;

            _themes.Add(manifest);
        }

        _themes.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.CurrentCultureIgnoreCase));
    }

    /// <summary>The theme by id, or null for Fluent and for one that is no longer installed.</summary>
    public ThemeManifest? For(string? id) =>
        string.IsNullOrWhiteSpace(id) || id == Fluent
            ? null
            : _themes.FirstOrDefault(t => string.Equals(t.Id, id, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// The token file to merge, or null when there is nothing to merge.
    ///
    /// A theme with one side answers with that side whichever was asked for. Pinning the app to what the
    /// theme actually has beats rendering it half-dressed, and it is why a theme is allowed to ship one.
    /// </summary>
    public string? Overlay(string? id, bool dark)
    {
        if (For(id) is not { } theme) return null;

        var side = (dark, theme.Dark, theme.Light) switch
        {
            (true, true, _) => "Dark",
            (false, _, true) => "Light",
            (_, true, _) => "Dark",
            (_, _, true) => "Light",
            _ => null,
        };

        return side is null ? null : $"pack://application:,,,/Themes/{theme.Id}/{side}.xaml";
    }

}
