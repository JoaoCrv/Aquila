using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;
using Aquila.ViewModels.Windows;

namespace Aquila.Views.Windows;

/// <summary>
/// The panel a left click on the tray icon opens. See the XAML for what it is for.
///
/// Built once and shown and hidden after that, never closed: it is a glance, and rebuilding the readings
/// and the window every time someone looks would be a cost paid for nothing.
/// </summary>
public partial class TrayFlyout : Wpf.Ui.Controls.FluentWindow
{
    private readonly Action _open;
    private readonly Action _editWidgets;
    private readonly Action _installUpdate;
    private readonly Action _exit;

    /// <summary>When the flyout last went away, so the click that took it away is not read as a request
    /// to open it again. See <see cref="Toggle"/>.</summary>
    private long _hiddenAt;

    public TitleBarViewModel TitleBar { get; }
    public NoticeCenterViewModel Notices { get; }

    public TrayFlyout(TitleBarViewModel titleBar, NoticeCenterViewModel notices,
        Action open, Action editWidgets, Action installUpdate, Action exit)
    {
        TitleBar = titleBar;
        Notices = notices;
        _open = open;
        _editWidgets = editWidgets;
        _installUpdate = installUpdate;
        _exit = exit;

        InitializeComponent();
        DataContext = this;

        // Gone the moment it stops being the window in front — a click on the desktop, another app, the
        // taskbar. That is the whole of how a flyout is dismissed, and it is why this needs no helper
        // window: it IS the window of ours in the foreground, so it hears the click that ends it.
        Deactivated += (_, _) => Dismiss();
    }

    public void ShowUpdate(bool available) =>
        UpdateButton.Visibility = available ? Visibility.Visible : Visibility.Collapsed;

    /// <summary>
    /// Opens the flyout, or closes it if it is open.
    ///
    /// The awkward case is clicking the tray icon to CLOSE it. The press reaches the taskbar first, which
    /// takes the focus, which hides the flyout through Deactivated — and only then does the click arrive
    /// here, finding it hidden and opening it again. So a click that lands within a moment of the flyout
    /// going away is taken to be the click that sent it away, and does nothing more.
    /// </summary>
    public void Toggle()
    {
        if (IsVisible)
        {
            Dismiss();
            return;
        }

        if (Environment.TickCount64 - _hiddenAt < 400) return;

        Show();
        UpdateLayout();
        PlaceAboveTray();
        Activate();
        SetForegroundWindow(new WindowInteropHelper(this).Handle);
    }

    /// <summary>
    /// Sits in the corner of the work area nearest the tray, a little off the edges, as the system flyouts
    /// do.
    ///
    /// The work area is the screen minus the taskbar, so whichever side it does NOT reach to is the side
    /// the taskbar is on. Placed after the window has laid itself out, because its height comes from its
    /// content and is not known before.
    /// </summary>
    private void PlaceAboveTray()
    {
        const double gap = 12;
        var area = SystemParameters.WorkArea;

        Left = area.Left > 0 ? area.Left + gap : area.Right - ActualWidth - gap;
        Top = area.Top > 0 ? area.Top + gap : area.Bottom - ActualHeight - gap;
    }

    private void Dismiss()
    {
        if (!IsVisible) return;

        _hiddenAt = Environment.TickCount64;
        Hide();
    }

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Dismiss();
            e.Handled = true;
            return;
        }

        base.OnPreviewKeyDown(e);
    }

    /// <summary>
    /// Every action runs after the flyout has gone, never inside the click. Inside it the flyout is still
    /// the active window, so anything the action opened — the update question, say — would take it as an
    /// owner, and be hidden along with it a moment later.
    /// </summary>
    private void Then(Action action)
    {
        Dismiss();
        Dispatcher.BeginInvoke(action);
    }

    private void OnOpen(object sender, RoutedEventArgs e) => Then(_open);
    private void OnEditWidgets(object sender, RoutedEventArgs e) => Then(_editWidgets);
    private void OnUpdate(object sender, RoutedEventArgs e) => Then(_installUpdate);
    private void OnExit(object sender, RoutedEventArgs e) => Then(_exit);

    /// <summary>Opens Aquila and then the notice panel in it. The panel is a popup anchored to the bell, so
    /// it is opened only once the window has been laid out — before that there is no bell to anchor to.</summary>
    private void OnNotices(object sender, RoutedEventArgs e) => Then(() =>
    {
        _open();
        Dispatcher.BeginInvoke(() => Notices.IsOpen = true, DispatcherPriority.Loaded);
    });

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(IntPtr hWnd);
}
