using System.Globalization;
using System.Text.RegularExpressions;
using Aquila.Models;

namespace Aquila.Controls;

/// <summary>
/// What a clock shows at a given instant — where each ring stands and what each line says — worked out from the
/// instant alone.
///
/// Pure, and apart from the piece, for the same reason <see cref="SensorFormat"/> is: the arithmetic is where a
/// clock goes wrong quietly. A ring that ran backwards once a minute, or a second written twice, looks like a
/// rendering glitch from the desktop and is a sum here, which Aquila.Tests can hold to its rule for any instant.
/// </summary>
public static partial class ClockFace
{
    /// <summary>Where each ring stands, as a share of its turn: 0 at twelve o'clock, rising clockwise, and never
    /// reaching 1 — a full turn is the next turn's 0.</summary>
    public readonly record struct Hands(double Seconds, double Minutes, double Hours);

    /// <summary>
    /// The three rings at an instant. Each carries the hand inside it, so the minutes creep through the minute
    /// rather than clicking over at its end — the way a wall clock's minute hand does.
    ///
    /// The hours stop at the minute: a twelve-hour ring moves a twelfth of a degree a second, which nobody can see,
    /// and a value that changes without anything visible changing is a redraw for nothing.
    ///
    /// The seconds stand on the whole second — the one written under the time. They carried the milliseconds once,
    /// and the only milliseconds a clock woken on the second ever has are the few it is woken late by: rendered,
    /// that put a hairline at twelve at the top of every minute, where the ring should be empty.
    /// </summary>
    public static Hands Fractions(DateTime time) => new(
        time.Second / 60d,
        (time.Minute + time.Second / 60d) / 60,
        (time.Hour % 12 + time.Minute / 60d) / 12);

    /// <summary>How many rings a setting draws.</summary>
    public static int RingCount(ClockRings rings) => rings switch
    {
        ClockRings.Seconds => 1,
        ClockRings.MinutesSeconds => 2,
        ClockRings.All => 3,
        _ => 0,
    };

    /// <summary>
    /// A clock without rings, written out.
    ///
    /// Standard specifiers, never a hand-written pattern: the result then follows the machine's own locale —
    /// 24-hour or 12-hour, day-month or month-day, in the user's language — and there is nothing for anyone to
    /// get wrong.
    /// </summary>
    public static string Text(DateTime time, ClockFormat format) => time.ToString(format switch
    {
        ClockFormat.TimeWithSeconds => "T",
        ClockFormat.Date => "d",
        ClockFormat.DateLong => "D",
        ClockFormat.DateAndTime => "g",
        _ => "t",
    });

    /// <summary>The time in the middle of the rings: the machine's short time, 14:07 or 2:07 PM.</summary>
    public static string Time(DateTime time) => time.ToString("t");

    /// <summary>The seconds under it, always two digits, so the line does not change width at :09 → :10.</summary>
    public static string Seconds(DateTime time) => time.ToString("ss");

    /// <summary>
    /// The date above the time, short: the day's name and the day and month — "terça, 20/10" on a Portuguese
    /// machine, "Tue, 10/20" on an American one.
    ///
    /// Built from the culture's own parts, never from a pattern of ours: its abbreviated day name, and its short
    /// date with the year taken out, which keeps its order and its separator. No standard specifier says this —
    /// "M" writes the month out in full, which is too long for the middle of a ring.
    /// </summary>
    public static string ShortDate(DateTime time, CultureInfo? culture = null)
    {
        culture ??= CultureInfo.CurrentCulture;
        var format = culture.DateTimeFormat;

        var dayMonth = YearAndItsSeparator().Replace(format.ShortDatePattern, string.Empty).Trim();

        // A pattern with nothing left but the year has no day and month to offer; the culture's month-day
        // pattern still does. A single letter left over would be read as a standard specifier — "d" is the whole
        // short date — so it is marked as a custom one.
        if (dayMonth.Length == 0) dayMonth = format.MonthDayPattern;
        else if (dayMonth.Length == 1) dayMonth = "%" + dayMonth;

        return $"{format.GetAbbreviatedDayName(time.DayOfWeek)}, {time.ToString(dayMonth, culture)}";
    }

    /// <summary>The year, with whatever separates it from the day and the month: "/yyyy", "yyyy-", ". yyyy 'г.'".
    /// </summary>
    [GeneratedRegex(@"[^dM]*y+[^dM]*")]
    private static partial Regex YearAndItsSeparator();

    /// <summary>
    /// How far past a whole second the clock is woken.
    ///
    /// Aimed AT the second, a timer that fires a few milliseconds early reads the old second again and then has
    /// to skip one to catch up. A Windows timer is only good to about fifteen milliseconds, so the aim is a little
    /// past it — late enough to be safely inside the new second, early enough that nobody sees the delay.
    /// </summary>
    public static readonly TimeSpan Lateness = TimeSpan.FromMilliseconds(25);

    /// <summary>The whole second after <paramref name="now"/> — when the clock is next woken, and where a ring moving
    /// smoothly is aimed, so that it is exactly there when the second arrives.</summary>
    public static DateTime NextSecond(DateTime now) =>
        new(now.Ticks - now.Ticks % TimeSpan.TicksPerSecond + TimeSpan.TicksPerSecond, now.Kind);

    /// <summary>How long to wait from <paramref name="now"/> to land just after the next whole second.</summary>
    public static TimeSpan UntilNextSecond(DateTime now) => NextSecond(now) - now + Lateness;

    /// <summary>Whether two instants fall in the same whole second — so whether the second has been shown.</summary>
    public static bool SameSecond(DateTime a, DateTime b) =>
        a.Ticks / TimeSpan.TicksPerSecond == b.Ticks / TimeSpan.TicksPerSecond;
}
