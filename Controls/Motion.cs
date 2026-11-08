using System.Windows;
using System.Windows.Media.Animation;
using Aquila.Controls.Primitives;
using Aquila.Models;

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
/// A speed of zero IS no animation — as LiveCharts' own "off" was a duration and not a flag, when the dial was
/// one of its charts. One number, with nothing beside it to disagree.
///
/// There is also no choice of curve, and that is a measured decision rather than an omission. Three were
/// offered and none could be told apart. The mechanism was confirmed working — the dial's series reported
/// back the speed and the easing it had accepted, and the bar's travel was 5 to 70 pixels — so the curve
/// was reaching the drawing and simply could not be seen: below roughly 400 ms a viewer perceives THAT a
/// thing moved, not HOW. That was measured while nothing could move for longer. Smooth now takes 600 ms and
/// Relaxed 900 (an experiment, 2026-11-01), where a curve may show, and it has not been looked at again.
///
/// One piece reads only whether this is on, not how fast: the clock's rings, which move through the whole of
/// every second when it is, arriving on each one. They are not on the poll, so the cap below has nothing to
/// protect there, and time moves between two seconds in a way a reading does not move between two polls — see
/// ClockTile.Sweep. The frame rate they follow like everything else.
///
/// The dial's arc moves at this speed but steadily, as every ring does (RingArc.MoveTo), while the bars ease into
/// place. Two paths under one setting, and the measurement above is why that was allowed: up to 400 ms nobody sees
/// the curve. Whether the two still look alike at 600 and 900 is part of the same experiment.
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

    /// <summary>Quick away, slow into place — a bar settling rather than arriving.</summary>
    public static IEasingFunction WpfEasing { get; } = new CubicEase { EasingMode = EasingMode.EaseOut };

    public static void Apply(AppSettings settings)
    {
        Apply(settings.AnimationSpeedMs, settings.PollingIntervalMs);

        // Held here and not trusted from the file: the menu offers only rates inside these, but the file is
        // the user's to edit, and a rate of nothing would be refused by every animation that carried it.
        FrameRate = Math.Clamp(settings.MaxFrameRate, LeastFrameRate, MostFrameRate);
    }

    /// <summary>
    /// The chosen speed, held to nine tenths of the poll interval.
    ///
    /// A sweep has to end inside one tick, or the piece is still on its way to one reading when the next
    /// arrives. At nine tenths it does, if only just: the default second allows Relaxed its 900 ms with a tenth
    /// left at rest, and a 500 ms poll holds every choice to 450. Held here, where both numbers meet, rather
    /// than by narrowing the menu, because the two change independently.
    ///
    /// Two fifths until 2026-11-01, which kept the reading at rest for most of every read — raised to try steps
    /// that can be told apart. At two fifths the slowest step was 400 ms against Smooth's 300, and the two
    /// looked the same.
    /// </summary>
    public static void Apply(int speedMs, int pollMs) =>
        Speed = TimeSpan.FromMilliseconds(Math.Clamp(speedMs, 0, Math.Max(0, pollMs) * 9 / 10));

    /// <summary>A reading's sweep to <paramref name="to"/>, as WPF draws it: at the speed and on the curve in
    /// force, and no oftener than the frame rate. One home, so the bars cannot come to move differently from each
    /// other.</summary>
    public static DoubleAnimation Sweep(double to) => Paced(new DoubleAnimation(to, Speed) { EasingFunction = WpfEasing });

    /// <summary>
    /// Takes a property to <paramref name="to"/>: by a <see cref="Sweep"/> while motion is on, at once while it is off
    /// or when <paramref name="animate"/> says not to — a piece resized, a row that has only just appeared.
    ///
    /// The one home for that choice, which each bar wrote out by hand and the list would have written three times more
    /// — its rows sliding, fading and filling. Both halves have a trap in them. The jump lets go of the animation
    /// first: a property held by one ignores what it is set to, so a bar animated once and then switched to instant
    /// would freeze where it last swept to. The sweep has no From and is handed off by snapshot, so it starts wherever
    /// the property is DRAWN: a value arriving mid-way redirects instead of jumping back to begin again, and a poll
    /// faster than the sweep degrades into a slower sweep rather than into a stutter.
    ///
    /// <paramref name="arrived"/> runs once the property is there — at once, for a jump. A sweep replaced by a later
    /// one may still report, so whether it is still wanted is for the caller to check when it runs.
    /// </summary>
    public static void To<T>(T target, DependencyProperty property, double to, bool animate = true,
        Action? arrived = null) where T : DependencyObject, IAnimatable
    {
        if (!animate || !Enabled)
        {
            target.BeginAnimation(property, null);
            target.SetValue(property, to);
            arrived?.Invoke();
            return;
        }

        var sweep = Sweep(to);
        if (arrived is not null) sweep.Completed += (_, _) => arrived();

        target.BeginAnimation(property, sweep, HandoffBehavior.SnapshotAndReplace);
    }

    /// <summary>
    /// Takes a bar to <paramref name="fraction"/> at the speed, on the curve and at the frame rate in force — at once
    /// while motion is off or when <paramref name="animate"/> says not to.
    ///
    /// The bar itself cannot ask: it is a primitive, and knows WPF and nothing of Aquila's settings. So this is where a
    /// bar of Aquila's learns how bars move here, once, rather than in each piece that draws one.
    /// </summary>
    public static void Move(BarFill bar, double fraction, bool animate = true) =>
        bar.MoveTo(fraction, animate && Enabled ? Speed : TimeSpan.Zero, WpfEasing, FrameRate);

    /// <summary>Puts the frame rate on an animation Aquila makes. Asked of the root of what is begun — WPF reads it
    /// there and nowhere else.</summary>
    public static T Paced<T>(T timeline) where T : Timeline
    {
        Timeline.SetDesiredFrameRate(timeline, FrameRate);
        return timeline;
    }
}
