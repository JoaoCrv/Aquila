using System.Windows;
using Aquila.Services.EditMode;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;

namespace Aquila.Tests;

/// <summary>
/// Which widget draws in front of which (<see cref="Layers"/>): the edit bar lists a screen's widgets in that order
/// and says how many cover each one, so a list that disagreed with the canvas would send someone looking for a
/// widget in the wrong place.
/// </summary>
public sealed class LayersProperties
{
    /// <summary>One screen's widgets, in layout order: small boxes on a small screen, so they overlap often, touch
    /// edge to edge now and then, and share layers.</summary>
    private static Gen<(Rect[] Boxes, int[] Layers)> Screens() =>
        from count in Gen.Choose(0, 12)
        from widgets in (from x in Gen.Choose(0, 20)
                         from y in Gen.Choose(0, 20)
                         from width in Gen.Choose(1, 8)
                         from height in Gen.Choose(1, 8)
                         from layer in Gen.Choose(-2, 3)
                         select (Box: new Rect(x * 5, y * 5, width * 5, height * 5), Layer: layer)).ArrayOf(count)
        select (widgets.Select(w => w.Box).ToArray(), widgets.Select(w => w.Layer).ToArray());

    /// <summary>A screen as Screens() is, with any layer from just past the range on either side.</summary>
    private static Gen<(Rect[] Boxes, int[] Layers, int Index)> Targets(int low, int high) =>
        from count in Gen.Choose(1, 12)
        from widgets in (from x in Gen.Choose(0, 20)
                         from y in Gen.Choose(0, 20)
                         from width in Gen.Choose(1, 8)
                         from height in Gen.Choose(1, 8)
                         from layer in Gen.Choose(low, high)
                         select (Box: new Rect(x * 5, y * 5, width * 5, height * 5), Layer: layer)).ArrayOf(count)
        from index in Gen.Choose(0, count - 1)
        select (widgets.Select(w => w.Box).ToArray(), widgets.Select(w => w.Layer).ToArray(), index);

    private static int[] With(int[] layers, int index, int layer)
    {
        var after = (int[])layers.Clone();
        after[index] = layer;
        return after;
    }

    /// <summary>Forward or back, the layer that comes out is always one the editor allows, whatever went in.</summary>
    [Property(MaxTest = 300)]
    public Property A_new_layer_is_always_in_range() =>
        Prop.ForAll(Targets(Layers.Lowest - 3, Layers.Highest + 3).ToArbitrary(), target =>
        {
            var (boxes, layers, index) = target;
            var forward = Layers.BringForward(boxes, layers, index);
            var back = Layers.SendBackward(boxes, layers, index);
            return forward is >= Layers.Lowest and <= Layers.Highest && back is >= Layers.Lowest and <= Layers.Highest;
        });

    /// <summary>Brought forward, a widget covered by others ends covered by fewer — it passes one that can be seen to
    /// pass — wherever the ceiling leaves room; sent back, a widget covering others covers fewer. Nothing else moves:
    /// the rule gives the one widget its layer and touches no other.</summary>
    [Property(MaxTest = 500)]
    public Property Each_step_passes_a_widget_where_there_is_room() =>
        Prop.ForAll(Targets(Layers.Lowest, Layers.Highest).ToArbitrary(), target =>
        {
            var (boxes, layers, index) = target;

            var behind = Layers.Behind(boxes, layers, index);
            var roomAbove = layers.Where((_, i) => i != index).All(layer => layer < Layers.Highest);
            var forward = With(layers, index, Layers.BringForward(boxes, layers, index));
            var passedForward = behind == 0 || !roomAbove || Layers.Behind(boxes, forward, index) < behind;

            var inFront = Layers.InFrontOf(boxes, layers, index);
            var roomBelow = layers.Where((_, i) => i != index).All(layer => layer > Layers.Lowest);
            var back = With(layers, index, Layers.SendBackward(boxes, layers, index));
            var passedBack = inFront == 0 || !roomBelow || Layers.InFrontOf(boxes, back, index) < inFront;

            return passedForward && passedBack;
        });

    /// <summary>A widget that overlaps nothing is described as overlapping nothing, and neither step moves it.</summary>
    [Property(MaxTest = 200)]
    public Property A_widget_alone_stays_where_it_is() =>
        Prop.ForAll(Targets(Layers.Lowest, Layers.Highest).ToArbitrary(), target =>
        {
            var (boxes, layers, index) = target;
            var alone = boxes.Select((box, i) => i == index ? box : new Rect(box.X + 1000, box.Y, box.Width, box.Height))
                .ToArray();

            return Layers.Behind(alone, layers, index) == 0 && Layers.InFrontOf(alone, layers, index) == 0
                   && Layers.BringForward(alone, layers, index) == layers[index]
                   && Layers.SendBackward(alone, layers, index) == layers[index];
        });

    /// <summary>Front to back holds every widget once, each one in front of every widget listed after it.</summary>
    [Property(MaxTest = 200)]
    public Property Front_to_back_lists_every_widget_once_in_drawing_order() =>
        Prop.ForAll(Screens().ToArbitrary(), screen =>
        {
            var order = Layers.FrontToBack(screen.Layers);
            var once = order.Order().SequenceEqual(Enumerable.Range(0, screen.Layers.Length));
            var drawn = Enumerable.Range(0, order.Count).All(a =>
                Enumerable.Range(a + 1, order.Count - a - 1).All(b => Layers.InFront(screen.Layers, order[a], order[b])));
            return once && drawn;
        });

    /// <summary>Of two widgets that overlap, exactly one is behind the other — so counting, for every widget, those
    /// covering it counts each overlapping pair once. Boxes that only touch are not a pair.</summary>
    [Property(MaxTest = 200)]
    public Property Every_overlap_puts_exactly_one_widget_behind() =>
        Prop.ForAll(Screens().ToArbitrary(), screen =>
        {
            var (boxes, layers) = screen;
            var pairs = 0;
            for (var a = 0; a < boxes.Length; a++)
                for (var b = a + 1; b < boxes.Length; b++)
                {
                    var inside = Rect.Intersect(boxes[a], boxes[b]);
                    if (!inside.IsEmpty && inside.Width > 0 && inside.Height > 0) pairs++;
                }

            var behind = Enumerable.Range(0, boxes.Length).Sum(i => Layers.Behind(boxes, layers, i));
            return behind == pairs;
        });
}
