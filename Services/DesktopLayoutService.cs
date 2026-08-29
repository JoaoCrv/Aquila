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

    // The layout is a document a user may open and edit by hand, so it is read the way a hand-written
    // file deserves to be read.
    private static readonly JsonDocumentOptions _parse = new()
    {
        AllowTrailingCommas = true,
        CommentHandling = JsonCommentHandling.Skip,
    };

    /// <summary>
    /// Reads the layout, one widget at a time.
    ///
    /// Deliberately not a single <c>Deserialize&lt;List&lt;...&gt;&gt;</c>. That throws on the first element
    /// it cannot read and takes the whole file with it, so one widget naming a kind this build no longer has
    /// costs the user every widget they own — which is exactly what happened when the Words kind became Text.
    /// Read element by element, an unreadable widget costs only itself.
    ///
    /// The skipped element is logged in full, because the next save rewrites the file without it and the log
    /// is then the only place it still exists.
    /// </summary>
    public List<DesktopWidgetDefinition> Load()
    {
        if (!File.Exists(AquilaPaths.Widgets)) return [];

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(File.ReadAllText(AquilaPaths.Widgets), _parse);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to load desktop widget layout, starting empty");
            return [];
        }

        using (document)
        {
            if (document.RootElement.ValueKind != JsonValueKind.Array)
            {
                logger.LogWarning("Desktop widget layout is not a list, starting empty");
                return [];
            }

            var widgets = new List<DesktopWidgetDefinition>();

            foreach (var element in document.RootElement.EnumerateArray())
            {
                try
                {
                    if (element.Deserialize<DesktopWidgetDefinition>(_json) is { } widget)
                        widgets.Add(widget);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "A widget could not be read and was skipped: {Widget}",
                        element.ToString());
                }
            }

            return widgets;
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
