namespace Aquila.Controls;

/// <summary>
/// A piece drawn as a list, and how much of it to show. Matched structurally like the rest, so a kind that is
/// not a list cannot be handed a row count.
///
/// Set from the widget, beside a chart's window, and never from the preset: how many rows are on screen is a
/// question about the data, and a preset that carried it would change what is shown when it was tried on. So are
/// the bar, the trend and the total — each one is something to read, and trying on a preset must never take one
/// away. How they look is the preset's.
/// </summary>
public interface IListStyle
{
    /// <summary>How many rows are shown, from the top of the list.</summary>
    int ItemCount { get; set; }

    /// <summary>Whether each row has a bar under it.</summary>
    bool ShowBar { get; set; }

    /// <summary>Whether each row draws its recent history beside its number.</summary>
    bool ShowSparkline { get; set; }

    /// <summary>Whether the whole of the source's figure is written at the end of the widget's title line.</summary>
    bool ShowTotal { get; set; }
}
