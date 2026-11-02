using System.Windows;
using Aquila.Services.EditMode;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;

namespace Aquila.Tests;

/// <summary>
/// Where the editor panel stands (<see cref="PanelPlacement"/>): on the primary screen, out of the widget's way where
/// it can be, and where it was left when it was dragged.
/// </summary>
public sealed class PanelPlacementProperties
{
    private const double Width = 460;

    /// <summary>A working area wide enough for the panel, anywhere on the desktop — a taskbar may take any edge.
    /// </summary>
    private static Gen<Rect> Areas() =>
        from left in Gen.Choose(-3000, 3000)
        from top in Gen.Choose(-200, 200)
        from width in Gen.Choose(1000, 4000)
        from height in Gen.Choose(600, 2400)
        select new Rect(left, top, width, height);

    /// <summary>A widget on that area, or none.</summary>
    private static Gen<Rect?> Widgets(Rect area) =>
        Gen.OneOf(
            Gen.Constant<Rect?>(null),
            from x in Gen.Choose(0, (int)area.Width - 40)
            from y in Gen.Choose(0, (int)area.Height - 40)
            from width in Gen.Choose(40, 600)
            from height in Gen.Choose(40, 600)
            select (Rect?)new Rect(area.Left + x, area.Top + y, Math.Min(width, area.Width - x), Math.Min(height, area.Height - y)));

    private static bool Inside(Rect inner, Rect outer) =>
        inner.Left >= outer.Left - 1e-9 && inner.Top >= outer.Top - 1e-9
        && inner.Right <= outer.Right + 1e-9 && inner.Bottom <= outer.Bottom + 1e-9;

    private static bool Overlap(Rect a, Rect b) =>
        a.Left < b.Right && b.Left < a.Right && a.Top < b.Bottom && b.Top < a.Bottom;

    /// <summary>Docked, the panel fits the primary screen's working area, at the inset from its top, and on the right
    /// only when the left would cover the widget and the right would not.</summary>
    [Property(MaxTest = 300)]
    public Property A_docked_panel_fits_and_steps_out_of_the_widgets_way() =>
        Prop.ForAll(
            (from area in Areas() from widget in Widgets(area) select (area, widget)).ToArbitrary(),
            docked =>
            {
                var (area, widget) = docked;
                var panel = PanelPlacement.Choose(area, widget, null, Width);

                var leftSide = new Rect(area.Left + PanelPlacement.Inset, panel.Top, Width, panel.Height);
                var rightSide = new Rect(area.Right - PanelPlacement.Inset - Width, panel.Top, Width, panel.Height);
                var expected = widget is { } box && Overlap(leftSide, box) && !Overlap(rightSide, box)
                    ? rightSide.Left
                    : leftSide.Left;

                return Inside(panel, area)
                       && Math.Abs(panel.Top - (area.Top + PanelPlacement.Inset)) < 1e-9
                       && Math.Abs(panel.Left - expected) < 1e-9;
            });

    /// <summary>Dragged to a place that fits its screen, the panel is put back exactly there; dragged half off it, it
    /// is brought back onto it, never further.</summary>
    [Property(MaxTest = 300)]
    public Property A_dragged_panel_stays_where_it_was_left() =>
        Prop.ForAll(
            (from area in Areas()
             from x in Gen.Choose(-600, (int)area.Width + 600)
             from y in Gen.Choose(-300, (int)area.Height + 300)
             select (area, at: new Point(area.Left + x, area.Top + y))).ToArbitrary(),
            dragged =>
            {
                var (area, at) = dragged;
                var panel = PanelPlacement.Choose(new Rect(0, 0, 1920, 1080), null, (at, area), Width);

                var fits = at.X >= area.Left && at.X <= area.Right - Width
                           && at.Y >= area.Top && at.Y <= area.Bottom - panel.Height;
                return Inside(panel, area) && (!fits || panel.TopLeft == at);
            });
}
