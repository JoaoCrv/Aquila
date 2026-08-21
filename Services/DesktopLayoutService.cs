using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Aquila.Models;
using Microsoft.Extensions.Logging;

namespace Aquila.Services;

/// <summary>
/// Loads and saves the desktop widget layout (<c>widgets.json</c>), kept separate from
/// <see cref="SettingsService"/> on purpose: this is a document (a list that grows as the user pins
/// widgets), not a set of preferences, and keeping it in its own file leaves room for the shareable
/// layouts/templates direction without bloating settings.json.
///
/// Failures are never fatal — a missing or corrupt file means "no widgets yet", so a bad layout can't
/// stop the app from starting.
/// </summary>
public sealed class DesktopLayoutService(ILogger<DesktopLayoutService> logger)
{
    private static readonly JsonSerializerOptions _json = new()
    {
        WriteIndented = true,
        // Enum names rather than numbers: the file stays readable and survives reordering of the enum.
        Converters = { new JsonStringEnumConverter() },
    };

    public List<DesktopWidgetDefinition> Load()
    {
        try
        {
            if (!File.Exists(AquilaPaths.Widgets)) return [];
            return JsonSerializer.Deserialize<List<DesktopWidgetDefinition>>(
                File.ReadAllText(AquilaPaths.Widgets), _json) ?? [];
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to load desktop widget layout, starting empty");
            return [];
        }
    }

    /// <summary>
    /// The exact text <see cref="Save"/> would write. Exposed so "has anything changed?" can be answered by
    /// comparing two layouts as they would be stored — anything this serializer ignores is, by definition,
    /// not a change worth prompting about, and no hand-written comparison can drift away from it.
    /// </summary>
    public static string Serialize(IEnumerable<DesktopWidgetDefinition> widgets) =>
        JsonSerializer.Serialize(widgets, _json);

    public void Save(IEnumerable<DesktopWidgetDefinition> widgets)
    {
        try
        {
            Directory.CreateDirectory(AquilaPaths.Root);
            File.WriteAllText(AquilaPaths.Widgets, Serialize(widgets));
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to save desktop widget layout");
        }
    }
}
