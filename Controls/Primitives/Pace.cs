using System.Windows.Media.Animation;

namespace Aquila.Controls.Primitives;

/// <summary>
/// How the elements move: how long a move takes, on what curve, at how many frames a second at most, and whether a
/// line slides with time between readings.
///
/// Set by whatever hosts the primitives — Aquila's Motion, from the machine's settings — and read by each element at
/// the moment it moves. So the primitives keep a pace without knowing whose it is, which is what lets a bar's
/// <see cref="BarFill.MoveTo"/> be just a value and whether to travel, and a chart's scale travel at all: it has no
/// caller at the moment its scale changes, only a reading that arrived.
///
/// There is deliberately no changed event, as Motion has none: an element reads the pace when it next moves, so a new
/// one is in force by the next reading, and nothing subscribes.
/// </summary>
public static class Pace
{
    /// <summary>How long a move takes. Nought is no movement at all: everything jumps.</summary>
    public static TimeSpan Duration { get; private set; } = TimeSpan.Zero;

    /// <summary>The curve a move takes; null is a straight line.</summary>
    public static IEasingFunction? Easing { get; private set; }

    /// <summary>The most frames a second a move may ask to be drawn at.</summary>
    public static int FrameRate { get; private set; } = 30;

    /// <summary>Whether a line slides steadily with time between readings, rather than standing still until the next.
    /// The one movement that never stops, so it is asked for separately.</summary>
    public static bool Slide { get; private set; }

    /// <summary>Whether anything moves at all.</summary>
    public static bool Moves => Duration > TimeSpan.Zero;

    public static void Set(TimeSpan duration, IEasingFunction? easing, int frameRate, bool slide)
    {
        Duration = duration < TimeSpan.Zero ? TimeSpan.Zero : duration;
        Easing = easing;
        FrameRate = Math.Max(1, frameRate);
        Slide = slide;
    }

    /// <summary>A move to <paramref name="to"/> at this pace, as WPF draws it: no From, so it starts wherever the value
    /// is drawn, at the pace's duration, curve and frame rate.</summary>
    internal static DoubleAnimation To(double to)
    {
        var move = new DoubleAnimation(to, Duration) { EasingFunction = Easing };
        Timeline.SetDesiredFrameRate(move, FrameRate);
        return move;
    }
}
