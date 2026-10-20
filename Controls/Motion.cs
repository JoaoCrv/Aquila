using System.Windows.Media.Animation;
using Aquila.Models;
using LiveChartsCore;

namespace Aquila.Controls;

/// <summary>
/// Whether readings travel to their new value, how fast, and at how many frames a second.
///
/// A setting of this machine, not of a preset. A preset is made to be shared, and colours and typefaces
/// arrive on someone else's computer as the same thing — while a duration arrives as a cost in frames,
/// on hardware the author knows nothing about. So it lives in AppSettings and applies everywhere.
///
/// Static, like <see cref="Aquila.Services.VitalMonitor.Current"/> and for the same reason: the pieces that
/// need it are controls built by WidgetCatalog and by XAML, neither of which can be handed anything by the
/// container.
///
/// There is deliberately no changed event. Every piece that animates is redrawn on the poll tick anyway, so
/// a new speed is in force by the next tick — and an event here would mean a subscription in each piece,
/// which is a leak per widget in exchange for a moment.
///
/// The library's own "off" is a duration and not a flag, so this follows it: a speed of zero IS no
/// animation. One number, with nothing beside it to disagree.
///
/// There is also no choice of curve, and that is a measured decision rather than an omission. Three were
/// offered and none could be told apart. The mechanism was confirmed working — the dial's series reported
/// back the speed and the easing it had accepted, and the bar's travel was 5 to 70 pixels — so the curve
/// was reaching the drawing and simply could not be seen: below roughly 400 ms a viewer perceives THAT a
/// thing moved, not HOW, and the speed in force never exceeds two fifths of the poll — 400 ms at the default
/// one second. A control that cannot work inside the constraint the application imposes is better deleted
/// than explained.
///
/// One piece reads only whether this is on, not how fast: the clock's rings, which move through the whole of
/// every second when it is, arriving on each one. They are not on the poll, so the cap below has nothing to
/// protect there, and time moves between two seconds in a way a reading does not move between two polls — see
/// ClockTile.Sweep. The frame rate they follow like everything else.
/// </summary>
public static class Motion
{
    public static TimeSpan Speed { get; private set; } = TimeSpan.Zero;

    public static bool Enabled => Speed > TimeSpan.Zero;

    /// <summary>
    /// The most frames a second anything Aquila animates may ask to be drawn at.
    ///
    /// Beside the speed and for the same reason: a frame is a cost paid on this machine, by the machine being
    /// measured, and only its owner can say what it is worth. Every WPF animation Aquila makes carries it; the
    /// charts take it once, at start — see App.OnStartup.
    /// </summary>
    public static int FrameRate { get; private set; } = DefaultFrameRate;

    public const int DefaultFrameRate = 30;
    public const int LeastFrameRate = 15;
    public const int MostFrameRate = 60;

    /// <summary>What to hand a LiveCharts chart. Off is <c>LiveCharts.DisableAnimations</c> — one
    /// millisecond rather than zero, which is the library's own constant for it.</summary>
    public static TimeSpan ChartSpeed => Enabled ? Speed : LiveCharts.DisableAnimations;

    /// <summary>Quick away, slow into place — a reading settling rather than arriving. The two below are
    /// the SAME curve in the two drawing worlds: sampled against each other they agree to three decimal
    /// places at every point. Pairing them by name instead is what once had the dial and the bar moving
    /// along visibly different paths under one setting.</summary>
    public static Func<float, float> ChartEasing { get; } = EasingFunctions.CubicOut;

    public static IEasingFunction WpfEasing { get; } = new CubicEase { EasingMode = EasingMode.EaseOut };

    public static void Apply(AppSettings settings)
    {
        Apply(settings.AnimationSpeedMs, settings.PollingIntervalMs);

        // Held here and not trusted from the file: the menu offers only rates inside these, but the file is
        // the user's to edit, and a rate of nothing would be refused by every animation that carried it.
        FrameRate = Math.Clamp(settings.MaxFrameRate, LeastFrameRate, MostFrameRate);
    }

    /// <summary>
    /// The chosen speed, held to two fifths of the poll interval.
    ///
    /// A sweep has to end well inside one tick, or the piece is in transit more often than it is right. The
    /// menu stops at 400 ms because that is two fifths of the default second — but the poll is a setting of
    /// its own, and at 500 ms the same "Relaxed" kept the dial moving four fifths of the time. Held here,
    /// where both numbers meet, rather than by narrowing the menu, because the two change independently.
    /// </summary>
    public static void Apply(int speedMs, int pollMs) =>
        Speed = TimeSpan.FromMilliseconds(Math.Clamp(speedMs, 0, Math.Max(0, pollMs) * 2 / 5));

    /// <summary>A reading's sweep to <paramref name="to"/>, as WPF draws it: at the speed and on the curve in
    /// force, and no oftener than the frame rate. One home, so the bars cannot come to move differently from each
    /// other.</summary>
    public static DoubleAnimation Sweep(double to) => Paced(new DoubleAnimation(to, Speed) { EasingFunction = WpfEasing });

    /// <summary>Puts the frame rate on an animation Aquila makes. Asked of the root of what is begun — WPF reads it
    /// there and nowhere else.</summary>
    public static T Paced<T>(T timeline) where T : Timeline
    {
        Timeline.SetDesiredFrameRate(timeline, FrameRate);
        return timeline;
    }
}
