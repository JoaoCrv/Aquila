using System.Windows.Media;

namespace Aquila.Controls.Primitives;

/// <summary>How the area under a line is filled.</summary>
public enum AreaFill
{
    /// <summary>No area: the line alone.</summary>
    None,

    /// <summary>The line's colour at the look's opacity, evenly, down to the bottom.</summary>
    Flat,

    /// <summary>The line's colour at the look's opacity at the area's highest point, fading to nothing at the bottom —
    /// so a reading that sits high and steady reads as a line, not as a filled box.</summary>
    Fade,
}

// The looks: what a preset says about a shape, at the size the widget is drawn, as one value each.
//
// One value rather than a property per detail because a preset is changed whole: swapping one is an assignment, and an
// element can tell "nothing changed" by equality and skip the work. Immutable for the same reason — a look handed to two
// elements can never be changed under one of them.
//
// They know WPF and nothing of Aquila, like everything in Primitives: whoever wraps an element reads the preset and
// makes the look. Brushes in them are best frozen, since a frozen brush makes a frozen pen (see Inks), but that is the
// maker's to decide — a brush from a resource may still be changing.

/// <summary>How a bar looks: how thick, how round its ends, what its track is drawn in, and whether it is drawn whole or
/// as blocks.</summary>
/// <param name="Thickness">Across the bar, in pixels.</param>
/// <param name="Corner">The radius of its ends — of each block's, in blocks — held where it is drawn to half the part:
/// past that a corner has nothing left to round.</param>
/// <param name="Track">The whole length, under the fill. Null draws none.</param>
/// <param name="Block">How long each block is, in pixels, an LED meter's; 0 draws the bar whole
/// (<see cref="BarGeometry.Blocks"/>).</param>
/// <param name="BlockGap">The space between two blocks, in pixels.</param>
/// <param name="Fade">Whether the filled part fades in along its length — faint where the bar starts, its whole colour
/// at the tip (<see cref="BarGeometry.FadeFrom"/>), as a chart's area fades to its line.</param>
public sealed record BarLook(double Thickness, double Corner, Brush? Track, double Block = 0, double BlockGap = 2,
    bool Fade = false)
{
    /// <summary>What a bar is drawn as before anything dresses it.</summary>
    public static BarLook Default { get; } = new(4, 0, null);

    /// <summary>The same look at a share of its size — a bar under a number, at two thirds of the preset's bar. The
    /// track is kept: it is a colour, not a size.</summary>
    public BarLook Scaled(double share) => this with
    {
        Thickness = Thickness * share, Corner = Corner * share, Block = Block * share, BlockGap = BlockGap * share,
    };
}

/// <summary>How a line looks.</summary>
/// <param name="Thickness">The stroke, in pixels.</param>
/// <param name="Area">How the area under the line is filled.</param>
/// <param name="AreaOpacity">How opaque the area is at its strongest: everywhere for <see cref="AreaFill.Flat"/>, at
/// the top for <see cref="AreaFill.Fade"/>.</param>
/// <param name="Smoothness">0 draws straight segments between readings, 1 the full curve — which never passes the
/// readings it joins (<see cref="PlotGeometry.Controls"/>).</param>
/// <param name="PointSize">The diameter of a dot at each reading; 0 draws none.</param>
public sealed record LineLook(double Thickness, AreaFill Area, double AreaOpacity, double Smoothness, double PointSize)
{
    /// <summary>What a line is drawn as before anything dresses it: the chart's look before presets had one.</summary>
    public static LineLook Default { get; } = new(1.5, AreaFill.Fade, 0.30, 0.5, 0);
}

/// <summary>How a plot writes: the face and size of what it writes, the colour of what belongs to no line — the
/// scale's ends, the peak — and the halo kept round those, so a line never runs through a number.</summary>
/// <param name="Face">The typeface — a widget's notes, in its reading's face.</param>
/// <param name="Size">The size, in pixels.</param>
/// <param name="Quiet">The colour of a label that is no line's.</param>
/// <param name="Halo">Drawn round a label over the lines — the panel's own colour, so the lines seem to pass behind it.
/// Null draws none.</param>
public sealed record TextLook(Typeface Face, double Size, Brush Quiet, Brush? Halo);

/// <summary>How an arc looks.</summary>
/// <param name="Thickness">The stroke, in pixels, shared by the arc and its track.</param>
/// <param name="Caps">The ends of the arc, and of the track when it stops short of a whole turn.</param>
/// <param name="Track">The whole sweep, under the arc. Null draws none.</param>
public sealed record ArcLook(double Thickness, PenLineCap Caps, Brush? Track)
{
    /// <summary>What an arc is drawn as before anything dresses it.</summary>
    public static ArcLook Default { get; } = new(10, PenLineCap.Round, null);
}
