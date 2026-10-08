using System.IO;
using Aquila.Models;
using Aquila.Services;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Aquila.Tests;

/// <summary>
/// Importing a preset is the one place Aquila reads something somebody else wrote. The rule: whatever the file
/// holds, the import either yields a preset the app can draw with, or refuses it. It never throws — an exception
/// here is the application falling over because of a file someone shared.
/// </summary>
public sealed class PresetImportProperties : IDisposable
{
    private readonly string _folder = Directory.CreateTempSubdirectory("aquila-tests-").FullName;

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    [Property(MaxTest = 500)]
    public Property Any_file_is_imported_drawable_or_refused() =>
        Prop.ForAll(PresetFiles.Any(), json => Import(json) is not { } preset || Drawable(preset));

    /// <summary>The four built-in presets, which ship inside the app, import whole. Example-based rather than a
    /// property: there are exactly four, and each must work.</summary>
    [Fact]
    public void Every_built_in_preset_imports_drawable()
    {
        var assembly = typeof(Preset).Assembly;
        var names = assembly.GetManifestResourceNames().Where(n => n.Contains(".Presets.") && n.EndsWith(".json")).ToList();
        Assert.NotEmpty(names);

        foreach (var name in names)
        {
            using var stream = assembly.GetManifestResourceStream(name)!;
            using var reader = new StreamReader(stream);
            var preset = Import(reader.ReadToEnd());

            Assert.True(preset is not null, $"{name} was refused");
            Assert.True(Drawable(preset!), $"{name} is not drawable");
        }
    }

    /// <summary>
    /// Files the property found, or would have, kept as fixed examples — a random search may not draw the same
    /// file twice, and these must stay fixed. The first was found on the property's first run: accepted, then
    /// fatal to the first widget that wore it.
    /// </summary>
    [Theory]
    [InlineData("""{"id":"x","background":null}""")]                                // a section written as null
    [InlineData("""{"id":"x","gauge":{"track":null}}""")]                           // a nested one
    [InlineData("""{"id":"x","ramps":null}""")]                                     // no ramps, said as null
    [InlineData("""{"id":"x","ramps":{"primary":null}}""")]                        // a ramp written as null
    [InlineData("""{"id":"x","ramps":{"primary":{},"Primary":{"normal":"#FFF"}}}""")] // one name, two spellings
    public void Known_hostile_files_import_drawable(string json)
    {
        var preset = Import(json);
        Assert.NotNull(preset);
        Assert.True(Drawable(preset!));
    }

    private Preset? Import(string json)
    {
        var path = Path.Combine(_folder, $"{Guid.NewGuid():N}.json");
        File.WriteAllText(path, json);
        return NewService().Import(path);
    }

    private static PresetService NewService() =>
        new(NullLogger<PresetService>.Instance,
            new SettingsService(NullLogger<SettingsService>.Instance),
            new NoticeService(NullLogger<NoticeService>.Instance));

    /// <summary>What drawing a widget will touch without checking: every section, the gauge's track, and a ramp
    /// for the first line — which <see cref="Preset.RampFor(string?, int)"/> must find.</summary>
    private static bool Drawable(Preset p) =>
        !string.IsNullOrWhiteSpace(p.Id)
        && !string.IsNullOrWhiteSpace(p.Name)
        && p.Format == Preset.CurrentFormat
        && p.Ramps is { Count: > 0 }
        && p.Ramps.Values.All(r => r is not null)
        && p.Background is not null && p.Border is not null && p.Line is not null && p.Bar is not null
        && p.Number is not null && p.Title is not null && p.Value is not null
        && p.Gauge is { Track: not null }
        && p.RampFor(null, 0) is not null;
}
