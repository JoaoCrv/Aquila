using System.Windows.Media.Animation;
using Aquila.Models;
using LiveChartsCore;

namespace Aquila.Controls;

/// <summary>
/// Whether readings travel to their new value, and how fast.
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
/// a new speed is in force within one second of being chosen — and an event here would mean a subscription
/// in each piece, which is a leak per widget in exchange for a second.
///
/// The library's own "off" is a duration and not a flag, so this follows it: a speed of zero IS no
/// animation. One number, with nothing beside it to disagree.
///
/// There is also no choice of curve, and that is a measured decision rather than an omission. Three were
/// offered and none could be told apart. The mechanism was confirmed working — the dial's series reported
/// back the speed and the easing it had accepted, and the bar's travel was 5 to 70 pixels — so the curve
/// was reaching the drawing and simply could not be seen: below roughly 400 ms a viewer perceives THAT a
/// thing moved, not HOW, and 400 ms is this app's ceiling because the poll is one second. A control that
/// cannot work inside the constraint the application imposes is better deleted than explained.
/// </summary>
public static class Motion
{
    public static TimeSpan Speed { get; private set; } = TimeSpan.Zero;

    public static bool Enabled => Speed > TimeSpan.Zero;

    /// <summary>What to hand a LiveCharts chart. Off is <c>LiveCharts.DisableAnimations</c> — one
    /// millisecond rather than zero, which is the library's own constant for it.</summary>
    public static TimeSpan ChartSpeed => Enabled ? Speed : LiveCharts.DisableAnimations;

    /// <summary>Quick away, slow into place — a reading settling rather than arriving. The two below are
    /// the SAME curve in the two drawing worlds: sampled against each other they agree to three decimal
    /// places at every point. Pairing them by name instead is what once had the dial and the bar moving
    /// along visibly different paths under one setting.</summary>
    public static Func<float, float> ChartEasing { get; } = EasingFunctions.CubicOut;

    public static IEasingFunction WpfEasing { get; } = new CubicEase { EasingMode = EasingMode.EaseOut };

    public static void Apply(AppSettings settings) => Apply(settings.AnimationSpeedMs);

    public static void Apply(int milliseconds) =>
        Speed = TimeSpan.FromMilliseconds(Math.Max(0, milliseconds));
}
