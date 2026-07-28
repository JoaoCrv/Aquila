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
        LabelEachScreen = _settings.Current.DesktopSurfaceShowScreenInfo;
        _initialized = true;

        // The floating toolbar is the only way out of arrange mode once the window is minimized.
        _surface.OrganizeFinished += StopArranging;
    }

    public HardwareNode Hardware => _aquila.State.Hardware;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanArrange))]
    private bool _showOnDesktop;

    [ObservableProperty]
    private bool _labelEachScreen;

    [ObservableProperty]
    private bool _isArranging;

    public bool CanArrange => ShowOnDesktop;

    partial void OnShowOnDesktopChanged(bool value)
    {
        if (!_initialized) return;

        _settings.Current.DesktopSurfaceEnabled = value;
        _settings.Save();

        if (value)
        {
            _surface.Show(showScreenInfo: LabelEachScreen);
            _widgets.Populate();
        }
        else
        {
            if (IsArranging) StopArranging();
            _surface.Hide();
        }
    }

    partial void OnLabelEachScreenChanged(bool value)
    {
        if (!_initialized) return;

        _settings.Current.DesktopSurfaceShowScreenInfo = value;
        _settings.Save();
        _surface.SetScreenInfoVisible(value);
    }

    /// <summary>
    /// Enters arrange mode and gets the app out of the way — you can't arrange a desktop you can't see.
    /// The floating toolbar (owned by the surface service) is what brings it back, so minimizing can't
    /// strand the user.
    /// </summary>
    [RelayCommand]
    private void StartArranging()
    {
        if (!ShowOnDesktop || IsArranging) return;

        IsArranging = true;
        _surface.SetOrganizing(true);

        if (FindMainWindow() is { } window)
            window.WindowState = WindowState.Minimized;
    }

    private void StopArranging()
    {
        if (!IsArranging) return;

        IsArranging = false;
        _surface.SetOrganizing(false);

        if (FindMainWindow() is { } window)
        {
            window.WindowState = WindowState.Normal;
            window.Activate();
        }
    }

    private static Window? FindMainWindow() =>
        Application.Current?.Windows.OfType<MainWindow>().FirstOrDefault();
}
