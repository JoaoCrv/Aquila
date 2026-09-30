using System.Windows;
using Wpf.Ui.Controls;

namespace Aquila.Services;

/// <summary>
/// The one place the application stops and asks, or stops and says.
///
/// Eleven call sites were building their own <c>System.Windows.MessageBox</c>, which meant eleven chances
/// to disagree about chrome, about owners, and about how a question is worded. Central not for tidiness
/// but because the next change — a different look, a different default, an owner rule — is then one edit
/// rather than a hunt.
///
/// WPF-UI's MessageBox is a FluentWindow, so these wear the app's own chrome instead of the system's grey
/// box. It has ONE way to be shown, <c>ShowDialogAsync</c>, with no synchronous overload — that is the
/// kit's design and not an oversight, and it is why the callers here are async.
///
/// ITS BUTTONS ARE NAMED ACTIONS, not Yes and No, and this is the part worth keeping. "Save as new" and
/// "Put it back" say what will happen; Yes and No only say that something will. That moves the question
/// out of the message and into the buttons, which is where a person reading quickly actually looks — so
/// <see cref="Ask"/> takes the two answers as words and refuses to have a default pair.
/// </summary>
public static class Dialogs
{
    /// <summary>
    /// Says something and waits for it to be acknowledged. One button, because there is nothing to decide.
    ///
    /// The owner is found rather than passed: almost every caller is a view model with no window to hand,
    /// and the one thing worse than a dialog with the wrong owner is one behind the window that raised it.
    /// </summary>
    public static async Task Tell(string title, string message, Window? owner = null)
    {
        var box = new Wpf.Ui.Controls.MessageBox
        {
            Title = title,
            Content = message,
            CloseButtonText = "OK",
            Owner = owner ?? Active(),
        };

        await box.ShowDialogAsync();
    }

    /// <summary>
    /// Asks a question whose answers are two actions, and returns true for the first of them.
    ///
    /// Both answers are named by the caller. There is deliberately no overload taking a message alone: a
    /// dialog that has to fall back on Yes and No is one where nobody decided what the buttons do, and
    /// that decision is the whole of making the question readable.
    ///
    /// Cancelling — Escape, or the title bar's close — is NOT the first answer. The kit reports it as
    /// None, which falls through to false, so the safe outcome has to be the one written second.
    /// </summary>
    public static async Task<bool> Ask(string title, string message, string yes, string no,
        Window? owner = null)
    {
        var box = new Wpf.Ui.Controls.MessageBox
        {
            Title = title,
            Content = message,
            PrimaryButtonText = yes,
            CloseButtonText = no,
            Owner = owner ?? Active(),
        };

        return await box.ShowDialogAsync() == Wpf.Ui.Controls.MessageBoxResult.Primary;
    }

    /// <summary>
    /// The window a dialog should sit over: whichever is active, or the main one, or none.
    ///
    /// None is a real answer and not a failure. Aquila spends most of its life in the tray with no window
    /// shown at all, and an ownerless dialog is right there — asking to be owned by a hidden window would
    /// put the dialog behind nothing, which is to say nowhere.
    /// </summary>
    private static Window? Active()
    {
        var app = Application.Current;
        if (app is null) return null;

        return app.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive && w.IsVisible)
            ?? (app.MainWindow is { IsVisible: true } main ? main : null);
    }
}
