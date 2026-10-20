namespace Aquila.Controls;

/// <summary>
/// A piece drawn as a list, and how much of it to show. Matched structurally like the rest, so a kind that is
/// not a list cannot be handed a row count.
///
/// Set from the widget, beside a chart's window, and never from the preset: how many rows are on screen is a
/// question about the data, and a preset that carried it would change what is shown when it was tried on.
/// </summary>
public interface IListStyle
{
    /// <summary>How many rows are shown, from the top of the list.</summary>
    int ItemCount { get; set; }
}
