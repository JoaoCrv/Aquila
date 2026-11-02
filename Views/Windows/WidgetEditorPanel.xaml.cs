using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Aquila.Models;
using Aquila.ViewModels.Windows;

namespace Aquila.Views.Windows;

/// <summary>
/// The floating properties panel shown while desktop edit mode is on.
///
/// It replaced a modal dialog. Edit mode exists so the widgets can be seen on the real desktop, and a
/// window that had to be dismissed before any of them could be touched worked against that: you could not
/// select a second widget without closing the editor for the first.
///
/// Long-lived and non-modal, so it is told which definition to show rather than being created per edit.
/// </summary>
public partial class WidgetEditorPanel : Wpf.Ui.Controls.FluentWindow
{
    /// <summary>The whole state rather than the hardware alone: a List is offered the lists, which are not
    /// hardware.</summary>
    private readonly AquilaState _state;

    /// <summary>Raised when the user removes the widget being edited.</summary>
    public event Action<DesktopWidgetDefinition>? RemoveRequested;

    /// <summary>Raised for Ctrl+D or the Duplicate button. The panel does not own the widget list — the domain
    /// service does, and it answers by selecting the copy.</summary>
    public event Action? DuplicateRequested;

    /// <summary>Raised for the arrow keys, in DIPs: 1, or 10 with Shift.</summary>
    public event Action<double, double>? NudgeRequested;

    /// <summary>Raised for Esc or the close button: letting go of the selection, which is what hides the panel.
    /// </summary>
    public event Action? CloseRequested;

    public WidgetEditorViewModel ViewModel { get; }

