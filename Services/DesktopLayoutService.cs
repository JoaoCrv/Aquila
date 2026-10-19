using System.IO;
using System.Text;
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
    /// And what it cannot read it KEEPS, as it arrived, and writes back on every save — the presets' rule of
    /// loading as far as understood, applied to the layout. Dropped instead, the next save erased it: an older
    /// Aquila meeting a newer layout destroyed every widget of a kind it did not know (it happened in the 2.3.0
    /// rehearsals — five widgets gone). Kept, they wait unseen for a build that can draw them.
    /// </summary>
    public List<DesktopWidgetDefinition> Load()
    {
        _unread = [];
        if (!File.Exists(AquilaPaths.Widgets)) return [];

        try
        {
            var (widgets, unread) = Parse(File.ReadAllText(AquilaPaths.Widgets), (raw, ex) =>
                logger.LogWarning(ex, "A widget could not be read; it is kept as it is and written back: {Widget}", raw));
            _unread = unread;
            return widgets;
        }
        catch (Exception ex)
        {
            // The whole file, not one widget: corrupt, or a layout from a newer Aquila that is no longer a list. It
            // is copied aside before anything can write over it, since the next save replaces it with whatever
            // this build made of nothing.
            var copy = Path.ChangeExtension(AquilaPaths.Widgets, ".unreadable.json");
            try { File.Copy(AquilaPaths.Widgets, copy, overwrite: true); }
            catch (Exception copyFailed) { logger.LogWarning(copyFailed, "Could not keep a copy of the unreadable layout"); }

            logger.LogWarning(ex, "Failed to load desktop widget layout, starting empty; the file was kept as {Copy}", copy);
            return [];
        }
    }

    /// <summary>Widgets the last <see cref="Load"/> could not read, as they arrived — every <see cref="Save"/>
    /// writes them back.</summary>
    private IReadOnlyList<string> _unread = [];

    /// <summary>
    /// The widgets a layout holds, and — whole, as their raw JSON — the ones this build cannot read. Throws when
    /// the text is not a layout at all. Static and without files, so the tests can hold it to its promise.
    /// </summary>
    public static (List<DesktopWidgetDefinition> Widgets, List<string> Unread) Parse(string text,
        Action<string, Exception>? skipped = null)
    {
        using var document = JsonDocument.Parse(text, _parse);
        if (document.RootElement.ValueKind != JsonValueKind.Array)
            throw new JsonException("The layout is not a list.");

        var widgets = new List<DesktopWidgetDefinition>();
        var unread = new List<string>();

        foreach (var element in document.RootElement.EnumerateArray())
        {
            try
            {
                if (element.Deserialize<DesktopWidgetDefinition>(_json) is { } widget)
                    widgets.Add(Migrate(widget, element));
            }
            catch (Exception ex)
            {
                var raw = element.GetRawText();
                unread.Add(raw);
                skipped?.Invoke(raw, ex);
            }
        }

        return (widgets, unread);
    }

    /// <summary>
    /// The text <see cref="Save"/> writes: the widgets, then the unread ones untouched. With nothing unread it is
    /// exactly <see cref="Serialize"/>, so a layout this build fully understands is written as it always was.
    /// </summary>
    public static string Write(IEnumerable<DesktopWidgetDefinition> widgets, IReadOnlyList<string> unread)
    {
        if (unread.Count == 0) return Serialize(widgets);

        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer, new JsonWriterOptions { Indented = true }))
        {
            writer.WriteStartArray();
            foreach (var widget in widgets)
                JsonSerializer.Serialize(writer, widget, _json);
            foreach (var raw in unread)
            {
                using var element = JsonDocument.Parse(raw, _parse);
                element.RootElement.WriteTo(writer);
            }
            writer.WriteEndArray();
        }
        return Encoding.UTF8.GetString(buffer.ToArray());
    }

    /// <summary>
    /// Fills in what an older layout spelled differently.
    ///
    /// Read from the raw JSON rather than from the definition, because that is the only place the old names
    /// still exist — the model has no SensorIdentifier or ScreenKey to migrate FROM, which is exactly why
    /// this cannot live with the other one-time fix-ups in SettingsService.
    ///
    /// Guarded on the NEW field being empty rather than on the old one being present, so a widget that has
    /// since been given a reading is never overwritten by the one it was pinned from.
    ///
    /// Not migrated: the old AccentKey. Those named a hardware colour — Aquila.Cpu — in a scheme where a
    /// series now names a ramp and follows its own reading. There is no honest mapping, and the default
    /// (follow the reading in the preset's primary ramp) is what the widget would have been given today.
    /// </summary>
    private static DesktopWidgetDefinition Migrate(DesktopWidgetDefinition widget, JsonElement element)
    {
        if (widget.Series.Count == 0 &&
            element.TryGetProperty("SensorIdentifier", out var sensor) &&
            sensor.GetString() is { Length: > 0 } identifier)
            widget.Series = [new WidgetSeries { SensorIdentifier = identifier }];

        if (string.IsNullOrWhiteSpace(widget.Surface) &&
            element.TryGetProperty("ScreenKey", out var screen) &&
            screen.GetString() is { Length: > 0 } key)
            widget.Surface = WidgetSurface.Screen(key);

        return widget;
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
            File.WriteAllText(AquilaPaths.Widgets, Write(widgets, _unread));
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to save desktop widget layout");
        }
    }
}
