using System.Windows.Threading;
using Aquila.Controls;
using Aquila.Models;
using Aquila.Models.Nodes;

namespace Aquila.Services;

/// <summary>
/// Keeps <see cref="AquilaState.Clock"/> on the time, once a second — one timer for every clock there is.
///
/// Not the hardware poll. The poll is a setting, and at two or five seconds a clock hanging off it would jump its
/// seconds; there is no scheduler of sources yet (#12), so the clock is a source of its own beside it. And not a
/// timer per clock either, which is how it began: each piece kept its own, ticking out of step with the others,
/// so two clocks side by side could show different seconds for most of every second.
///
/// The timer is re-armed on every tick for just after the next whole second, rather than left on a one-second
/// interval. A fixed interval drifts against the second it is meant to follow — late by a few milliseconds a tick,
/// it reaches the end of a second and shows the same one twice, then skips the next.
///
/// It runs whether or not anything shows a clock. One tick a second that writes one value is not worth the
/// bookkeeping it would take to stop it.
/// </summary>
public sealed class ClockService(AquilaState state) : IDisposable
{
    /// <summary>
    /// The clock the pieces read.
    ///
    /// Static, like <see cref="VitalMonitor.Current"/> and for the same reason: a clock is a control built by
    /// WidgetCatalog — and by the editor's type picker — and neither can be handed anything by the container.
    /// Null until <see cref="Start"/>, and a clock that finds it null shows the time it was drawn at.
    /// </summary>
    public static ClockNode? Current { get; private set; }

    // Normal rather than the Background the poll runs at: a clock that is late is wrong, a sensor that is late
    // is only late.
    private readonly DispatcherTimer _timer = new(DispatcherPriority.Normal);

    public void Start()
    {
        Current = state.Clock;

        _timer.Tick += OnTick;
        OnTick(null, EventArgs.Empty);
        _timer.Start();
    }

    private void OnTick(object? sender, EventArgs e)
    {
        var now = DateTime.Now;

        // A tick that woke early, still inside the second already shown, writes nothing: writing it would show
        // that second twice. The next one is armed for just past the boundary all the same.
        if (!ClockFace.SameSecond(now, state.Clock.Now)) state.Clock.Now = now;

        _timer.Interval = ClockFace.UntilNextSecond(now);
    }

    public void Dispose()
    {
        _timer.Stop();
        _timer.Tick -= OnTick;
    }
}
