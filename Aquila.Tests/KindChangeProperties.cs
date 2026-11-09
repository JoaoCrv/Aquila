using Aquila.Services;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;

namespace Aquila.Tests;

/// <summary>
/// What a widget keeps when its kind changes (<see cref="WidgetCatalog.ReadingsKept"/>): the change cuts by it, and
/// the type gallery warns by it before the change, so a reading it got wrong would be lost without the warning that
/// should have named it.
/// </summary>
public sealed class KindChangeProperties
{
    /// <summary>A widget's readings: numbered so their order can be checked, each a sensor or a list.</summary>
    private static Gen<(int Id, bool IsList)[]> Readings() =>
        from count in Gen.Choose(0, 20)
        from lists in Gen.Elements(false, true).ArrayOf(count)
        select lists.Select((isList, id) => (id, isList)).ToArray();

    /// <summary>The kinds a widget can be changed TO — the listed ones; the old two-reading chart is never picked.</summary>
    private static Gen<WidgetKindInfo> Kinds() => Gen.Elements(WidgetCatalog.Listed.ToArray());

    /// <summary>Whatever the kind and the readings, what is kept is never more than the kind draws, all of the sort it
    /// draws, and in the order the widget had them.</summary>
    [Property(MaxTest = 300)]
    public Property What_is_kept_fits_the_kind_and_keeps_its_order() =>
        Prop.ForAll((from kind in Kinds() from readings in Readings() select (kind, readings)).ToArbitrary(), change =>
        {
            var kept = WidgetCatalog.ReadingsKept(change.kind, change.readings, r => r.IsList);
            var ordered = kept.Select(r => r.Id).SequenceEqual(kept.Select(r => r.Id).Order());
            var subset = kept.All(r => change.readings.Contains(r));
            return kept.Count <= change.kind.MaxSeries
                   && kept.All(r => r.IsList == change.kind.HasList)
                   && ordered && subset;
        });

    /// <summary>A kind that can draw everything a widget has keeps all of it.</summary>
    [Property(MaxTest = 300)]
    public Property A_kind_that_takes_everything_keeps_everything() =>
        Prop.ForAll((from kind in Kinds() from readings in Readings() select (kind, readings)).ToArbitrary(), change =>
        {
            var fitting = change.readings.Where(r => r.IsList == change.kind.HasList).Take(change.kind.MaxSeries).ToArray();
            return WidgetCatalog.ReadingsKept(change.kind, fitting, r => r.IsList).SequenceEqual(fitting);
        });
}
