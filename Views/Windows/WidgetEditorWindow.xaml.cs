using System.Windows;
using Aquila.Models;
using Aquila.ViewModels.Windows;

namespace Aquila.Views.Windows;

/// <summary>
/// Add/edit dialog for a desktop widget. One dialog for all three entry points — new, new with a sensor
/// already picked (from the Explorer), and editing an existing widget — since they differ only in what
/// arrives pre-filled.
///
/// It edits the live definition directly, so the widget on the desktop updates as you go; the host owns
/// undoing that if the dialog is cancelled.
/// </summary>
public partial class WidgetEditorWindow : Window
{
    public WidgetEditorViewModel ViewModel { get; }

    public WidgetEditorWindow(HardwareNode hardware, DesktopWidgetDefinition target, bool isNew,
        string? presetSensorIdentifier = null)
    {
        ViewModel = new WidgetEditorViewModel();

        InitializeComponent();
        DataContext = this;

        ViewModel.Load(hardware, target, isNew, presetSensorIdentifier);

        Title = isNew ? "Add widget" : "Edit widget";
        HeaderText.Text = Title;
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (!ViewModel.CanSave) return;
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