    public WidgetEditorPanel(AquilaState state, Aquila.Services.PresetService presets)
    {
        _state = state;
        ViewModel = new WidgetEditorViewModel(presets);

        InitializeComponent();
        DataContext = this;

        ReadingPicker.Chosen += OnReadingChosen;
        ReadingPicker.Cancelled += () => ReadingPopup.IsOpen = false;
        ReadingPopup.Closed += (_, _) => _readingClosedAt = DateTime.UtcNow;

        ViewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(WidgetEditorViewModel.IsGalleryOpen) && ViewModel.IsGalleryOpen)
                Dispatcher.BeginInvoke(FocusCurrentKind, DispatcherPriority.Loaded);
        };
    }

    private DateTime _readingClosedAt;

    /// <summary>Opens the sensors to choose a reading from, under the button, leaving out what the widget reads
    /// already. Pressing the button while they are open closes them on the press, and the click that follows must
    /// not open them again.</summary>
    private void OnReadingButton(object sender, RoutedEventArgs e)
    {
        if (DateTime.UtcNow - _readingClosedAt < TimeSpan.FromMilliseconds(300)) return;

        ReadingPicker.Open(ViewModel.SensorOptions, ViewModel.ChosenIdentifiers);
        ReadingPopup.IsOpen = true;
    }

    private void OnReadingChosen(SensorOption option)
    {
        ReadingPopup.IsOpen = false;
        ViewModel.AddReading(option);
    }

    /// <summary>The gallery opens on the current kind, with the keys there, so the arrows start from it.</summary>
    private void FocusCurrentKind()
    {
        foreach (var tile in Descendants<Controls.MenuToggle>(Gallery))
            if (tile.DataContext is KindChoice { IsCurrent: true })
            {
                tile.Focus();
                return;
            }
    }

    /// <summary>The tile under the pointer or the keys is the one the card below the gallery describes. Two handlers,
    /// each of its event's exact type: inside a template the event is bound by name, which takes no looser signature —
    /// one shared handler threw as the gallery opened.</summary>
    private void OnKindTileHovered(object sender, MouseEventArgs e) => Describe(sender);

    private void OnKindTileFocused(object sender, KeyboardFocusChangedEventArgs e) => Describe(sender);

    private void Describe(object tile)
    {
        if (tile is FrameworkElement { DataContext: KindChoice choice }) ViewModel.FocusedKind = choice.Kind;
    }

    /// <summary>Points the panel at a definition, or at nothing — the latter only as it is hidden, since the panel
    /// is shown for a selection alone. Called every time the selection changes, which is why the view model reloads
    /// in place rather than being replaced: the Changed subscription the host set up has to survive.</summary>
    public void Edit(DesktopWidgetDefinition? definition)
    {
        Target = definition;

        if (definition is null)
        {
            ViewModel.Clear();
            return;
        }

        // The subtitle is bound now: it has to say which of the two views you are in, and only the view
        // model knows that.
        ViewModel.Load(_state, definition, presetSensorIdentifier: null);
    }

    /// <summary>The definition currently shown, so the host knows what Remove refers to.</summary>
    public DesktopWidgetDefinition? Target { get; private set; }

    /// <summary>Raised when the panel has been dragged somewhere by hand, with where it now stands.</summary>
    public event Action<Point>? Moved;

    /// <summary>Raised for a double-click on the header: dock again, and forget where it was dragged to.</summary>
    public event Action? DockRequested;

    /// <summary>Raised for "Widgets" in the header's path: up a level — let go of the widget and show the list of
    /// them, which is the edit bar's.</summary>
    public event Action? WidgetsRequested;

    private void OnWidgetsCrumb(object sender, RoutedEventArgs e) => WidgetsRequested?.Invoke();

    /// <summary>
    /// A screen's area in DIPs, from the physical pixels Screen reports — Left/Top are DIPs, so a rectangle left
    /// unconverted lands off the edge of a scaled display. Asked once the window exists (Show, or EnsureHandle while
    /// it is still hidden): the transform comes from its own presentation source, which does not exist before then.
    /// </summary>
    public Rect ToDips(System.Drawing.Rectangle pixels)
    {
        var fromDevice = PresentationSource.FromVisual(this)?.CompositionTarget?.TransformFromDevice
            ?? System.Windows.Media.Matrix.Identity;

        return new Rect(fromDevice.Transform(new Point(pixels.Left, pixels.Top)),
            fromDevice.Transform(new Point(pixels.Right, pixels.Bottom)));
    }

    /// <summary>Stands the panel where the placement rule chose — see <see cref="Services.EditMode.PanelPlacement"/>.
    /// </summary>
    public void Place(Rect at)
    {
        Height = Math.Max(320, at.Height);
        Left = at.Left;
        Top = at.Top;
    }

    /// <summary>The whole header is the panel's handle. DragMove returns when the button is let go, so the new place
    /// is known here; a click that did not move it is not a drag.</summary>
    private void Header_Drag(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState != MouseButtonState.Pressed) return;

        // A step of the path is a link, not a handle: a drag started on it would swallow its click.
        for (var at = e.OriginalSource as DependencyObject; at is not null; at = LogicalTreeHelper.GetParent(at))
            if (at is System.Windows.Documents.Hyperlink) return;

        if (e.ClickCount == 2)
        {
            DockRequested?.Invoke();
            return;
        }

        var before = new Point(Left, Top);
        DragMove();
        if (new Point(Left, Top) != before) Moved?.Invoke(new Point(Left, Top));
    }

    private void Duplicate_Click(object sender, RoutedEventArgs e) => DuplicateRequested?.Invoke();

    private void Remove_Click(object sender, RoutedEventArgs e)
    {
        if (Target is { } target) RemoveRequested?.Invoke(target);
    }

    private void Close_Click(object sender, RoutedEventArgs e) => CloseRequested?.Invoke();

    /// <summary>Brings the panel forward with the keyboard on the window itself, not on whichever field had it
    /// last: a widget just reached for on the desktop is what the arrows should move, not a slider left focused.
    /// </summary>
    public void TakeKeys()
    {
        Activate();
        Focus();
    }

    /// <summary>
    /// The arrows move the widget, read before anything else sees them: the panel's ScrollViewer takes them to
    /// scroll, and the window would never hear them otherwise. Except where the focused control uses them itself —
    /// a caret in a text field, a slider, a list, a combo, the type gallery's tiles — which the user is plainly
    /// working in.
    /// </summary>
    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        base.OnPreviewKeyDown(e);
        if (e.Handled || Target is null || OwnsArrows(Keyboard.FocusedElement as DependencyObject)) return;

        var step = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) ? 10 : 1;
        (double dx, double dy)? by = e.Key switch
        {
            Key.Left => (-step, 0),
            Key.Right => (step, 0),
            Key.Up => (0, -step),
            Key.Down => (0, step),
            _ => null,
        };
        if (by is not { } move) return;

        NudgeRequested?.Invoke(move.dx, move.dy);
        e.Handled = true;
    }

    /// <summary>Whether the focused control, or the item or field it sits in, moves with the arrows. Named one by
    /// one rather than as Selector: a TabControl is a Selector too, and everything in a tab would then keep them.
    /// </summary>
    private static bool OwnsArrows(DependencyObject? focused)
    {
        for (var at = focused; at is not null and not Window;
             at = (at is Visual ? VisualTreeHelper.GetParent(at) : null) ?? LogicalTreeHelper.GetParent(at))
        {
            if (at is System.Windows.Controls.Primitives.TextBoxBase or PasswordBox
                or System.Windows.Controls.Primitives.RangeBase or ComboBox or ListBox or ListBoxItem
                or TabItem or MenuItem or Controls.MenuToggle)
                return true;
        }
        return false;
    }

    /// <summary>
    /// Del, Esc and Ctrl+D, heard after the focused control has had them — so a text field keeps Delete, and an open
    /// combo closes on Esc before the panel does. Esc steps back one level: out of the preset into the widget, then
    /// out of the widget, which lets go of it.
    /// </summary>
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled || Target is null) return;

        switch (e.Key)
        {
            case Key.Delete:
                RemoveRequested?.Invoke(Target);
                break;
            case Key.Escape when ViewModel.IsGalleryOpen:
                ViewModel.IsGalleryOpen = false;
                break;
            case Key.Escape when ViewModel.EditingPreset:
                ViewModel.EditingPreset = false;
                break;
            case Key.Escape:
                CloseRequested?.Invoke();
                break;
            case Key.D when Keyboard.Modifiers == ModifierKeys.Control:
                DuplicateRequested?.Invoke();
                break;
            default:
                return;
        }
        e.Handled = true;
    }

    /// <summary>
    /// Opens the preset menu under its button.
    ///
    /// A ContextMenu lives in a visual tree of its own, so it inherits no DataContext and every binding
    /// inside it would silently find nothing. Handed the window's here, once, at the moment it opens.
    /// </summary>
    private void OnPresetMenu(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { ContextMenu: { } menu } button) return;

        menu.PlacementTarget = button;
        menu.DataContext = DataContext;
        menu.IsOpen = true;
    }

    /// <summary>Opens a preset row's menu with the ROW as its data context, so Export and Delete act on that preset —
    /// not on the one worn, which is what the row's menu exists to spare: tidying one away without wearing it.</summary>
    private void OnPresetRowMenu(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { ContextMenu: { } menu } button) return;

        menu.PlacementTarget = button;
        menu.DataContext = button.DataContext;
        menu.IsOpen = true;
    }

    /// <summary>Pointing at a screen's name in the picker lights up that monitor; closing the picker puts it out.
    /// </summary>
    private void OnScreenPointed(object sender, MouseEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: ScreenChoice screen }) ViewModel.IdentifyScreen?.Invoke(screen.Key);
    }

    private void OnScreenPickerClosed(object? sender, EventArgs e) => ViewModel.IdentifyScreen?.Invoke(null);

    private void OnDeleteRowPreset(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: PresetRow row }) ViewModel.DeletePresetCommand.Execute(row.Preset);
    }

    private const string PresetFilter = "Aquila preset (*.json)|*.json|All files (*.*)|*.*";

    /// <summary>
    /// Writes a preset to a file: a row's, from its own menu, or the one on screen, from the preset view's.
    ///
    /// Offered under its id rather than its name, because that is what the file is called in the presets
    /// folder — so a preset exported and then dropped back into that folder lands where it would have been
    /// anyway, instead of arriving as a second copy under a different stem.
    /// </summary>
    private async void OnExportPreset(object sender, RoutedEventArgs e)
    {
        var preset = (sender as FrameworkElement)?.DataContext is PresetRow row ? row.Preset : ViewModel.SelectedPreset;
        if (preset is null) return;

        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Title = "Export preset",
            Filter = PresetFilter,
            FileName = WidgetEditorViewModel.ExportFileName(preset),
            DefaultExt = ".json",
            AddExtension = true,
        };

        if (dialog.ShowDialog(this) != true) return;

        try
        {
            ViewModel.Export(preset, dialog.FileName);
        }
        catch (Exception ex)
        {
            // The only failures left here are the file system's — a folder that vanished, a name already
            // held open. Reported with the reason, because "export failed" alone tells you nothing you
            // could act on.
            await Aquila.Services.Dialogs.Tell(
                "Export preset", $"The preset could not be written. {ex.Message}", this);
        }
    }

    /// <summary>
    /// Reads a preset in from a file.
    ///
    /// Nothing is overwritten: the service gives an arriving preset a free id when the one it carries is
    /// taken. So the only outcome worth reporting is a file that is not a preset at all.
    /// </summary>
    private async void OnImportPreset(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Import preset",
            Filter = PresetFilter,
            Multiselect = false,
            CheckFileExists = true,
        };

        if (dialog.ShowDialog(this) != true) return;

        if (!ViewModel.Import(dialog.FileName, out var newer))
            await Aquila.Services.Dialogs.Tell(
                "Import preset", "That file could not be read as an Aquila preset.", this);

        // Said now or never: once read, the preset holds only what this version understood, and nothing
        // about it records that more was there.
        else if (newer)
            await Aquila.Services.Dialogs.Tell(
                "Import preset",
                "This preset was made by a newer version of Aquila. It has been imported as far as this "
                + "version understands it; anything newer was left out, and would be left out of the file "
                + "too if it is saved from here.",
                this);
    }

    /// <summary>
    /// Opens one ramp row's menu.
    ///
    /// The menu takes the ROW's data context, not the window's. Every entry in it acts on that one ramp,
    /// and a menu holding the window would remove whichever ramp the view model happened to think was
    /// current — which is not a thing the view model even tracks.
    /// </summary>
    private void OnRampMenu(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { ContextMenu: { } menu } button) return;

        menu.PlacementTarget = button;
        menu.DataContext = button.DataContext;
        menu.IsOpen = true;
    }

    /// <summary>
    /// Puts the caret in the row's own name box, the way Rename does for a preset.
    ///
    /// The box is found by walking out to the row and back down, never by x:Name: every row is built from
    /// the same template, so a name would answer with whichever one happened to be registered last.
    /// </summary>
    private void OnRenameRamp(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem { Parent: ContextMenu { PlacementTarget: DependencyObject target } }) return;
        if (Descendant<TextBox>(Ancestor<DockPanel>(target)) is not { } box) return;

        box.Focus();
        box.SelectAll();
    }

    private static T? Ancestor<T>(DependencyObject? from) where T : DependencyObject
    {
        for (; from is not null; from = VisualTreeHelper.GetParent(from))
            if (from is T hit) return hit;

        return null;
    }

    private static IEnumerable<T> Descendants<T>(DependencyObject from) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(from); i++)
        {
            var child = VisualTreeHelper.GetChild(from, i);
            if (child is T hit) yield return hit;
            foreach (var deeper in Descendants<T>(child)) yield return deeper;
        }
    }

    private static T? Descendant<T>(DependencyObject? from) where T : DependencyObject
    {
        if (from is null) return null;

        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(from); i++)
        {
            var child = VisualTreeHelper.GetChild(from, i);
            if (child is T hit) return hit;
            if (Descendant<T>(child) is { } deeper) return deeper;
        }

        return null;
    }

    /// <summary>Renaming is the name box — the preset view's title — so the menu entry goes there, opening the view
    /// when it is reached from the Preset tab, rather than a dialog asking for the same thing in a second place.
    /// After the layout pass: the box is only just being shown.</summary>
    private void OnRenamePreset(object sender, RoutedEventArgs e)
    {
        ViewModel.EditPresetCommand.Execute(null);
        Dispatcher.BeginInvoke(() =>
        {
            PresetNameBox.Focus();
            PresetNameBox.SelectAll();
        }, DispatcherPriority.Loaded);
    }

    /// <summary>
    /// Opening one card closes the others.
    ///
    /// The panel is a narrow column with eight sections in it, and with several open at once the one being
    /// used spends its life below the fold. Handled on the parent, not per card — Expanded is a bubbling
    /// routed event, so a section added later joins in without anyone remembering to wire it.
    ///
    /// Closing every card is still allowed. An accordion that refuses to let go of the last one is a
    /// section you cannot get out of the way to see the widget underneath.
    /// </summary>
    private void OnCardExpanded(object sender, RoutedEventArgs e)
    {
        if (e.OriginalSource is not Expander opened || sender is not Panel cards) return;

        // Only for the set this panel actually owns. Expanded bubbles, so a section opening INSIDE one of
        // these cards reaches here too — and since it is not among the children, the loop below would find
        // no match to skip and close every card, the one containing it included.
        if (!cards.Children.Contains(opened)) return;

        foreach (var card in cards.Children.OfType<Expander>())
            if (!ReferenceEquals(card, opened))
                card.IsExpanded = false;

        // After the layout pass. The card has only just grown, so scrolling to it now would aim at the
        // bounds it had while it was still shut.
        Dispatcher.BeginInvoke(new Action(() => opened.BringIntoView()), DispatcherPriority.Loaded);
    }

    /// <summary>
    /// Enter keeps a typed name, Escape puts the old one back. Used by the preset's name box and by a
    /// ramp's — the behaviour was never about presets, only about a box you rename something in.
    ///
    /// The binding commits on lost focus, which is what makes clicking away keep the name too — Enter only
    /// brings that moment forward. Escape has to restore the target by hand, because a binding that has not
    /// written anything yet has nothing to undo.
    ///
    /// Focus is cleared either way: the box only looks editable while it is being edited, and one left
    /// outlined after Enter would suggest the name had not been taken.
    /// </summary>
    private void OnNameKey(object sender, KeyEventArgs e)
    {
        if (sender is not TextBox box) return;

        switch (e.Key)
        {
            case Key.Enter:
                box.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
                break;

            case Key.Escape:
                box.GetBindingExpression(TextBox.TextProperty)?.UpdateTarget();
                break;

            default:
                return;
        }

        Keyboard.ClearFocus();
        e.Handled = true;
    }
}
