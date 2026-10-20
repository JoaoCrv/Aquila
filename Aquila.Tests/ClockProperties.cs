using System.Globalization;
using Aquila.Controls;
using Aquila.Models;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Xunit;

namespace Aquila.Tests;

/// <summary>
/// The clock's arithmetic (<see cref="ClockFace"/>). A ring that ran backwards once a minute, or a second shown
/// twice, looks like a drawing glitch from the desktop and is a sum here — so each rule is held for any instant.
/// </summary>
public sealed class ClockProperties
{
    /// <summary>Any instant from the year 1 to 9999, to the millisecond.</summary>
    private static Arbitrary<DateTime> Instants() =>
        (from days in Gen.Choose(0, 3_652_000)
         from milliseconds in Gen.Choose(0, 86_399_999)
         select DateTime.MinValue.AddDays(days).AddMilliseconds(milliseconds))
        .ToArbitrary();

    /// <summary>Instants crowded round the moments a ring wraps — the last second of a minute, of an hour, of
    /// twelve hours — which an even spread would almost never land on.</summary>
    private static Arbitrary<DateTime> NearTheTop() =>
        (from days in Gen.Choose(0, 3_652_000)
         from hour in Gen.OneOf(Gen.Choose(0, 23), Gen.Elements(0, 11, 12, 23))
         from minute in Gen.OneOf(Gen.Choose(0, 59), Gen.Elements(0, 59))
         from second in Gen.OneOf(Gen.Choose(0, 59), Gen.Elements(0, 58, 59))
         from milliseconds in Gen.Choose(0, 999)
         select DateTime.MinValue.AddDays(days).Add(new TimeSpan(0, hour, minute, second, milliseconds)))
        .ToArbitrary();

    /// <summary>Every hand is somewhere on its turn: from twelve o'clock, and never a whole turn — a full turn is
    /// the next one's start, and a ring drawn full and then empty would be the backward sweep under another name.
    /// </summary>
    [Property(MaxTest = 1000)]
    public Property Every_hand_stays_within_its_turn() =>
        Prop.ForAll(Instants(), time =>
        {
            var (seconds, minutes, hours) = ClockFace.Fractions(time);
            return seconds is >= 0 and < 1 && minutes is >= 0 and < 1 && hours is >= 0 and < 1;
        });

    /// <summary>Within a minute the seconds ring only ever moves forward, a step every second.</summary>
    [Property(MaxTest = 300)]
    public Property Within_a_minute_the_seconds_rise_with_every_second() =>
        Prop.ForAll(Instants(), time =>
        {
            var minute = time.AddTicks(-(time.Ticks % TimeSpan.TicksPerMinute)).AddMilliseconds(time.Millisecond);

            return Enumerable.Range(0, 59).All(second =>
                ClockFace.Fractions(minute.AddSeconds(second + 1)).Seconds
                > ClockFace.Fractions(minute.AddSeconds(second)).Seconds);
        });

    /// <summary>
    /// From one second to the next, a ring is reported as wrapping exactly when its turn ends — the seconds at the
    /// minute, the minutes at the hour, the hours at twelve and at midnight — and never otherwise. A wrap missed is a
    /// ring unwinding a whole circle backwards; one reported wrongly is a ring that jumps when it should sweep.
    /// </summary>
    [Property(MaxTest = 2000)]
    public Property A_ring_wraps_exactly_when_its_turn_ends() =>
        Prop.ForAll(NearTheTop(), before =>
        {
            var after = before.AddSeconds(1);
            var minute = after.Second == 0;
            var hour = minute && after.Minute == 0;
            var twelve = hour && after.Hour % 12 == 0;

            var (seconds, minutes, hours) = ClockFace.Wrapped(before, after);
            return seconds == minute && minutes == hour && hours == twelve;
        });

    /// <summary>The tops of the turns, as examples — the property above may not draw each of them.</summary>
    [Theory]
    [InlineData("10:30:15", false, false, false)]
    [InlineData("10:30:59", true, false, false)]
    [InlineData("10:59:59", true, true, false)]
    [InlineData("11:59:59", true, true, true)]
    [InlineData("12:59:59", true, true, false)]
    [InlineData("23:59:59", true, true, true)]
    public void The_tops_of_the_turns(string time, bool seconds, bool minutes, bool hours)
    {
        var before = DateTime.Parse("2026-10-20T" + time, CultureInfo.InvariantCulture);
        Assert.Equal((seconds, minutes, hours), ClockFace.Wrapped(before, before.AddSeconds(1)));
    }

