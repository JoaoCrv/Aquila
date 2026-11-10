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
        foreach (var (name, preset) in BuiltIns())
        {
            Assert.True(preset is not null, $"{name} was refused");
            Assert.True(Drawable(preset!), $"{name} is not drawable");
        }
    }

    /// <summary>A chart draws up to four lines, each in the ramp of its place, so every built-in preset has four ramps
    /// in that order (docs/widget-grafico) — and four lines are four colours, except in Classic, whose ramps all say
    /// only the state.</summary>
    [Fact]
    public void Every_built_in_preset_colours_a_chart_s_four_lines()
    {
        foreach (var (name, preset) in BuiltIns())
        {
            Assert.NotNull(preset);
            var lines = Enumerable.Range(0, 4).ToList();

            Assert.Equal(["primary", "secondary", "tertiary", "quaternary"], lines.Select(i => preset!.RampNameFor(null, i)));
            Assert.True(lines.Select(i => preset!.RampFor(null, i).Normal).Distinct().Count() == (preset!.Id == "classic" ? 1 : 4),
                $"{name}: four lines in the wrong number of colours");
        }
    }

    /// <summary>Bars share one ramp (docs/widget-barras): sixteen cores are sixteen of the same thing, so every bar that
    /// names no ramp is in the first — the colour of a bar says its state, not which bar it is — and one that names a
    /// ramp keeps it. In a legend, where a swatch's colour tells one reading from the next, each takes the ramp of its
    /// place again, as a chart's lines always do.</summary>
    [Fact]
    public void Every_bar_takes_the_first_ramp_unless_it_names_one()
    {
        var bars = WidgetCatalog.For(DesktopWidgetKind.BarChart);
        var old = WidgetCatalog.For(DesktopWidgetKind.Bars);
        var chart = WidgetCatalog.For(DesktopWidgetKind.Chart);

        foreach (var (name, preset) in BuiltIns())
        {
            Assert.NotNull(preset);
            var each = Enumerable.Range(0, BarsTileReadings).ToList();

            foreach (var place in new[] { ReadingPlace.None, ReadingPlace.Title, ReadingPlace.Beside, ReadingPlace.Above })
            {
                Assert.All(each, i => Assert.Equal("primary", preset!.RampNameFor(null, bars.RampPlace(i, place))));
                Assert.All(each, i => Assert.Equal("primary", preset!.RampNameFor(null, old.RampPlace(i, place))));
            }

            Assert.Equal("secondary", preset!.RampNameFor("secondary", bars.RampPlace(5, ReadingPlace.Above)));
            Assert.Equal(["primary", "secondary", "tertiary", "quaternary"],
                Enumerable.Range(0, 4).Select(i => preset.RampNameFor(null, bars.RampPlace(i, ReadingPlace.Legend))));
            Assert.Equal("tertiary", preset.RampNameFor(null, chart.RampPlace(2, ReadingPlace.Title)));
        }
    }

    private const int BarsTileReadings = Aquila.Controls.BarsTile.MostReadings;

    /// <summary>The built-in presets, as the app ships them, each imported.</summary>
    private IEnumerable<(string Name, Preset? Preset)> BuiltIns()
    {
        var assembly = typeof(Preset).Assembly;
        var names = assembly.GetManifestResourceNames().Where(n => n.Contains(".Presets.") && n.EndsWith(".json")).ToList();
        Assert.NotEmpty(names);

        foreach (var name in names)
        {
            using var stream = assembly.GetManifestResourceStream(name)!;
            using var reader = new StreamReader(stream);
            yield return (name, Import(reader.ReadToEnd()));
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
    [InlineData("""{"id":"x","title":{"background":null,"border":null}}""")]        // the title's box, said as null
    [InlineData("""{"id":"x","ramps":null}""")]                                     // no ramps, said as null
    [InlineData("""{"id":"x","ramps":{"primary":null}}""")]                        // a ramp written as null
    [InlineData("""{"id":"x","ramps":{"primary":{},"Primary":{"normal":"#FFF"}}}""")] // one name, two spellings
    public void Known_hostile_files_import_drawable(string json)
    {
        var preset = Import(json);
        Assert.NotNull(preset);
        Assert.True(Drawable(preset!));
    }

    /// <summary>
    /// A preset written before titles had a box draws its titles as it did: no background, no outline, no padding,
    /// and four pixels from the content. Only the presets shipped with the app took the new heading. And an outline
    /// given only a thickness gets none of the panel's padding or corners — the reason the title's border is a class
    /// of its own, since the reader builds what it meets from that class's defaults.
    /// </summary>
    [Theory]
    [InlineData("""{"id":"x","title":{"size":11,"align":"Center"}}""", 0)]
    [InlineData("""{"id":"x","title":{"border":{"thickness":1}}}""", 1)]
    public void A_title_is_given_no_box_it_did_not_ask_for(string json, double thickness)
    {
        var title = Import(json)!.Title;

        Assert.Equal(4, title.Gap);
        Assert.False(title.Bleed);
        Assert.Equal(0, title.Background.Opacity);
        Assert.Equal(thickness, title.Border.Thickness);
        Assert.Equal(0, title.Border.CornerRadius);
        Assert.Equal(0, title.Border.Padding);
    }

    /// <summary>
    /// A bar's blocks survive an export and an import (docs/widget-barras), and a preset written when it said where the
    /// meter's number went still reads — "layout" is kept, unread. Absent, a bar is drawn whole, two pixels apart if it
    /// is ever given blocks; the built-ins draw theirs whole.
    /// </summary>
    [Fact]
    public void A_bar_s_blocks_survive_an_export_and_an_old_layout_still_reads()
    {
        var led = Import("""{"id":"led","bar":{"thickness":14,"block":5,"blockGap":2,"layout":"Above","fill":"Gradient"}}""");
        Assert.NotNull(led);

        var path = Path.Combine(_folder, "led-out.json");
        NewService().Export(led!, path);
        var again = NewService().Import(path);

        Assert.NotNull(again);
        Assert.Equal((5d, 2d, BarLayout.Above, BarShade.Gradient),
            (again!.Bar.Block, again.Bar.BlockGap, again.Bar.Layout, again.Bar.Fill));
        Assert.True(Drawable(again));

        var plain = Import("""{"id":"plain","bar":{"thickness":6}}""")!;
        Assert.Equal((0d, 2d, BarShade.Solid), (plain.Bar.Block, plain.Bar.BlockGap, plain.Bar.Fill));
        Assert.All(BuiltIns(), builtIn => Assert.Equal(0d, builtIn.Preset!.Bar.Block));
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
        && p.Number is not null && p.Title is { Background: not null, Border: not null } && p.Value is not null
        && p.Gauge is { Track: not null }
        && p.RampFor(null, 0) is not null;
}
