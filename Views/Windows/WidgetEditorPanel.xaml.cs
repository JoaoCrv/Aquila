using System.Windows;
using System.Windows.Input;
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
            SubtitleText.Text = "Click a widget on the desktop to edit it.";
            return;
        }

        ViewModel.Load(_hardware, definition, presetSensorIdentifier: null);
        SubtitleText.Text = "Changes land on the desktop as you make them.";
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
}
