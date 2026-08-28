namespace Aquila.Models;

/// <summary>
/// One widget as the Widgets page lists it: what it is, what it reads, and whether it can draw at all.
///
/// A widget that draws nothing is invisible on the desktop, and invisible is indistinguishable from
/// deleted. The list exists so the two can be told apart — a definition can be present, correct and
/// unrenderable, and until now the only way to find that out was to read widgets.json by hand.
/// </summary>
/// <param name="Definition">The live definition, so a row can act on it.</param>
/// <param name="Kind">The kind's readable name, from the catalog.</param>
/// <param name="Title">The widget's own title, or its kind when it has none.</param>
/// <param name="Detail">What it draws, or where it lives when it draws nothing.</param>
/// <param name="Problem">Why it cannot draw, or null when it can. The reason, not just the fact —
/// "no reading chosen" and "its sensor is gone" call for different fixes.</param>
public sealed record WidgetSummary(
    DesktopWidgetDefinition Definition,
    string Kind,
    string Title,
    string Detail,
    string? Problem)
{
    public bool HasProblem => Problem is not null;
}
