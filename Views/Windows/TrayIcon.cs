using Aquila.Services;
using Aquila.ViewModels.Windows;
using Microsoft.Extensions.DependencyInjection;

namespace Aquila.Views.Windows;

/// <summary>
/// The icon in the notification area — its update badge, its balloons, and the menu and flyout its clicks open.
///
/// Owned by no window. It lived in MainWindow, which did no harm while that window was built at every start; now
/// the window is built the first time it is opened (<see cref="MainWindowService"/>), and the icon is the only way
/// in for someone running Aquila in the tray, so it cannot wait for the window. Shown by ApplicationHostService
/// before any window, and gone when the application exits, whichever way it exits.
///
/// The icon, the badge and the balloons are WinForms; only what the clicks open is WPF (see TrayMenu).
/// </summary>
public sealed class TrayIcon : ITrayNotifier, IDisposable
{
    private readonly MainWindowService _mainWindow;
    private readonly UpdateService _updateService;
    private readonly IServiceProvider _services;

    private readonly System.Windows.Forms.NotifyIcon _icon;
    private readonly System.Drawing.Icon _baseIcon;
    private readonly TrayMenu _menu;
    private System.Drawing.Icon? _badgedIcon;
    private TrayFlyout? _flyout;
    private Action? _balloonClick;
    private bool _disposed;

    public TrayIcon(MainWindowService mainWindow, UpdateService updateService, IServiceProvider services)
    {
        _mainWindow = mainWindow;
        _updateService = updateService;
        _services = services;

        _baseIcon = AppIcon(); // kept so the badge can be added and removed without re-extracting

        // A WPF menu rather than the WinForms ContextMenuStrip it replaced, so it wears the theme — see
        // TrayMenu. No ContextMenuStrip is attached to the icon, so a right click does nothing until the
        // handler below opens ours.
        _menu = new TrayMenu(_mainWindow.Open, EditWidgets, InstallUpdate, Exit);

        _icon = new System.Windows.Forms.NotifyIcon
        {
            Icon = _baseIcon,
            Text = "Aquila",
        };

        // Left click opens the flyout and right click the menu — the Windows 11 arrangement, where the
        // left button gives an app's own panel and the right one the plain list every icon has.
        _icon.MouseClick += (_, e) => { if (e.Button == System.Windows.Forms.MouseButtons.Left) Flyout().Toggle(); };

        // On the button's RELEASE, which is when Windows opens every other context menu.
        _icon.MouseUp += (_, e) => { if (e.Button == System.Windows.Forms.MouseButtons.Right) _menu.Open(); };
        _icon.DoubleClick += (_, _) => _mainWindow.Open();

        // Clicking a notification brings Aquila up — the least surprising outcome, and the only way
        // back for someone running it hidden in the tray.
        _icon.BalloonTipClicked += (_, _) =>
        {
            var action = _balloonClick;
            _balloonClick = null;
            if (action is not null) action(); else _mainWindow.Open();
        };

        _updateService.StatusChanged += RefreshUpdateBadge;
        RefreshUpdateBadge();

        // Every way out ends here — the tray's Exit, the main or dashboard window closed, a restart — so the
        // icon is taken down in one place rather than by whichever of them happened to own it. An icon left
        // behind sits in the tray until the pointer passes over it.
        Application.Current.Exit += (_, _) => Dispose();
    }

    /// <summary>Puts the icon in the notification area.</summary>
    public void Show() => _icon.Visible = true;

    /// <summary>
    /// Shows a notification from the tray icon. On Windows 10/11 this renders as a normal toast and lands in
    /// the Action Centre, so it survives the user being away from the machine — unlike an in-window snackbar,
    /// which is both invisible while the window is hidden and gone after a few seconds.
    /// </summary>
    public void Notify(string title, string message, Action? onClick = null)
    {
        // One handler at a time: the click handler captures this specific notification's action, so a
        // stale one would run the wrong thing.
        _balloonClick = onClick;
        _icon.ShowBalloonTip(10_000, title, message, System.Windows.Forms.ToolTipIcon.Info);
    }

    public bool IsWindowVisible => _mainWindow.IsOnScreen;

