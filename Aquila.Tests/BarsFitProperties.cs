using Aquila.Controls;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Xunit;

namespace Aquila.Tests;

/// <summary>
/// What standing bars write (<see cref="BarsTile.Fit"/>), decided for every column at once: sixteen cores in a narrow
/// widget must never leave numbers or names run into one another, nor some columns written and their neighbours bare.
/// </summary>
public sealed class BarsFitProperties
{
    /// <summary>Widths as measured, and a column as wide as a widget can make it — none, or not decided yet.</summary>
    private static Gen<double> Widths() =>
        Gen.OneOf(Gen.Choose(0, 2000).Select(v => v / 10d), Gen.Elements(0d, double.PositiveInfinity, double.NaN));

    /// <summary>
    /// A number is written only where the widest fits: with its unit if that fits, without it if only that does, and not
    /// at all otherwise. The unit is dropped only when it is what does not fit. Names likewise, all or none. A column not
    /// measured yet — NaN, or unbounded — holds everything.
    /// </summary>
    [Property(MaxTest = 1000)]
    public Property Numbers_and_names_are_written_only_where_they_fit() =>
        Prop.ForAll(
            (from column in Widths()
             from bare in Gen.Choose(0, 1000).Select(v => v / 10d)
             from unit in Gen.Choose(0, 400).Select(v => v / 10d)
             from name in Gen.Choose(0, 2000).Select(v => v / 10d)
             select (column, bare, unit, name)).ToArbitrary(),
            text =>
            {
                var (column, bare, unit, name) = text;
                var withUnit = bare + unit;
                var (numbers, unitShown, names) = BarsTile.Fit(column, withUnit, bare, name);
                var room = double.IsNaN(column) ? double.PositiveInfinity : column;

                var numbersRight = unitShown ? withUnit <= room
                    : numbers ? withUnit > room && bare <= room
                    : bare > room;

                return numbersRight && (!unitShown || numbers) && names == name <= room;
            });

    [Fact]
    public void Sixteen_cores_in_a_narrow_widget_write_nothing_and_four_write_everything()
    {
        Assert.Equal((false, false, false), BarsTile.Fit(column: 12, withUnit: 34, withoutUnit: 20, name: 30));
        Assert.Equal((true, false, true), BarsTile.Fit(column: 24, withUnit: 34, withoutUnit: 20, name: 22));
        Assert.Equal((true, true, true), BarsTile.Fit(column: 60, withUnit: 34, withoutUnit: 20, name: 50));
    }
}