    /// <summary>
    /// A clock without rings writes exactly what the clock wrote before rings existed, format by format — the
    /// specifiers below are the old piece's own, pinned, so a clock in an older layout cannot change by a
    /// character. (Rendered side by side with the old piece once, the two were identical to the pixel.)
    /// </summary>
    [Theory]
    [InlineData(ClockFormat.Time, "t")]
    [InlineData(ClockFormat.TimeWithSeconds, "T")]
    [InlineData(ClockFormat.Date, "d")]
    [InlineData(ClockFormat.DateLong, "D")]
    [InlineData(ClockFormat.DateAndTime, "g")]
    public void Without_rings_a_clock_writes_what_it_always_wrote(ClockFormat format, string specifier)
    {
        var time = new DateTime(2026, 10, 20, 14, 7, 9);
        Assert.Equal(time.ToString(specifier), ClockFace.Text(time, format));
    }

    /// <summary>
    /// In every culture this machine knows, the short date over the rings is that culture's own: it starts with its
    /// abbreviated day name, writes the day of the month, and never the year — and never throws, which a year taken
    /// out of a pattern carelessly could, by leaving a quote unclosed.
    /// </summary>
    [Fact]
    public void The_short_date_is_every_cultures_own_and_has_no_year()
    {
        var time = new DateTime(2026, 10, 27, 14, 7, 9);
        var wrong = new List<string>();

        foreach (var culture in CultureInfo.GetCultures(CultureTypes.SpecificCultures))
        {
            try
            {
                var date = ClockFace.ShortDate(time, culture);
                var format = culture.DateTimeFormat;

                if (!date.StartsWith(format.GetAbbreviatedDayName(time.DayOfWeek), StringComparison.Ordinal)
                    || date.Contains(time.ToString("yyyy", culture), StringComparison.Ordinal)
                    || !date.Contains(time.ToString("%d", culture), StringComparison.Ordinal))
                    wrong.Add($"{culture.Name}: {date}");
            }
            catch (FormatException e)
            {
                wrong.Add($"{culture.Name}: {e.Message}");
            }
        }

        Assert.Empty(wrong);
    }

    /// <summary>
    /// The rounded ends of an arc — the clock's rings and the dial alike — for any length, corner and radius: never
    /// below nothing, never rounder than asked, and never more than the arc can hold. "Hold" is LiveCharts' own
    /// geometry: the two ends take 2·atan(r / (inner + r)) from the arc's inner edge, and more than the arc has draws
    /// that edge backwards. A length of nothing has no ends at all.
    /// </summary>
    [Property(MaxTest = 1000)]
    public Property An_arcs_ends_are_never_rounder_than_it_can_hold() =>
        Prop.ForAll(
            (from fraction in Gen.Choose(-100, 100_000)
             from corner in Gen.Choose(-20, 400)
             from sweep in Gen.Elements(180d, 270d, 360d)
             from inner in Gen.Choose(-10, 3000)
             select (Fraction: fraction / 100_000d, Corner: corner / 10d, Sweep: sweep, Inner: inner / 10d))
            .ToArbitrary(),
            drawn =>
            {
                var (fraction, corner, sweep, inner) = drawn;
                var r = AquilaCharts.CornerFor(fraction, corner, sweep, inner);
                var arc = Math.Min(fraction, 1) * sweep * Math.PI / 180;

                return r >= 0
                    && r <= Math.Max(corner, 0)
                    && (r == 0 || 2 * Math.Atan(r / (inner + r)) <= arc)
                    && (fraction > 0 || r == 0);
            });

    /// <summary>The clock is woken just after the next whole second, by exactly the lateness it allows itself — never
    /// inside the second it has already shown, and never a whole second late.</summary>
    [Property(MaxTest = 1000)]
    public Property The_clock_wakes_just_after_the_next_second() =>
        Prop.ForAll(Instants(), now =>
        {
            var woken = now + ClockFace.UntilNextSecond(now);
            return woken.Ticks / TimeSpan.TicksPerSecond == now.Ticks / TimeSpan.TicksPerSecond + 1
                && woken.Ticks % TimeSpan.TicksPerSecond == ClockFace.Lateness.Ticks;
        });
}
