using Aquila.Models;
using Aquila.Services;
using Wpf.Ui.Controls;

namespace Aquila.ViewModels.Windows;

/// <summary>
/// The title bar's notice indicator and the panel behind it.
///
/// Kept apart from MainWindowViewModel for the same reason TitleBarViewModel is: that one is about the
/// window, and this is about what the application has to say.
/// </summary>
public partial class NoticeCenterViewModel : ObservableObject
{
    private readonly NoticeService _notices;

    public NoticeCenterViewModel(NoticeService notices)
    {
        _notices = notices;
        _notices.Changed += Refresh;
    }

    /// <summary>Conditions first, then events newest-first.</summary>
    public IReadOnlyList<Notice> Items => _notices.Ordered;

    public bool IsEmpty => Items.Count == 0;

    /// <summary>Events that arrived without the panel being opened. Conditions are not counted: a badge
    /// that cannot reach zero while the cause persists stops being read.</summary>
    public int Unseen => _notices.Unseen;

    public bool HasUnseen => Unseen > 0;

    /// <summary>A count up to nine, then "9+". A two-digit badge on a title-bar glyph is unreadable, and
    /// the difference between eleven and twelve notices is not information anybody acts on.</summary>
    public string UnseenLabel => Unseen > 9 ? "9+" : Unseen.ToString();

    /// <summary>
    /// What the bell is coloured by: the worst thing currently TRUE, never an event.
    ///
    /// An event that went badly an hour ago is history, and history must not keep an icon red — an
    /// indicator that stays lit after the thing it was about is over teaches people to stop looking at it.
    /// </summary>
    public StatusKind Indicator => _notices.Worst ?? StatusKind.Plain;

    /// <summary>Whether anything is being said at all. The bell is dimmed when nothing is: having nothing
    /// to report is the common case and should look like it, rather than like a control waiting to be
    /// pressed.</summary>
    public bool HasAnything => Items.Count > 0;

    /// <summary>
    /// The panel in one line, for a surface with no room for the panel — the tray flyout.
    ///
    /// The worst thing currently TRUE if there is one, because that is what someone glancing at the tray
    /// most needs; otherwise how many events are waiting; otherwise that there is nothing. The same order
    /// the panel reads in, cut to its first line.
    /// </summary>
    public string Headline
    {
        get
        {
            var worst = Items
                .Where(n => n.Shape is NoticeShape.Condition)
                .OrderByDescending(n => n.Kind)
                .FirstOrDefault();

            if (worst is not null) return worst.Title;

            return Unseen switch
            {
                0 => "Nothing to report",
                1 => "1 new notice",
                var n => $"{n} new notices",
            };
        }
    }

    public string Tooltip => Items.Count switch
    {
        0 => "Nothing to report",
        1 => "1 notice",
        var n => $"{n} notices",
    };

    /// <summary>
    /// Whether the panel is showing.
    ///
    /// Opening marks the events seen. On OPEN rather than on close, because the badge answers "is there
    /// anything I have not looked at" — and by then it has been looked at. Waiting for the close would
    /// leave the count sitting there beside the very list the user is reading.
    /// </summary>
    [ObservableProperty] private bool _isOpen;

    partial void OnIsOpenChanged(bool value)
    {
        if (value) _notices.MarkSeen();
    }

    [RelayCommand]
    private void Toggle() => IsOpen = !IsOpen;

    [RelayCommand]
    private void Dismiss(Notice? notice)
    {
        if (notice is not null) _notices.Dismiss(notice);
    }

    /// <summary>Clears the events and leaves every condition, which is why the button says "Clear" and not
    /// "Clear all" — a condition is not the panel's to clear.</summary>
    [RelayCommand]
    private void Clear() => _notices.DismissAll();

    public bool CanClear => Items.Any(n => n.CanDismiss);

    private void Refresh()
    {
        System.Windows.Application.Current?.Dispatcher.Invoke(() =>
        {
            OnPropertyChanged(nameof(Items));
            OnPropertyChanged(nameof(IsEmpty));
            OnPropertyChanged(nameof(Unseen));
            OnPropertyChanged(nameof(HasUnseen));
            OnPropertyChanged(nameof(UnseenLabel));
            OnPropertyChanged(nameof(Indicator));
            OnPropertyChanged(nameof(HasAnything));
            OnPropertyChanged(nameof(Headline));
            OnPropertyChanged(nameof(Tooltip));
            OnPropertyChanged(nameof(CanClear));
        });
    }
}
