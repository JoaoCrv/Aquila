using System.Windows;
using Aquila.Models;
using Aquila.Services;
using Aquila.Views.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Aquila.ViewModels.Pages;

/// <summary>
/// The Widgets page: a live preview of the single-sensor pieces (#23) — one example of each, reading real
/// sensors from the same AquilaService tick every other page uses — plus the controls for the desktop
/// surface (#24). Those controls live here rather than in Settings because this is where widgets are
/// managed; keeping them in one place also avoids two views owning the same state.
/// </summary>
public partial class WidgetsViewModel : ObservableObject
{
    private readonly AquilaService _aquila;
    private readonly SettingsService _settings;
    private readonly Desktop.DesktopSurfaceService _surface;
    private readonly DesktopWidgetService _widgets;
    private bool _initialized;

    public WidgetsViewModel(AquilaService aquila, SettingsService settings,
        Desktop.DesktopSurfaceService surface, DesktopWidgetService widgets)
    {
        _aquila = aquila;
        _settings = settings;
        _surface = surface;
        _widgets = widgets;

        ShowOnDesktop = _settings.Current.DesktopSurfaceEnabled;
        _initialized = true;

        // The floating toolbar is the only way out of edit mode once the window is minimized.
        _surface.EditingFinished += StopEditing;
    }

    public HardwareNode Hardware => _aquila.State.Hardware;

    [ObservableProperty]
    private bool _showOnDesktop;

    /// <summary>Desktop edit mode: drag, change, remove, and send widgets between screens. "Edit" rather
    /// than "arrange" because arranging describes only the dragging part — and the same word is used
    /// throughout the code (SetEditing, EditingFinished, EditModeAdorner) so the UI and the code can be
    /// searched for with the same term.</summary>
    [ObservableProperty]
    private bool _isEditingOnDesktop;

    partial void OnShowOnDesktopChanged(bool value)
    {
        if (!_initialized) return;

        _settings.Current.DesktopSurfaceEnabled = value;
        _settings.Save();

        if (value)
        {
            _surface.Show();
            _widgets.Populate();
        }
        else
        {
            if (IsEditingOnDesktop) StopEditing();
            _surface.Hide();
        }
    }

    /// <summary>Flashes a number on each monitor, like Windows' own Identify button — a momentary answer
    /// to "which screen is which", not a label left permanently on the desktop.</summary>
    [RelayCommand]
    private void IdentifyScreens() => _surface.IdentifyScreens();

    /// <summary>Opens the editor to create a widget. The same dialog handles editing (from the desktop
    /// context menu) and, later, arriving from the Explorer with a sensor already chosen.</summary>
    [RelayCommand]
    private void AddWidget()
    {
        var dialog = new Views.Windows.WidgetEditorWindow(Hardware) { Owner = FindMainWindow() };
        if (dialog.ShowDialog() != true || dialog.Result is null) return;

        // Adding a widget implies wanting to see it.
        if (!ShowOnDesktop) ShowOnDesktop = true;

        _widgets.Add(dialog.Result);
    }

    /// <summary>
    /// Enters desktop edit mode and gets the app out of the way — you can't edit a desktop you can't see.
    /// The floating toolbar (owned by the surface service) is what brings it back, so minimizing can't
    /// strand the user.
    /// </summary>
    [RelayCommand]
    private void StartEditing()
    {
        if (!ShowOnDesktop || IsEditingOnDesktop) return;

        IsEditingOnDesktop = true;
        _surface.SetEditing(true);

        if (FindMainWindow() is { } window)
            window.WindowState = WindowState.Minimized;
    }

    private void StopEditing()
    {
        if (!IsEditingOnDesktop) return;

        IsEditingOnDesktop = false;
        _surface.SetEditing(false);

        if (FindMainWindow() is { } window)
        {
            window.WindowState = WindowState.Normal;
            window.Activate();
        }
    }

    private static Window? FindMainWindow() =>
        Application.Current?.Windows.OfType<MainWindow>().FirstOrDefault();
}
