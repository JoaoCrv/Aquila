using System.Collections.ObjectModel;
using Aquila.Models;
using Microsoft.Extensions.Logging;

namespace Aquila.Services;

/// <summary>
/// The one place the application says something about itself.
///
/// Both notifiers before this were transient by design: a snackbar is drawn inside a window that is
/// usually in the tray, and a tray balloon is gone the moment Windows decides it is. Neither is wrong —
/// an announcement should be brief — but neither leaves a record, so anything that arrived while nobody
/// was looking never happened.
///
/// This does not replace them. They announce; this remembers. An update found at boot still raises its
/// balloon AND lands here, and the two answer different questions: "did something just happen" and "what
/// is going on".
///
/// Kept in memory and never written to disk. An event is news, and news does not survive a restart —
/// an update notice reappearing three days later, after the update was installed, would be the panel
/// lying about the present in order to be thorough about the past. A condition needs no file at all,
/// because whatever asserts it will assert it again on the next run if it is still true.
/// </summary>
public sealed class NoticeService(ILogger<NoticeService> logger)
{
    private readonly ObservableCollection<Notice> _notices = [];

    /// <summary>Conditions first, then events newest-first — see <see cref="Ordered"/>.</summary>
    public ObservableCollection<Notice> Notices => _notices;

    /// <summary>Raised whenever the list or the unseen count changes, so a title-bar indicator can follow
    /// without binding to the collection twice.</summary>
    public event Action? Changed;

    /// <summary>
    /// The list in reading order: what is true now, then what happened, latest first.
    ///
    /// Conditions lead because they are about the present and a person opening this panel is asking about
    /// the present. Within each group the order is stable, so a panel left open does not reshuffle itself
    /// under the pointer.
    /// </summary>
    public IReadOnlyList<Notice> Ordered =>
    [
        .. _notices.Where(n => n.Shape is NoticeShape.Condition),
        .. _notices.Where(n => n.Shape is NoticeShape.Event).OrderByDescending(n => n.At),
    ];

    /// <summary>How many events have arrived without the panel being opened. Conditions are excluded on
    /// purpose: a badge counting them would never reach zero while the cause persists, and a badge that
    /// cannot be cleared stops being read.</summary>
    public int Unseen => _notices.Count(n => n.Shape is NoticeShape.Event && !n.Seen);

    /// <summary>The worst thing currently TRUE, or null. What the indicator is coloured by — an event that
    /// went badly an hour ago is history, and history should not keep an icon red.</summary>
    public StatusKind? Worst
    {
        get
        {
            var conditions = _notices.Where(n => n.Shape is NoticeShape.Condition).ToList();
            return conditions.Count == 0 ? null : conditions.Max(n => n.Kind);
        }
    }

    /// <summary>
    /// Asserts a condition. Called as often as the caller likes: the same key updates in place.
    ///
    /// Updating rather than replacing is what keeps the panel still. A check that runs on a timer would
    /// otherwise remove and re-add the same line every tick, and anything the user was reaching for would
    /// move out from under them.
    /// </summary>
    public void Set(string key, string title, string detail = "", StatusKind kind = StatusKind.Caution)
    {
        if (string.IsNullOrWhiteSpace(key)) return;

        if (_notices.FirstOrDefault(n => n.Shape is NoticeShape.Condition && n.Key == key) is { } existing)
        {
            if (existing.Title == title && existing.Detail == detail && existing.Kind == kind) return;

            existing.Title = title;
            existing.Detail = detail;
            existing.Kind = kind;
        }
        else
        {
            _notices.Add(new Notice
            {
                Key = key,
                Shape = NoticeShape.Condition,
                Title = title,
                Detail = detail,
                Kind = kind,
            });

            logger.LogInformation("Condition raised: {Key} — {Title}", key, title);
        }

        Changed?.Invoke();
    }

    /// <summary>Withdraws a condition. Silent when it was never true, because "stop saying the thing you
    /// were not saying" is a normal thing for a check to conclude.</summary>
    public void Clear(string key)
    {
        var gone = _notices.Where(n => n.Shape is NoticeShape.Condition && n.Key == key).ToList();
        if (gone.Count == 0) return;

        foreach (var notice in gone) _notices.Remove(notice);

        logger.LogInformation("Condition cleared: {Key}", key);
        Changed?.Invoke();
    }

    /// <summary>Records something that happened. Never collapsed with anything else: two of the same thing
    /// an hour apart are two things, and merging them discards the one that was missed.</summary>
    public void Raise(string title, string detail = "", StatusKind kind = StatusKind.Plain)
    {
        _notices.Add(new Notice
        {
            Shape = NoticeShape.Event,
            Title = title,
            Detail = detail,
            Kind = kind,
        });

        logger.LogInformation("Notice raised: {Title}", title);
        Changed?.Invoke();
    }

    /// <summary>Marks every event as seen. Called when the panel is opened, not when it is closed: the
    /// badge answers "is there anything I have not looked at", and it has been looked at.</summary>
    public void MarkSeen()
    {
        var unseen = _notices.Where(n => n.Shape is NoticeShape.Event && !n.Seen).ToList();
        if (unseen.Count == 0) return;

        foreach (var notice in unseen) notice.Seen = true;
        Changed?.Invoke();
    }

    /// <summary>Removes one event. Refuses a condition rather than throwing — the button is not offered
    /// for one, and a service that trusts its callers to have read the UI is a service with a hole in it.</summary>
    public void Dismiss(Notice notice)
    {
        if (!notice.CanDismiss || !_notices.Remove(notice)) return;

        Changed?.Invoke();
    }

    /// <summary>Clears every event, leaving every condition. The button says "clear", and a condition is
    /// not the panel's to clear — it belongs to whatever is still true.</summary>
    public void DismissAll()
    {
        var events = _notices.Where(n => n.Shape is NoticeShape.Event).ToList();
        if (events.Count == 0) return;

        foreach (var notice in events) _notices.Remove(notice);
        Changed?.Invoke();
    }
}
