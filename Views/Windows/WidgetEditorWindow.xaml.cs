using System.Windows;
using Aquila.Models;
using Aquila.ViewModels.Windows;

namespace Aquila.Views.Windows;

/// <summary>
/// Add/edit dialog for a desktop widget. One dialog for all three entry points — new, new with a sensor
/// already picked (from the Explorer), and editing an existing widget — since they differ only in what
/// arrives pre-filled.
/// </summary>
public partial class WidgetEditorWindow : Window
{
    private readonly DesktopWidgetDefinition? _existing;

    public WidgetEditorViewModel ViewModel { get; }

    /// <summary>The result, once the dialog returns true.</summary>
    public DesktopWidgetDefinition? Result { get; private set; }

    public WidgetEditorWindow(HardwareNode hardware, DesktopWidgetDefinition? existing = null,
        string? presetSensorIdentifier = null)
    {
        _existing = existing;
        ViewModel = new WidgetEditorViewModel();

        InitializeComponent();
        DataContext = this;

        ViewModel.Load(hardware, existing, presetSensorIdentifier);

        Title = ViewModel.IsEditing ? "Edit widget" : "Add widget";
        HeaderText.Text = Title;
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (!ViewModel.CanSave) return;

        Result = ViewModel.ToDefinition(_existing);
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
