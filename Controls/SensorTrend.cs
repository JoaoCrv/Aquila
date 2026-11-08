namespace Aquila.Controls;

/// <summary>Which way a reading is heading, as an arrow beside it shows it — or that it is not going anywhere.</summary>
public enum TrendArrow { None, Up, Down }

/// <summary>
/// Whether a reading is rising or falling — the arrow beside a Number. A rule of its own, beside
/// <see cref="SensorScale"/> and <see cref="SensorFormat"/>, because it is a judgement about readings that must hold
/// for any of them, and so is stated in <c>TrendProperties</c> rather than looked at on a desktop.
///
/// The last <see cref="Recent"/> against the <see cref="Before"/> that came before them, each by its mean. Means and
/// not two readings, because two readings are noise: a load idling between 5 and 40% would point up and down every
/// second. By time and not by count, so it reads the same at any poll interval, and a widget coming back on screen
/// does not compare its new readings with ones taken a minute ago.
///
/// The margin a difference must pass is the larger of two: a share of the reading's scale, so a CPU going from 45 to
/// 46 °C is not a trend; and the spread of the earlier window, so a reading that jumps about anyway has to move by more
/// than it jumps. Once an arrow shows it stays until the difference falls below half that margin, so a reading sitting
/// on the edge does not blink.
///
/// It points, it does not judge: rising is bad for a temperature and good for a download, so the arrow wears no
/// state's colour.
/// </summary>
public static class SensorTrend
{
    /// <summary>The readings that say where it is now.</summary>
    public static readonly TimeSpan Recent = TimeSpan.FromSeconds(3);

    /// <summary>The readings it is compared with, just before those.</summary>
    public static readonly TimeSpan Before = TimeSpan.FromSeconds(10);

    /// <summary>How far back a reading can still count, and so how long a sample is worth keeping.</summary>
    public static TimeSpan Kept => Recent + Before;

    /// <summary>The fewest readings each window needs before anything is said. Two recent ones, so one odd reading
    /// is not a trend; five earlier ones, so their spread means something.</summary>
    private const int LeastRecent = 2;
    private const int LeastBefore = 5;

    /// <summary>The smallest difference that is a trend, as a share of the reading's scale.</summary>
    private const double LeastShare = 0.02;

    /// <summary>How many times the earlier window's spread a difference must pass.</summary>
    private const double Spread = 1.5;

    /// <summary>
    /// Where the reading is heading at <paramref name="now"/>.
    ///
    /// <paramref name="samples"/> are the readings and when each was taken, in any order; a null is a reading that was
    /// not there, and counts for nothing — the sensor's own history writes it down as a nought, which is why a piece
    /// asking this keeps samples of its own. Nothing is said when the reading is missing now, or when either window
    /// has too few readings to go on.
    ///
    /// The scale is <paramref name="critical"/> where the reading has limits — a fiftieth of 95 °C is 1.9 degrees.
    /// Without them it is the larger of the range seen this session, <paramref name="min"/> to
    /// <paramref name="max"/>, and the size of the reading itself: watts or bytes have no top to take a share of.
    ///
    /// <paramref name="shown"/> is the arrow showing now, which a smaller difference keeps than the one that put it
    /// there.
    /// </summary>
    public static TrendArrow Of(IReadOnlyList<(TimeSpan At, float? Value)> samples, TimeSpan now, double? critical,
        float? min, float? max, TrendArrow shown)
    {
        if (samples.Count == 0) return TrendArrow.None;

        // The reading as it is now: the last one taken, if it is there.
        var latest = samples[0];
        foreach (var sample in samples)
            if (sample.At > latest.At) latest = sample;

        if (Missing(latest.Value)) return TrendArrow.None;

        var split = now - Recent;
        var start = split - Before;

        double recentSum = 0, beforeSum = 0;
        int recentCount = 0, beforeCount = 0;

        foreach (var (at, value) in samples)
        {
            if (Missing(value) || at > now || at <= start) continue;

            if (at > split)
            {
                recentSum += value!.Value;
                recentCount++;
            }
            else
            {
                beforeSum += value!.Value;
                beforeCount++;
            }
        }

        if (recentCount < LeastRecent || beforeCount < LeastBefore) return TrendArrow.None;

        var recentMean = recentSum / recentCount;
        var beforeMean = beforeSum / beforeCount;

        // The spread of the earlier window, from its mean in a second pass: the shortcut through the sum of squares
        // loses the spread altogether on a drive reading a billion bytes a second.
        double squares = 0;
        foreach (var (at, value) in samples)
            if (!Missing(value) && at <= split && at > start)
            {
                var off = value!.Value - beforeMean;
                squares += off * off;
            }

        var deviation = Math.Sqrt(squares / beforeCount);

        var scale = critical is > 0 and var limit
            ? limit
            : Math.Max(min is { } low && max is { } high ? Math.Abs(high - low) : 0, Math.Abs(beforeMean));

        var margin = Math.Max(LeastShare * scale, Spread * deviation);
        var difference = recentMean - beforeMean;

        // Kept on half the margin that put it there.
        if (shown == TrendArrow.Up && difference > margin / 2) return TrendArrow.Up;
        if (shown == TrendArrow.Down && difference < -margin / 2) return TrendArrow.Down;

        return difference > margin ? TrendArrow.Up
            : difference < -margin ? TrendArrow.Down
            : TrendArrow.None;
    }

    private static bool Missing(float? value) => value is not { } reading || !float.IsFinite(reading);
}
