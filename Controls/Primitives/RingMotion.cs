namespace Aquila.Controls.Primitives;

/// <summary>
/// How a ring gets from where it is to where it is asked to be — planned here, as plain numbers, and carried out by
/// <see cref="RingArc.MoveTo"/>.
///
/// Apart from the control for the reason <see cref="ArcGeometry"/> is: the plan is where a ring goes wrong quietly.
/// One that unwound a whole turn backwards at the top of every minute, or arrived a frame late at each second, looks
/// like a drawing glitch on the desktop and is a sum here.
/// </summary>
public static class RingMotion
{
    /// <summary>One stretch of a move: the arc is at <see cref="From"/>, and travels steadily to <see cref="To"/>
    /// over <see cref="Duration"/>. A stretch that starts somewhere other than where the last one ended starts with a
    /// jump — the reset to nothing at the top of a turn.</summary>
    public readonly record struct Segment(double From, double To, TimeSpan Duration);

    /// <summary>A whole sweep — the end of a turn, and the next turn's nothing.</summary>
    public const double Full = 1;

    /// <summary>
    /// How far behind a target may be and still count as the same place.
    ///
    /// A forward-only ring treats a target behind it as the next turn's. Two sums that should agree can differ in the
    /// last bit, and without this a ring at 0.30000000000000004 asked for 0.3 would go all the way round. A
    /// thousand-millionth of a turn is far below anything a ring can show, and far below the smallest real step — a
    /// twelve-hour ring moves a forty-thousandth of a turn in a second.
    /// </summary>
    public const double Tolerance = 1e-9;

    /// <summary>
    /// The way from <paramref name="current"/> to <paramref name="target"/> in <paramref name="duration"/>.
    ///
    /// Without <paramref name="forwardOnly"/> it is one stretch, whichever way it points: a dial goes down as
    /// readily as up.
    ///
    /// With it — a clock, where time does not run backwards — a target behind the ring is reached by going on: up to
    /// the full sweep, back to nothing at once, and on to the target. The time is shared in proportion to the
    /// distance, so the ring moves at one speed all the way and is where it should be at every moment of the move,
    /// the wrap included. Both stretches are there even when one is empty, so a wrap always reads the same.
    ///
    /// A duration below zero is taken as none: the ring is late already and goes straight there.
    /// </summary>
    public static Segment[] Plan(double current, double target, TimeSpan duration, bool forwardOnly)
    {
        if (duration < TimeSpan.Zero) duration = TimeSpan.Zero;

        if (!forwardOnly || !Behind(current, target)) return [new(current, target, duration)];

        var up = Math.Max(0, Full - current);
        var on = Math.Max(0, target);

        // Rounded to the tick and the second stretch given the remainder, so the two add up to exactly the duration
        // and the ring arrives when it was told to, not a rounding later.
        var first = up + on > 0
            ? TimeSpan.FromTicks((long)Math.Round(duration.Ticks * up / (up + on)))
            : TimeSpan.Zero;

        return [new(current, Full, first), new(0, target, duration - first)];
    }

    /// <summary>
    /// Where a ring moving from <paramref name="from"/> to <paramref name="to"/> stands after <paramref name="share"/>
    /// of the move — by the same rule as <see cref="Plan"/>, so a ring put there and then moved on carries on as if it
    /// had been moving all along. This is how something shown part-way through a move starts where it should, rather
    /// than standing still or catching up.
    /// </summary>
    public static double At(double from, double to, double share, bool forwardOnly)
    {
        share = double.IsNaN(share) ? 0 : Math.Clamp(share, 0, 1);

        if (!forwardOnly || !Behind(from, to)) return from + (to - from) * share;

        var at = from + (to + Full - from) * share;
        return at > Full ? at - Full : at;
    }

    private static bool Behind(double current, double target) => target < current - Tolerance;
}
