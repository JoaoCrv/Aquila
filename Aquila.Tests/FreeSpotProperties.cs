using System.Windows;
using Aquila.Services.EditMode;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;

namespace Aquila.Tests;

/// <summary>
/// Where a new widget goes (<see cref="FreeSpot"/>): on the screen, and clear of everything already there when there
/// is room — a widget born under the editor panel, as they were, is a widget that seems not to have appeared.
/// </summary>
public sealed class FreeSpotProperties
{
    private static Gen<(Rect Area, Rect[] Taken, Size Size)> Screens() =>
        from width in Gen.Choose(640, 2560)
        from height in Gen.Choose(480, 1440)
        from count in Gen.Choose(0, 14)
        from taken in (from x in Gen.Choose(0, width)
                       from y in Gen.Choose(0, height)
                       from w in Gen.Choose(20, 600)
                       from h in Gen.Choose(20, 500)
                       select new Rect(x, y, w, h)).ArrayOf(count)
        from size in (from w in Gen.Choose(40, 500) from h in Gen.Choose(40, 400) select new Size(w, h))
        select (new Rect(0, 0, width, height), taken, size);

    private static bool Inside(Rect spot, Rect area) =>
        spot.Left >= area.Left - 1e-9 && spot.Top >= area.Top - 1e-9
        && spot.Right <= area.Right + 1e-9 && spot.Bottom <= area.Bottom + 1e-9;

    private static bool Clear(Rect spot, IEnumerable<Rect> taken) =>
        taken.All(t => spot.Right + FreeSpot.Slack <= t.Left || t.Right + FreeSpot.Slack <= spot.Left
                       || spot.Bottom + FreeSpot.Slack <= t.Top || t.Bottom + FreeSpot.Slack <= spot.Top);

    /// <summary>A widget that fits the screen always lands on it; and when any place on the search's grid is free,
    /// where it lands is clear of everything taken.</summary>
    [Property(MaxTest = 300)]
    public Property A_new_widget_lands_on_the_screen_and_in_a_free_place_when_there_is_one() =>
        Prop.ForAll(Screens().ToArbitrary(), screen =>
        {
            var (area, taken, size) = screen;
            var at = FreeSpot.Find(area, taken, size, taken.LastOrDefault());
            var spot = new Rect(at, size);

            var anyFree = false;
            for (var y = area.Top + FreeSpot.Margin; !anyFree && y + size.Height <= area.Bottom - FreeSpot.Margin; y += FreeSpot.Step)
                for (var x = area.Left + FreeSpot.Margin; !anyFree && x + size.Width <= area.Right - FreeSpot.Margin; x += FreeSpot.Step)
                    anyFree = Clear(new Rect(x, y, size.Width, size.Height), taken);

            return Inside(spot, area) && (!anyFree || Clear(spot, taken));
        });

    /// <summary>On an empty screen a widget starts at its top-left corner, in from the edges by the margin.</summary>
    [Property(MaxTest = 100)]
    public Property On_an_empty_screen_it_starts_at_the_corner() =>
        Prop.ForAll(Screens().ToArbitrary(), screen =>
        {
            var at = FreeSpot.Find(screen.Area, [], screen.Size);
            return at == new Point(screen.Area.Left + FreeSpot.Margin, screen.Area.Top + FreeSpot.Margin);
        });
}
