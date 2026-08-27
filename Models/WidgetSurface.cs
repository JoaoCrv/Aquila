namespace Aquila.Models;

/// <summary>
/// Where a widget lives, written as one prefixed string: <c>screen:DEL-A1B2-DP1</c> today,
/// <c>page:overview</c> when pages arrive.
///
/// One field rather than a ScreenKey beside a PageId with one of them always empty — two fields where only
/// one can be set is a shape that invites both to be set, and nothing to say which wins. The prefix makes
/// the namespaces explicit and costs one parse where the surface is resolved.
///
/// Only the screen half is here because only screens exist. A surface with any other prefix simply is not a
/// screen, which is all the resolver needs to know until there is something else to resolve.
/// </summary>
public static class WidgetSurface
{
    private const string ScreenPrefix = "screen:";

    /// <summary>The surface string for a monitor, by its stable key.</summary>
    public static string Screen(string key) => ScreenPrefix + key;

    /// <summary>The monitor key a surface names, or null when it names something else.</summary>
    public static string? ScreenKeyOf(string? surface) =>
        surface is not null && surface.StartsWith(ScreenPrefix, StringComparison.Ordinal)
            ? surface[ScreenPrefix.Length..]
            : null;
}
