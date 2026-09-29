namespace Aquila.Models;

/// <summary>
/// What a notice IS, and the whole reason there is one panel rather than two.
///
/// A CONDITION is true right now and stops being true on its own: running unelevated, a driver that will
/// not start, a widget naming a sensor this machine does not have. It is keyed, because it is one fact
/// being re-asserted rather than a stream of separate ones — a check that runs every minute must not leave
/// sixty copies behind. It cannot be dismissed either: dismissing something still true is a lie the app
/// would be telling on the user's behalf, and the tidy panel that follows is worse than the untidy one.
///
/// An EVENT happened at a moment and is over: an update was found, a preset failed to load, a file was
/// written. It survives not being looked at, which is the whole point — today's notifiers are transient by
/// design, so an update notice that arrived while the window was in the tray is simply gone. It CAN be
/// dismissed, because the moment really has passed and there is nothing left to be wrong about.
///
/// The two are one type and not two, because the panel that shows them is one panel. Splitting them into
/// separate types is how you end up with separate lists, separate indicators and a user asked to learn
/// which of two places to look.
/// </summary>
public enum NoticeShape
{
    Condition,
    Event,
}

/// <summary>
/// One line in the notice panel.
///
/// Mutable on purpose. A condition re-asserted with a new message updates in place rather than being
/// replaced, so the panel does not flicker and anything the user is pointing at stays where it is.
/// </summary>
public sealed class Notice
{
    /// <summary>
    /// What this notice is ABOUT, for a condition — "elevation", "driver". Two raises with the same key
    /// are the same fact said twice, and the second one wins.
    ///
    /// Empty for an event: two updates found an hour apart are two things that happened, and collapsing
    /// them would throw away the one that was missed.
    /// </summary>
    public string Key { get; init; } = string.Empty;

    public NoticeShape Shape { get; init; }

    /// <summary>The headline, in the user's words rather than the code's. One line.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>What to do about it, or why it matters. Optional: a notice that needs no explanation
    /// should not be given one.</summary>
    public string Detail { get; set; } = string.Empty;

    /// <summary>
    /// How serious, decided WHERE IT IS WRITTEN and never by matching the text.
    ///
    /// Only the writer knows whether "Update available" is good news or a nudge, and any rule that reads
    /// the words breaks on the first rewording — or the first translation.
    /// </summary>
    public StatusKind Kind { get; set; } = StatusKind.Plain;

    /// <summary>When it arrived. Shown for events, where "an hour ago" is part of the meaning, and not for
    /// conditions, where the only thing that matters is that it is true now.</summary>
    public DateTimeOffset At { get; init; } = DateTimeOffset.Now;

    /// <summary>Whether the panel has been opened since this arrived. Events only — a condition is not
    /// news, it is a state, and it does not stop counting because you glanced at it.</summary>
    public bool Seen { get; set; }

    /// <summary>A condition is never dismissible: it goes when it stops being true and not before.</summary>
    public bool CanDismiss => Shape is NoticeShape.Event;
}
