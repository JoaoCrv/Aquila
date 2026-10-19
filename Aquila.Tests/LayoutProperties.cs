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
}
