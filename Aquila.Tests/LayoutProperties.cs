using System.Text.Json;
using System.Text.Json.Nodes;
using Aquila.Models;
using Aquila.Services;
using FsCheck.Xunit;
using Xunit;

namespace Aquila.Tests;

/// <summary>
/// The desktop layout keeps what it cannot read. An older Aquila meeting a newer layout used to drop every widget of
/// a kind it did not know, and the next save erased them for good — five widgets were lost that way in the 2.3.0
/// rehearsals. Now they are kept as they arrived and written back.
/// </summary>
public sealed class LayoutProperties
{
    /// <summary>Any mix of widgets this build reads and widgets from a "newer" one — kinds it has never heard of,
    /// carrying fields of their own — comes back from a save with the unknown ones intact and in their order.</summary>
    [Property(MaxTest = 200)]
    public bool Widgets_this_build_cannot_read_survive_a_save(byte known, byte[] future)
    {
        var futures = (future ?? []).Take(6).ToArray();
        var elements = Enumerable.Range(0, known % 4)
            .Select(i => $$"""{ "Kind": "Clock", "X": {{i}} }""")
            .Concat(futures.Select((v, i) =>
                $$"""{ "Kind": "Hologram{{v}}", "Shimmer": {{v}}, "Layers": [ {{i}}, "glow", { "deep": true } ] }"""));
        var text = "[" + string.Join(",", elements) + "]";

        var (widgets, unread) = DesktopLayoutService.Parse(text);
        var (widgetsAgain, unreadAgain) = DesktopLayoutService.Parse(DesktopLayoutService.Write(widgets, unread));

        return widgets.Count == known % 4
            && unread.Count == futures.Length
            && widgetsAgain.Count == widgets.Count
            && unreadAgain.Count == unread.Count
            && unread.Zip(unreadAgain).All(pair => JsonNode.DeepEquals(JsonNode.Parse(pair.First), JsonNode.Parse(pair.Second)));
    }

    /// <summary>With nothing unread, the layout is written exactly as it always was: the change costs nobody whose
    /// layout this build fully understands.</summary>
    [Fact]
    public void A_layout_read_in_full_is_written_as_before()
    {
        var (widgets, unread) = DesktopLayoutService.Parse("""[ { "Kind": "Clock" }, { "Kind": "Text" } ]""");

        Assert.Empty(unread);
        Assert.Equal(DesktopLayoutService.Serialize(widgets), DesktopLayoutService.Write(widgets, unread));
    }

    /// <summary>A file that is not a layout at all is refused rather than read as empty — the caller then keeps a
    /// copy of it before anything can write over it.</summary>
    [Theory]
    [InlineData("""{ "format": 2, "widgets": [] }""")]
    [InlineData("""[ { "Kind": "Clock" """)]
    public void Text_that_is_not_a_layout_is_refused(string text)
    {
        Assert.ThrowsAny<JsonException>(() => DesktopLayoutService.Parse(text));
    }

    /// <summary>
    /// A sparkline is read as the Chart it became, its number in the title — or nowhere, if it had been turned off —
    /// and everything else as it was. The old two-reading chart keeps its own kind, and writes no numbers, as it never
    /// did (docs/widget-grafico).
    /// </summary>
    [Theory]
    [InlineData(true, ReadingPlace.Title)]
    [InlineData(false, ReadingPlace.None)]
    public void A_sparkline_is_read_as_a_chart(bool showValue, ReadingPlace expected)
    {
        var (widgets, _) = DesktopLayoutService.Parse($$"""
            [ { "Kind": "MiniSparkline", "Title": "CPU Temp", "Width": 240, "Height": 100, "PointCount": 120,
                "ShowValue": {{(showValue ? "true" : "false")}}, "Series": [ { "SensorIdentifier": "/cpu/0/temperature/0" } ] } ]
            """);

        var chart = Assert.Single(widgets);
        Assert.Equal(DesktopWidgetKind.Chart, chart.Kind);
        Assert.Equal(expected, chart.ReadingsAt);
        Assert.Equal(("CPU Temp", 240d, 100d, 120), (chart.Title, chart.Width, chart.Height, chart.PointCount));
        Assert.Equal("/cpu/0/temperature/0", Assert.Single(chart.Series).SensorIdentifier);
    }

    /// <summary>A reading's name is kept through a save, and a reading written before names existed has none — the
    /// sensor's own is written in its place.</summary>
    [Fact]
    public void A_reading_s_name_survives_a_save()
    {
        var (widgets, unread) = DesktopLayoutService.Parse("""
            [ { "Kind": "Chart", "Series": [ { "SensorIdentifier": "/nvme/0/temperature/0", "Label": "System drive" },
                                             { "SensorIdentifier": "/nvme/1/temperature/0" } ] } ]
            """);
        var (again, _) = DesktopLayoutService.Parse(DesktopLayoutService.Write(widgets, unread));

        Assert.Equal(["System drive", ""], Assert.Single(again).Series.Select(series => series.Label));
    }

    /// <summary>A meter is read as the Bars it became — one bar lying down, its number beside it, as every built-in
    /// preset drew it — with its number and its limits as they were (docs/widget-barras).</summary>
    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void A_meter_is_read_as_bars(bool showValue, bool showLimits)
    {
        var (widgets, _) = DesktopLayoutService.Parse($$"""
            [ { "Kind": "SensorMeter", "Title": "CPU Power", "Width": 240, "Height": 80,
                "ShowValue": {{(showValue ? "true" : "false")}}, "ShowLimits": {{(showLimits ? "true" : "false")}},
                "Series": [ { "SensorIdentifier": "/cpu/0/power/0" } ] } ]
            """);

        var bars = Assert.Single(widgets);
        Assert.Equal(DesktopWidgetKind.BarChart, bars.Kind);
        Assert.Equal((BarDirection.Horizontal, BarLayout.Beside), (bars.BarDirection, bars.BarLayout));
        Assert.Equal((showValue, showLimits), (bars.ShowValue, bars.ShowLimits));
        Assert.Equal((false, false, false), (bars.ShowNames, bars.ColourByReading, bars.ShowPeak));
        Assert.Equal(("CPU Power", 240d, 80d), (bars.Title, bars.Width, bars.Height));
    }

    /// <summary>The old Bars keep their kind and are drawn as they were: their direction, their numbers beside — under
    /// standing bars — and no names, bands or peak.</summary>
    [Fact]
    public void Old_bars_keep_their_kind()
    {
        var (widgets, _) = DesktopLayoutService.Parse("""[ { "Kind": "Bars", "BarDirection": "Horizontal", "Width": 260, "Height": 140 } ]""");

        var bars = Assert.Single(widgets);
        Assert.Equal(DesktopWidgetKind.Bars, bars.Kind);
        Assert.Equal((BarDirection.Horizontal, BarLayout.Beside), (bars.BarDirection, bars.BarLayout));
        Assert.Equal((false, false, false), (bars.ShowNames, bars.ColourByReading, bars.ShowPeak));
    }

    [Fact]
    public void An_old_chart_keeps_its_kind_and_writes_no_numbers()
    {
        var (widgets, _) = DesktopLayoutService.Parse("""[ { "Kind": "SparklineChart", "Width": 300, "Height": 160 } ]""");

        var chart = Assert.Single(widgets);
        Assert.Equal(DesktopWidgetKind.SparklineChart, chart.Kind);
        Assert.Equal(ReadingPlace.None, chart.ReadingsAt);
    }
}
