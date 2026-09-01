using System.Collections.ObjectModel;
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
    private readonly DesktopSurface.DesktopSurfaceService _surface;
    private readonly DesktopWidgetService _widgets;
    private bool _initialized;

    public WidgetsViewModel(AquilaService aquila, SettingsService settings,
        DesktopSurface.DesktopSurfaceService surface, DesktopWidgetService widgets)
    {
        _aquila = aquila;
        _settings = settings;
        _surface = surface;
        _widgets = widgets;

        ShowOnDesktop = _settings.Current.DesktopSurfaceEnabled;
        _initialized = true;

        // The floating toolbar is the only way out of edit mode once the window is minimized.
        _surface.EditingFinished += save => StopEditing(save);
    }

    public HardwareNode Hardware => _aquila.State.Hardware;

    /// <summary>
    /// What exists on the desktop, and what is wrong with it.
    ///
    /// A widget that cannot draw is invisible, and invisible looks exactly like deleted. Without this the
    /// only way to tell a broken widget from a removed one was to open widgets.json.
    /// </summary>
    public ObservableCollection<WidgetSummary> Widgets { get; } = [];

    /// <summary>Rebuilt when the page is shown, and after anything that changes the set — the sensors are
    /// not populated when this view model is constructed, so a list built once would report every reading
    /// as missing.</summary>
    public void RefreshWidgets()
    {
        Widgets.Clear();
        foreach (var summary in _widgets.Describe()) Widgets.Add(summary);
    }

    /// <summary>Forgets a widget, from the list rather than from the desktop — the way to reach one that
    /// cannot be seen because it does not draw.</summary>
    [RelayCommand]
    private void RemoveWidget(WidgetSummary? summary)
    {
        if (summary is null) return;

        _widgets.Remove(summary.Definition);
        RefreshWidgets();
    }

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
            // Turning the surface off mid-edit keeps the work: the user asked to hide the widgets, not to
            // throw away what they had just done to them.
            if (IsEditingOnDesktop) StopEditing(save: true);
            _surface.Hide();
        }
    }

    /// <summary>Flashes a number on each monitor, like Windows' own Identify button — a momentary answer
    /// to "which screen is which", not a label left permanently on the desktop.</summary>
    [RelayCommand]
    private void IdentifyScreens() => _surface.IdentifyScreens();

    /// <summary>
    /// Creates a widget and takes the user to it. Adding one means going to the desktop to place and dress
    /// it, so this enters edit mode rather than leaving the new widget behind on a screen nobody is looking
    /// at — the same trip they would have made a second later anyway.
    /// </summary>
    [RelayCommand]
    private void AddWidget()
    {
        // The surface first, because there has to be somewhere to put the widget; then the session, because
        // a widget added outside one would be written to disk immediately and Discard could not take it
        // back; then the widget itself. Populate is idempotent, so turning the surface on twice is harmless.
        if (!ShowOnDesktop) ShowOnDesktop = true;

        StartEditing();
        _widgets.AddWidget(presetSensorIdentifier: null);
        RefreshWidgets();
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

        // Everything else out of the way FIRST, then our own window, then the toolbar and the panel. The
        // last two are the only way back out of edit mode, so they must not be standing when the screen is
        // swept — and our own minimize stays explicit rather than trusting the shell to have obliged.
        DesktopSurface.DesktopReveal.MinimizeAll();

        if (FindMainWindow() is { } window)
            window.WindowState = WindowState.Minimized;

        _surface.SetEditing(true);
        _widgets.BeginEditSession();
    }

    /// <summary>Leaves edit mode, keeping the session's changes or throwing them away. Nothing was written
    /// while it was open, so this is the one moment either outcome is decided.</summary>
    private void StopEditing(bool save)
    {
        if (!IsEditingOnDesktop) return;

        // Asked only when there is something to lose. A confirmation that appears when nothing changed
        // teaches the user to dismiss it without reading, which is when it stops protecting anything.
        if (!save && _widgets.HasUnsavedChanges &&
            MessageBox.Show(
                "Discard every change made since edit mode was entered?",
                "Discard changes",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning,
                MessageBoxResult.No) != MessageBoxResult.Yes)
            return;

        // A locked preset cannot be written over, so changes made to one have nowhere to go unless the
        // user says where. Asked per preset, and only for the locked ones — a preset of their own is
        // simply saved, the way editing anything of your own works everywhere else.
        if (save)
            foreach (var locked in _widgets.LockedEdits)
                if (MessageBox.Show(
                        $"\u201c{locked.Name}\u201d is a built-in preset and cannot be changed.\n\n" +
                        "Keep your changes as a new preset? Choosing No puts it back as it was.",
                        "Built-in preset",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question,
                        MessageBoxResult.Yes) == MessageBoxResult.Yes)
                    _widgets.KeepAsNew(locked);

        IsEditingOnDesktop = false;
        _surface.SetEditing(false);
        _widgets.EndEditSession(save);
        RefreshWidgets();

        // Put back what entering edit mode swept aside. Wanting to see the desktop for twenty seconds is
        // not a reason to have to rebuild a whole workspace afterwards.
        DesktopSurface.DesktopReveal.RestoreAll();

        if (FindMainWindow() is { } window)
        {
            window.WindowState = WindowState.Normal;
            window.Activate();
        }
    }

    private static Window? FindMainWindow() =>
        Application.Current?.Windows.OfType<MainWindow>().FirstOrDefault();
}