    /// <summary>
    /// The tray flyout, built on first use and kept.
    ///
    /// A left click used to show or hide the main window directly. It opens the flyout now, which carries
    /// "Open Aquila" among the rest — the window is one press further away, and in exchange the click answers
    /// the question someone running Aquila in the tray actually has, which is how the machine is doing,
    /// without a whole window to open and put away again. Double click still opens the window.
    ///
    /// Its view models are asked for here rather than injected, so that nothing builds them before somebody
    /// looks: they are the main window's too, and whichever of the two comes first makes them.
    /// </summary>
    private TrayFlyout Flyout()
    {
        if (_flyout is null)
        {
            _flyout = new TrayFlyout(
                _services.GetRequiredService<TitleBarViewModel>(),
                _services.GetRequiredService<NoticeCenterViewModel>(),
                _mainWindow.Open, EditWidgets, InstallUpdate, Exit);
            _flyout.ShowUpdate(_updateService.IsUpdateAvailable);
        }

        return _flyout;
    }

    /// <summary>
    /// Marks the tray icon while an update is waiting, so the signal survives a missed notification and is
    /// visible even with the window closed — which is how Aquila normally runs.
    /// </summary>
    private void RefreshUpdateBadge()
    {
        Application.Current?.Dispatcher.Invoke(() =>
        {
            if (_disposed) return;

            var available = _updateService.IsUpdateAvailable;

            _icon.Text = available ? "Aquila — update available" : "Aquila";
            _icon.Icon = available ? (_badgedIcon ??= BuildBadgedIcon(_baseIcon)) : _baseIcon;

            _menu.ShowUpdate(available);
            _flyout?.ShowUpdate(available);
        });
    }

    /// <summary>
    /// Starts a desktop edit session from the tray.
    ///
    /// Resolved when clicked rather than injected: the view model is a singleton either way, and asking for
    /// it at startup would tie two objects together that have no reason to know about each other until
    /// somebody opens a menu.
    /// </summary>
    private void EditWidgets() =>
        _services.GetService<ViewModels.Pages.WidgetsViewModel>()?.StartEditingCommand.Execute(null);

    /// <summary>The same path the Settings page takes, so there is one update flow and not two.</summary>
    private async void InstallUpdate()
    {
        try
        {
            await _updateService.RunUserInitiatedUpdateAsync();
        }
        catch (Exception ex)
        {
            await Dialogs.Tell("Update failed", ex.Message);
        }
    }

    /// <summary>
    /// Ends Aquila. Through the main window when it has been built, so it saves where it was, as exiting always
    /// did; a window never opened has nothing to save, and the application is simply shut down.
    /// </summary>
    private void Exit()
    {
        if (_mainWindow.Window is { } window) window.CloseForExit();
        else Application.Current.Shutdown();
    }

    /// <summary>Extracts the icon embedded in our own exe (the &lt;ApplicationIcon&gt;), which is always present
    /// — copying a loose .ico into the output is unreliable under Velopack.</summary>
    private static System.Drawing.Icon AppIcon()
    {
        try
        {
            var exePath = Environment.ProcessPath ?? System.Reflection.Assembly.GetEntryAssembly()!.Location;
            return System.Drawing.Icon.ExtractAssociatedIcon(exePath) ?? System.Drawing.SystemIcons.Application;
        }
        catch
        {
            return System.Drawing.SystemIcons.Application;
        }
    }

    /// <summary>Draws a dot on the app icon. Generated once and cached: Icon.FromHandle wraps a native HICON
    /// that the Icon does not own, so rebuilding it per call would leak a GDI handle each time.</summary>
    private static System.Drawing.Icon BuildBadgedIcon(System.Drawing.Icon baseIcon)
    {
        using var bitmap = new System.Drawing.Bitmap(baseIcon.Width, baseIcon.Height,
            System.Drawing.Imaging.PixelFormat.Format32bppArgb);

        using (var g = System.Drawing.Graphics.FromImage(bitmap))
        {
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.DrawIcon(baseIcon, new System.Drawing.Rectangle(0, 0, bitmap.Width, bitmap.Height));

            // Bottom-right, with a light ring so it stays legible against a dark taskbar or a dark icon.
            var size = Math.Max(6f, bitmap.Width / 2.4f);
            var dot = new System.Drawing.RectangleF(bitmap.Width - size, bitmap.Height - size, size, size);

            using var ring = new System.Drawing.SolidBrush(System.Drawing.Color.White);
            using var fill = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(0x2E, 0x9E, 0x4F));
            g.FillEllipse(ring, System.Drawing.RectangleF.Inflate(dot, 1.5f, 1.5f));
            g.FillEllipse(fill, dot);
        }

        var handle = bitmap.GetHicon();
        return System.Drawing.Icon.FromHandle(handle);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _updateService.StatusChanged -= RefreshUpdateBadge;
        _icon.Visible = false;
        _icon.Dispose();
    }
}
