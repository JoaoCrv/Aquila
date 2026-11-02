using System.Windows;
using Aquila.Models;

namespace Aquila.Services.EditMode;

/// <summary>A widget as the edit bar lists it: what it is, the screen it is drawn on (null when it draws nothing),
/// and its box on that screen.</summary>
public sealed record WidgetPlace(WidgetSummary Summary, string? Screen, Rect Box);

/// <summary>
/// Which widget draws in front of which, on one screen — the rule the canvas follows, stated once for everything
/// that has to say it in words: the edit bar's list, front to back, and "behind 2 widgets" beside a backdrop.
///
/// The canvas draws a higher <see cref="DesktopWidgetDefinition.ZIndex"/> in front, and at the same one the widget
/// added later — the later in the layout, since it is added in that order. Pure, and given only one screen's
/// widgets, in layout order: widgets on two screens never cover each other.
/// </summary>
public static class Layers
{
    /// <summary>The layers a widget may be put on — the range the editor's slider offered, kept when the buttons
    /// replaced it.</summary>
    public const int Lowest = -5;
    public const int Highest = 10;
    /// <summary>Whether the widget at <paramref name="j"/> draws in front of the one at <paramref name="i"/>.</summary>
    public static bool InFront(IReadOnlyList<int> zIndexes, int j, int i) =>
        zIndexes[j] > zIndexes[i] || (zIndexes[j] == zIndexes[i] && j > i);

    /// <summary>The positions of the widgets, front first.</summary>
    public static IReadOnlyList<int> FrontToBack(IReadOnlyList<int> zIndexes) =>
        [.. Enumerable.Range(0, zIndexes.Count).OrderByDescending(i => zIndexes[i]).ThenByDescending(i => i)];

    /// <summary>How many widgets draw in front of the one at <paramref name="index"/> and cover part of it. Boxes
    /// that only touch cover nothing.</summary>
    public static int Behind(IReadOnlyList<Rect> boxes, IReadOnlyList<int> zIndexes, int index)
    {
        var count = 0;
        for (var other = 0; other < boxes.Count; other++)
            if (other != index && InFront(zIndexes, other, index) && Overlap(boxes[other], boxes[index]))
                count++;
        return count;
    }

    /// <summary>How many widgets the one at <paramref name="index"/> draws in front of and covers part of.</summary>
    public static int InFrontOf(IReadOnlyList<Rect> boxes, IReadOnlyList<int> zIndexes, int index)
    {
        var count = 0;
        for (var other = 0; other < boxes.Count; other++)
            if (other != index && InFront(zIndexes, index, other) && Overlap(boxes[other], boxes[index]))
                count++;
        return count;
    }

    /// <summary>
    /// The layer that takes the widget at <paramref name="index"/> one step forward: just in front of the lowest of
    /// the widgets that overlap it from in front — so every click passes one widget that can be seen to pass, where a
    /// slider's 0 to 1 could change nothing on screen. Its own layer when nothing overlaps it from in front; held
    /// between <see cref="Lowest"/> and <see cref="Highest"/>, so at the ceiling it may not get past.
    /// </summary>
    public static int BringForward(IReadOnlyList<Rect> boxes, IReadOnlyList<int> zIndexes, int index)
    {
        var lowest = -1;
        for (var other = 0; other < boxes.Count; other++)
            if (other != index && InFront(zIndexes, other, index) && Overlap(boxes[other], boxes[index])
                && (lowest < 0 || InFront(zIndexes, lowest, other)))
                lowest = other;

        if (lowest < 0) return Held(zIndexes[index]);

        // The same layer is enough when it comes later in the layout; otherwise one above.
        return Held(index > lowest ? zIndexes[lowest] : zIndexes[lowest] + 1);
    }

    /// <summary>The same, backwards: just behind the highest of the widgets it overlaps from in front.</summary>
    public static int SendBackward(IReadOnlyList<Rect> boxes, IReadOnlyList<int> zIndexes, int index)
    {
        var highest = -1;
        for (var other = 0; other < boxes.Count; other++)
            if (other != index && InFront(zIndexes, index, other) && Overlap(boxes[other], boxes[index])
                && (highest < 0 || InFront(zIndexes, other, highest)))
                highest = other;

        if (highest < 0) return Held(zIndexes[index]);

        return Held(index < highest ? zIndexes[highest] : zIndexes[highest] - 1);
    }

    private static int Held(int layer) => Math.Clamp(layer, Lowest, Highest);

    private static bool Overlap(Rect a, Rect b) =>
        !a.IsEmpty && !b.IsEmpty
        && a.Left < b.Right && b.Left < a.Right && a.Top < b.Bottom && b.Top < a.Bottom;
}
