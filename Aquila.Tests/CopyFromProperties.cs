using Aquila.Models;
using Aquila.Services;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;

namespace Aquila.Tests;

/// <summary>
/// A widget put back by the editor's undo (<see cref="DesktopWidgetDefinition.CopyFrom"/>): it takes every value of
/// the state kept, into the same object, and shares nothing with it afterwards — a field it missed would be a change
/// undo cannot take back, and a shared list one that a later edit would make to the history too.
/// </summary>
public sealed class CopyFromProperties
{
    /// <summary>A widget with every property set from a seed — by reflection, so a property added later is drawn
    /// here too without anyone remembering to add it.</summary>
    private static DesktopWidgetDefinition Filled(int seed)
    {
        var random = new Random(seed);
        var widget = new DesktopWidgetDefinition();
        foreach (var property in typeof(DesktopWidgetDefinition).GetProperties().Where(p => p.CanWrite))
        {
            var type = property.PropertyType;
            object? value =
                type == typeof(string) ? $"s{random.Next(1000)}"
                : type == typeof(int) ? random.Next(-5, 600)
                : type == typeof(double) ? Math.Round(random.NextDouble() * 900, 2)
                : type == typeof(bool) ? random.Next(2) == 1
                : type.IsEnum ? Enum.GetValues(type).GetValue(random.Next(Enum.GetValues(type).Length))
                : null;
            if (value is not null) property.SetValue(widget, value);
        }
        widget.Series = [.. Enumerable.Range(0, random.Next(4)).Select(i => new WidgetSeries
        {
            SensorIdentifier = $"/sensor/{random.Next(50)}", Ramp = $"ramp{i}", Metric = $"m{random.Next(9)}",
        })];
        return widget;
    }

    /// <summary>Copied into any other widget, a widget's values come over whole — written out, the two are the same.
    /// </summary>
    [Property(MaxTest = 200)]
    public Property Every_value_is_taken() =>
        Prop.ForAll(Arb.From(Gen.Choose(0, int.MaxValue - 1).Two()), seeds =>
        {
            var (kept, live) = (Filled(seeds.Item1), Filled(seeds.Item2));
            live.CopyFrom(kept);
            return DesktopLayoutService.Serialize([live]) == DesktopLayoutService.Serialize([kept]);
        });

    /// <summary>After the copy, changing the widget's readings leaves the state it was copied from as it was.</summary>
    [Property(MaxTest = 200)]
    public Property Nothing_is_shared_afterwards() =>
        Prop.ForAll(Arb.From(Gen.Choose(0, int.MaxValue - 1)), seed =>
        {
            var kept = Filled(seed);
            var before = DesktopLayoutService.Serialize([kept]);
            var live = new DesktopWidgetDefinition();
            live.CopyFrom(kept);

            live.Series.Add(new WidgetSeries { SensorIdentifier = "/added" });
            foreach (var series in live.Series) series.Ramp = "changed";

            return DesktopLayoutService.Serialize([kept]) == before;
        });
}
