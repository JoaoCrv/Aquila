using System.Windows;
using Aquila.DesktopSurface;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;

namespace Aquila.Tests;

/// <summary>
/// The geometry of editing a widget (<see cref="EditGeometry"/>): the wheel, the corner handles, the arrows and the
/// drag all ask it, so a widget that could be pushed off its screen, made smaller than the boxes allow, or lose its
/// shape while a handle was pulled would be wrong in four places at once.
/// </summary>
public sealed class EditGeometryProperties
{
    /// <summary>A screen, in DIPs: from a small laptop at 200% to a large monitor at 100%.</summary>
    private static Gen<Size> Screens() =>
        from width in Gen.Choose(640, 4000)
        from height in Gen.Choose(480, 2400)
        select new Size(width, height);

    /// <summary>A widget as editing leaves one: wholly on its screen, no side under the smallest.</summary>
    private static Gen<(Size Screen, Rect Widget)> Placed() =>
        from screen in Screens()
        from width in Gen.Choose((int)EditGeometry.MinSide, (int)screen.Width)
        from height in Gen.Choose((int)EditGeometry.MinSide, (int)screen.Height)
        from x in Gen.Choose(0, (int)(screen.Width - width))
        from y in Gen.Choose(0, (int)(screen.Height - height))
        select (screen, new Rect(x, y, width, height));

    /// <summary>What a hand on a wheel, a box or a file can ask for, nonsense included.</summary>
    private static Gen<double> AnySides() =>
        Gen.OneOf(
            Gen.Choose(-10_000, 10_000).Select(v => v / 2d),
            Gen.Elements(0d, EditGeometry.MinSide, -1d, 1e12, double.NaN, double.PositiveInfinity, double.NegativeInfinity));

    private static Gen<Vector> Moves() =>
        from dx in Gen.Choose(-6000, 6000)
        from dy in Gen.Choose(-6000, 6000)
        select new Vector(dx / 2d, dy / 2d);

    private static Gen<Corner> Corners() => Gen.Elements(Enum.GetValues<Corner>());

    private static bool Inside(Rect widget, Size screen) =>
        widget.Left >= -1e-9 && widget.Top >= -1e-9
        && widget.Right <= screen.Width + 1e-9 && widget.Bottom <= screen.Height + 1e-9;

    /// <summary>Whatever is asked for, a size comes back between the smallest and the screen.</summary>
    [Property(MaxTest = 500)]
    public Property A_size_is_always_between_the_smallest_and_the_screen() =>
        Prop.ForAll(
            (from screen in Screens() from width in AnySides() from height in AnySides() select (screen, width, height))
            .ToArbitrary(),
            asked =>
            {
                var size = EditGeometry.ClampSize(asked.width, asked.height, asked.screen);
                return size.Width >= EditGeometry.MinSide && size.Width <= asked.screen.Width
                    && size.Height >= EditGeometry.MinSide && size.Height <= asked.screen.Height;
            });

    /// <summary>Moved by any amount, a widget that fits its screen stays wholly on it.</summary>
    [Property(MaxTest = 500)]
    public Property A_nudge_keeps_the_widget_on_its_screen() =>
        Prop.ForAll(
            (from placed in Placed() from move in Moves() select (placed.Screen, placed.Widget, move)).ToArbitrary(),
            moved =>
            {
                var at = EditGeometry.Nudge(moved.Widget, moved.move, moved.Screen);
                return Inside(new Rect(at, moved.Widget.Size), moved.Screen);
            });

    /// <summary>Pulled by any corner, in any direction, a widget stays on its screen, no side goes under the
    /// smallest, and the opposite corner does not move.</summary>
    [Property(MaxTest = 1000)]
    public Property A_corner_drag_keeps_the_opposite_corner_and_the_screen() =>
        Prop.ForAll(
            (from placed in Placed() from corner in Corners() from move in Moves() from keep in Gen.Elements(false, true)
             select (placed.Screen, placed.Widget, corner, move, keep)).ToArbitrary(),
            drag =>
            {
                var after = EditGeometry.Resize(drag.Widget, drag.corner, drag.move, drag.keep, drag.Screen);
                var (startX, afterX) = drag.corner is Corner.TopLeft or Corner.BottomLeft
                    ? (drag.Widget.Right, after.Right)
                    : (drag.Widget.Left, after.Left);
                var (startY, afterY) = drag.corner is Corner.TopLeft or Corner.TopRight
                    ? (drag.Widget.Bottom, after.Bottom)
                    : (drag.Widget.Top, after.Top);

                return Inside(after, drag.Screen)
                    && after.Width >= EditGeometry.MinSide - 1e-6 && after.Height >= EditGeometry.MinSide - 1e-6
                    && Math.Abs(afterX - startX) < 1e-6 && Math.Abs(afterY - startY) < 1e-6;
            });

    /// <summary>Turned by the wheel any amount, a widget keeps the ratio of its sides — whatever shape it had, its
    /// kind's or one chosen with a handle — stays between the smallest and the screen, and grows or shrinks by one
    /// factor, never one side alone.</summary>
    [Property(MaxTest = 1000)]
    public Property The_wheel_keeps_the_shape_a_widget_has() =>
        Prop.ForAll(
            (from placed in Placed() from factor in Gen.Choose(1, 4000).Select(f => f / 100d)
             select (placed.Screen, placed.Widget, factor)).ToArbitrary(),
            turned =>
            {
                var size = EditGeometry.Scale(turned.Widget.Size, turned.factor, turned.Screen);
                var ratio = turned.Widget.Width / turned.Widget.Height;
                return Math.Abs(size.Width - size.Height * ratio) <= 1e-6 * size.Width
                    && size.Width >= EditGeometry.MinSide - 1e-6 && size.Height >= EditGeometry.MinSide - 1e-6
                    && size.Width <= turned.Screen.Width + 1e-6 && size.Height <= turned.Screen.Height + 1e-6;
            });

    /// <summary>Pulled with its shape kept, a widget's sides keep their ratio — within a DIP, the most a person
    /// could see — however far the pointer goes, edges included.</summary>
    [Property(MaxTest = 1000)]
    public Property A_shape_keeping_drag_keeps_the_ratio() =>
        Prop.ForAll(
            (from placed in Placed() from corner in Corners() from move in Moves()
             select (placed.Screen, placed.Widget, corner, move)).ToArbitrary(),
            drag =>
            {
                var after = EditGeometry.Resize(drag.Widget, drag.corner, drag.move, keepShape: true, drag.Screen);
                var ratio = drag.Widget.Width / drag.Widget.Height;
                return Math.Abs(after.Width - after.Height * ratio) <= 1;
            });
}
