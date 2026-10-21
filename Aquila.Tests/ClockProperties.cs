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
