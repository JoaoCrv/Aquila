using Aquila.Controls;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;

namespace Aquila.Tests;

/// <summary>
/// A chart's legend folds into lines when its readings do not fit across, and never takes more than the room it is
/// given (<see cref="LegendPanel.Columns"/>): what it chooses must read every name whole whenever any layout within the
/// room could, and stay within the room whenever any layout could.
/// </summary>
public sealed class LegendProperties
{
    private const double Gap = 10, Line = 15, LineGap = 2;

    private static Gen<(double[] Widths, double Width, double Room)> Legends() =>
        from count in Gen.Choose(1, 4)
        from widths in Gen.Choose(20, 200).Select(w => (double)w).ArrayOf(count)
        from width in Gen.Choose(40, 400)
        from room in Gen.Choose(10, 120)
        select (widths, (double)width, (double)room);

    private static double Column(double width, int across) => (width - (across - 1) * Gap) / across;

    private static bool Whole(double[] widths, double width, int across) => Column(width, across) >= widths.Max() - 0.01;

    private static bool Fits(int count, int across, double room) =>
        LegendPanel.HeightOf(count, across, Line, LineGap) <= room + 0.01;

    private static Property ForAll(Func<double[], double, double, int, bool> rule) =>
        Prop.ForAll(Legends().ToArbitrary(), legend =>
            rule(legend.Widths, legend.Width, legend.Room,
                LegendPanel.Columns(legend.Widths, legend.Width, Gap, Line, LineGap, legend.Room)));

    [Property(MaxTest = 500)]
    public Property Between_one_column_and_one_per_reading() =>
        ForAll((widths, _, _, across) => across >= 1 && across <= widths.Length);

    /// <summary>Side by side whenever they all fit side by side.</summary>
    [Property(MaxTest = 500)]
    public Property One_line_when_every_name_fits_across() =>
        ForAll((widths, width, _, across) => !Whole(widths, width, widths.Length) || across == widths.Length);

    /// <summary>Every name whole, whenever some layout within the room has every name whole.</summary>
    [Property(MaxTest = 500)]
    public Property Names_are_cut_only_when_the_room_leaves_no_choice() =>
        ForAll((widths, width, room, across) =>
            Whole(widths, width, across)
            || !Enumerable.Range(1, widths.Length).Any(c => Whole(widths, width, c) && Fits(widths.Length, c, room)));

    /// <summary>Within the room, whenever any layout is — and one line is the least any legend takes.</summary>
    [Property(MaxTest = 500)]
    public Property Never_taller_than_the_room_unless_one_line_is() =>
        ForAll((widths, _, room, across) => Fits(widths.Length, across, room) || across == widths.Length);

    /// <summary>The lines are even: as few columns as the number of lines needs, so four readings are two and two.
    /// </summary>
    [Property(MaxTest = 500)]
    public Property The_lines_are_even() =>
        ForAll((widths, _, _, across) =>
        {
            var lines = LegendPanel.Lines(widths.Length, across);
            return across == (widths.Length + lines - 1) / lines;
        });
}
