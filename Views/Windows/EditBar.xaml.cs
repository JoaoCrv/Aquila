using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Threading;
using Aquila.ViewModels.Windows;

namespace Aquila.Views.Windows;

/// <summary>
/// The edit bar, centred at the top of the primary screen for the length of an edit session. Its menus are
/// popups, so they stand above everything as the bar does.
/// </summary>
public partial class EditBar : Wpf.Ui.Controls.FluentWindow
{
    private double _centre = double.NaN;
    private Rect _clearOf = Rect.Empty;

    // Which menu closed last, and when: pressing a menu's own button while it is open closes it on the press —
    // the popup lets go of everything outside it — and the click that follows would open it again.
    private Popup? _closed;
    private DateTime _closedAt;

    public EditBarViewModel ViewModel { get; }

    public EditBar(EditBarViewModel viewModel)
    {
        ViewModel = viewModel;

        InitializeComponent();
        DataContext = this;

        Tip.CustomPopupPlacementCallback = (popup, target, _) =>
            [new CustomPopupPlacement(new Point((target.Width - popup.Width) / 2, target.Height + 22), PopupPrimaryAxis.None)];

        ViewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(EditBarViewModel.ShowsTip) && !ViewModel.ShowsTip) Tip.IsOpen = false;
        };
        ContentRendered += (_, _) => Tip.IsOpen = ViewModel.ShowsTip;

        AddPicker.Chosen += reading =>
        {
            AddMenu.IsOpen = false;
            ViewModel.Create(reading);
        };
        AddPicker.Cancelled += () => AddMenu.IsOpen = false;
    }

    /// <summary>Opens the Add menu — from its button, or from the Widgets page's, which opens edit mode on the way:
    /// the bar may not be on screen yet then, and a popup has to wait for the thing it stands under.</summary>
    public void OpenAddMenu()
    {
        if (!IsLoaded)
        {
            ContentRendered += OpenOnce;
            return;
        }

        Activate();
        AddPicker.Open(ViewModel.Readings(), [], ViewModel.Starts, ViewModel.LabelOf);
        AddMenu.IsOpen = true;

        void OpenOnce(object? sender, EventArgs e)
        {
            ContentRendered -= OpenOnce;
            OpenAddMenu();
        }
    }

    private void OnUnreadTile(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: Aquila.Services.WidgetKindInfo kind }) return;

        AddMenu.IsOpen = false;
        ViewModel.Create(kind);
    }

    /// <summary>
    /// Centred on the primary screen's working area, 24 DIPs from its top — where the bar before it stood. The area
    /// arrives in physical pixels and Left and Top are DIPs, so it is converted with this window's own transform,
    /// which exists from here on.
    /// </summary>
    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        var screen = System.Windows.Forms.Screen.PrimaryScreen;
        var fromDevice = PresentationSource.FromVisual(this)?.CompositionTarget?.TransformFromDevice;
        if (screen is null || fromDevice is not { } transform) return;

        var area = screen.WorkingArea;
        var topLeft = transform.Transform(new Point(area.Left, area.Top));
        var bottomRight = transform.Transform(new Point(area.Right, area.Bottom));

        UpdateLayout();
        _centre = (topLeft.X + bottomRight.X) / 2;
        Top = topLeft.Y + 24;
        Recentre();
    }

    /// <summary>Kept centred as it changes width — a count with another digit, the status saying more — and the
    /// tip moved with it: a popup does not follow its window.</summary>
    protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
    {
        base.OnRenderSizeChanged(sizeInfo);
        Recentre();
    }

    /// <summary>The panel's place, in DIPs, or empty while it is hidden. On a narrow screen a centred bar runs under a
    /// docked panel, so it slides off to the panel's free side and stands 16 DIPs clear of it.</summary>
    public void KeepClearOf(Rect panel)
    {
        _clearOf = panel;
        Recentre();
    }

    private void Recentre()
    {
        if (double.IsNaN(_centre)) return;

        var left = _centre - ActualWidth / 2;
        if (!_clearOf.IsEmpty && _clearOf.IntersectsWith(new Rect(left, Top, ActualWidth, ActualHeight)))
            left = _clearOf.Left + _clearOf.Width / 2 > _centre ? _clearOf.Left - 16 - ActualWidth : _clearOf.Right + 16;

        Left = left;
        if (Tip.IsOpen)
        {
            var offset = Tip.HorizontalOffset;
            Tip.HorizontalOffset = offset + 1;
            Tip.HorizontalOffset = offset;
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        AddMenu.IsOpen = false;
        WidgetsMenu.IsOpen = false;
        PreviewMenu.IsOpen = false;
        Tip.IsOpen = false;
        base.OnClosed(e);
    }

    private void Add_Click(object sender, RoutedEventArgs e)
    {
        if (!JustClosed(AddMenu)) OpenAddMenu();
    }

    private void Undo_Click(object sender, RoutedEventArgs e) => ViewModel.Undo();

    private void Redo_Click(object sender, RoutedEventArgs e) => ViewModel.Redo();

    /// <summary>The undo keys here too: with nothing selected the panel is gone, and the bar is what has the keys.
    /// After the focused control, so the search boxes keep their own.</summary>
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled) return;

        if (e.Key == Key.Z && Keyboard.Modifiers == ModifierKeys.Control) ViewModel.Undo();
        else if ((e.Key == Key.Y && Keyboard.Modifiers == ModifierKeys.Control)
                 || (e.Key == Key.Z && Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift))) ViewModel.Redo();
        else return;
        e.Handled = true;
    }

    private void Discard_Click(object sender, RoutedEventArgs e) => ViewModel.Finish(save: false);

    private void Save_Click(object sender, RoutedEventArgs e) => ViewModel.Finish(save: true);

    private void CloseTip_Click(object sender, RoutedEventArgs e) => ViewModel.TipSeen();

    private bool JustClosed(Popup menu) =>
        ReferenceEquals(_closed, menu) && DateTime.UtcNow - _closedAt < TimeSpan.FromMilliseconds(300);

    private void Menu_Closed(object? sender, EventArgs e)
    {
        _closed = sender as Popup;
        _closedAt = DateTime.UtcNow;
        ViewModel.Highlight(null);
    }

    /// <summary>Filled as it opens, from a blank search: widgets move, change and get covered while it is shut.
    /// The search box takes the keys, so typing finds a widget at once.</summary>
    private void Widgets_Click(object sender, RoutedEventArgs e)
    {
        if (!JustClosed(WidgetsMenu)) OpenWidgetsMenu();
    }

    /// <summary>Also asked from outside: the panel's "Widgets" goes up to this list — from another window, which is
    /// why the bar takes the keys first, for the search box.</summary>
    public void OpenWidgetsMenu()
    {
        Activate();
        ViewModel.Search = string.Empty;
        ViewModel.FillGroups();
        WidgetsMenu.IsOpen = true;
        Dispatcher.BeginInvoke(() => SearchBox.Focus(), DispatcherPriority.Input);
    }

    private void Preview_Click(object sender, RoutedEventArgs e)
    {
        if (!JustClosed(PreviewMenu)) PreviewMenu.IsOpen = true;
    }

    private void PreviewChoice_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement choice) ViewModel.PreviewRole = choice.Tag as string;
        PreviewMenu.IsOpen = false;
    }

    private void Row_MouseEnter(object sender, MouseEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: WidgetRow row }) ViewModel.Highlight(row);
    }

    private void Row_MouseLeave(object sender, MouseEventArgs e) => ViewModel.Highlight(null);

    /// <summary>Selects the widget — one that draws nothing included, which is why the list exists — after the menu
    /// has gone, so the panel opens on a clear desktop.</summary>
    private void Row_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: WidgetRow row }) return;

        WidgetsMenu.IsOpen = false;
        ViewModel.Select(row);
    }
}
