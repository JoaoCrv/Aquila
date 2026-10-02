using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using Wpf.Ui.Controls;
using WpfControls = System.Windows.Controls;

namespace Aquila.Views.Windows;

/// <summary>
/// The tray icon's menu, drawn by WPF instead of WinForms.
///
/// It was a WinForms ContextMenuStrip, which paints itself with its own engine and reads none of our
/// resources — so it was the one surface in the application that could never wear the theme. A WPF
/// ContextMenu is styled by WPF-UI like every other menu here, which means it follows the palette, Aquila
/// and Fluent, light and dark, BY CONSTRUCTION: there is no second copy of the theme to keep in step, and
/// every kit family claimed in Themes/Aquila applies to it untouched. The icon, its update badge and the
/// balloon notifications stay WinForms; only the menu moved.
///
/// Built here in code rather than in XAML so the icons are SymbolRegular values the COMPILER checks. A
/// misspelt icon name in XAML compiles cleanly and throws the first time the menu is drawn.
/// </summary>
internal sealed class TrayMenu
{
    private readonly WpfControls.ContextMenu _menu = new();
    private readonly WpfControls.MenuItem _update;
    private readonly WpfControls.Separator _updateRule;

    /// <summary>
    /// A one-pixel transparent window that exists only to be in the foreground while the menu is open.
    ///
    /// The known catch with a WPF menu opened from the tray: it closes on a click elsewhere only if a window
    /// of THIS process is the foreground one, because that is what lets it see the click at all. Aquila
    /// spends most of its life with no window shown, so it has nothing to put in front — and without this
    /// the menu would sit on the screen until an item was chosen. Native tray menus pull the same trick
    /// with their own hidden message window; this is that, made visible to WPF.
    /// </summary>
    private Window? _anchor;

    public TrayMenu(Action open, Action editWidgets, Action installUpdate, Action exit)
    {
        _menu.Items.Add(Item("Open Aquila", SymbolRegular.Home24, open));

        // The tray is where someone running Aquila hidden actually lives, and arranging widgets is the one
        // thing they would otherwise have to open the whole window to reach — only to have it minimise
        // itself again a second later, because you cannot edit a desktop you cannot see.
        _menu.Items.Add(Item("Edit widgets on the desktop", SymbolRegular.Edit24, editWidgets));

        // Hidden until there is one. An entry that is present but does nothing teaches people to ignore
        // the menu, and the badge on the icon already says when to look. The rule above it travels with
        // it: two rules with nothing between them is a gap that reads as a missing entry.
        _updateRule = new WpfControls.Separator { Visibility = Visibility.Collapsed };
        _update = Item("Update available — install…", SymbolRegular.ArrowDownload24, installUpdate);
        _update.Visibility = Visibility.Collapsed;
        _menu.Items.Add(_updateRule);
        _menu.Items.Add(_update);

        _menu.Items.Add(new WpfControls.Separator());
        _menu.Items.Add(Item("Exit", SymbolRegular.Power24, exit));

        _menu.Closed += (_, _) => _anchor?.Hide();
    }

    public void ShowUpdate(bool available)
    {
        var visibility = available ? Visibility.Visible : Visibility.Collapsed;
        _update.Visibility = visibility;
        _updateRule.Visibility = visibility;
    }

    /// <summary>Opens the menu at the pointer, after putting a window of ours in front so that a click
    /// anywhere else closes it.</summary>
    public void Open()
    {
        _anchor ??= CreateAnchor();
        _anchor.Show();
        _anchor.Activate();
        SetForegroundWindow(new WindowInteropHelper(_anchor).Handle);

        _menu.Placement = WpfControls.Primitives.PlacementMode.MousePoint;
        _menu.IsOpen = true;
    }

    /// <summary>
    /// One entry. Its action runs AFTER the menu has closed and the anchor has gone, not inside the click.
    ///
    /// Inside the click the anchor is still the active visible window, so anything the action opened would
    /// take it as an owner — the update question would be owned by a one-pixel window that is about to be
    /// hidden, and could go with it.
    /// </summary>
    private WpfControls.MenuItem Item(string header, SymbolRegular icon, Action action)
    {
        var item = new WpfControls.MenuItem
        {
            Header = header,
            Icon = new SymbolIcon { Symbol = icon },
        };

        item.Click += (_, _) =>
        {
            _menu.IsOpen = false;
            _anchor?.Hide();
            _menu.Dispatcher.BeginInvoke(action);
        };

        return item;
    }

    private static Window CreateAnchor() => new()
    {
        WindowStyle = WindowStyle.None,
        AllowsTransparency = true,
        Background = Brushes.Transparent,
        ShowInTaskbar = false,
        ShowActivated = true,
        Topmost = true,
        ResizeMode = ResizeMode.NoResize,
        Width = 1,
        Height = 1,
        Left = SystemParameters.VirtualScreenLeft,
        Top = SystemParameters.VirtualScreenTop,
    };

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(IntPtr hWnd);
}
