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
    private readonly HardwareNode _hardware;

    /// <summary>Raised when the panel wants a widget created. The panel does not own the widget list —
    /// the domain service does, and it answers by selecting whatever it created.</summary>
    public event Action? AddRequested;

    /// <summary>Raised when the user removes the widget being edited.</summary>
    public event Action<DesktopWidgetDefinition>? RemoveRequested;

    public WidgetEditorViewModel ViewModel { get; }

    public WidgetEditorPanel(HardwareNode hardware, Aquila.Services.PresetService presets)
    {
        _hardware = hardware;
        ViewModel = new WidgetEditorViewModel(presets);

        InitializeComponent();
        DataContext = this;
    }

    /// <summary>Points the panel at a definition, or at nothing. Called every time the selection on the
    /// desktop changes, which is why the view model reloads in place rather than being replaced — the
    /// Changed subscription the host set up has to survive.</summary>
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
        ViewModel.Load(_hardware, definition, presetSensorIdentifier: null);
    }

    /// <summary>The definition currently shown, so the host knows what Remove refers to.</summary>
    public DesktopWidgetDefinition? Target { get; private set; }

    /// <summary>
    /// Docks the panel to the right edge of a screen.
    ///
    /// Bounds arrive in physical pixels — that is what Screen reports — while Left/Top are DIPs, so they
    /// have to be converted or the panel lands off the edge of a scaled display. Call this after Show:
    /// the transform comes from the window's own presentation source, which does not exist before then.
    /// </summary>
    public void DockRight(System.Drawing.Rectangle screenBounds)
    {
        const double inset = 16;

        var fromDevice = PresentationSource.FromVisual(this)?.CompositionTarget?.TransformFromDevice
            ?? System.Windows.Media.Matrix.Identity;

        var topLeft = fromDevice.Transform(new Point(screenBounds.Left, screenBounds.Top));
        var bottomRight = fromDevice.Transform(new Point(screenBounds.Right, screenBounds.Bottom));

        Height = Math.Max(320, bottomRight.Y - topLeft.Y - inset * 2);
        Left = bottomRight.X - Width - inset;
        Top = topLeft.Y + inset;
    }

    private void Header_Drag(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed) DragMove();
    }

    private void Add_Click(object sender, RoutedEventArgs e) => AddRequested?.Invoke();

    private void Remove_Click(object sender, RoutedEventArgs e)
    {
        if (Target is { } target) RemoveRequested?.Invoke(target);
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

    private const string PresetFilter = "Aquila preset (*.json)|*.json|All files (*.*)|*.*";

    /// <summary>
    /// Writes the preset on screen to a file.
    ///
    /// Offered under its id rather than its name, because that is what the file is called in the presets
    /// folder — so a preset exported and then dropped back into that folder lands where it would have been
    /// anyway, instead of arriving as a second copy under a different stem.
    /// </summary>
    private void OnExportPreset(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Title = "Export preset",
            Filter = PresetFilter,
            FileName = ViewModel.ExportFileName,
            DefaultExt = ".json",
            AddExtension = true,
        };

        if (dialog.ShowDialog(this) != true) return;

        try
        {
            ViewModel.Export(dialog.FileName);
        }
        catch (Exception ex)
        {
            // The only failures left here are the file system's — a folder that vanished, a name already
            // held open. Reported with the reason, because "export failed" alone tells you nothing you
            // could act on.
            MessageBox.Show(this, $"The preset could not be written. {ex.Message}",
                "Export preset", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    /// <summary>
    /// Reads a preset in from a file.
    ///
    /// Nothing is overwritten: the service gives an arriving preset a free id when the one it carries is
    /// taken. So the only outcome worth reporting is a file that is not a preset at all.
    /// </summary>
    private void OnImportPreset(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Import preset",
            Filter = PresetFilter,
            Multiselect = false,
            CheckFileExists = true,
        };

        if (dialog.ShowDialog(this) != true) return;

        if (!ViewModel.Import(dialog.FileName))
            MessageBox.Show(this, "That file could not be read as an Aquila preset.",
                "Import preset", MessageBoxButton.OK, MessageBoxImage.Warning);
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

    /// <summary>Renaming is the name box, so the menu entry goes there rather than opening a dialog that
    /// would ask for the same thing in a second place.</summary>
    private void OnRenamePreset(object sender, RoutedEventArgs e)
    {
        PresetNameBox.Focus();
        PresetNameBox.SelectAll();
    }

    /// <summary>
    /// Removes the preset on the row the pointer is over.
    ///
    /// Handled on the PREVIEW of the button press, not on its Click. A ComboBoxItem selects on mouse-down,
    /// so by the time a Click arrived the list would already have chosen the very preset being removed —
    /// and the open widget would be wearing it. Marking the press handled stops it reaching the row at all,
    /// which is also why the work happens here rather than in a command binding.
    /// </summary>
    private void OnDeletePreset(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: Preset preset })
            ViewModel.DeletePresetCommand.Execute(preset);

        e.Handled = true;
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
