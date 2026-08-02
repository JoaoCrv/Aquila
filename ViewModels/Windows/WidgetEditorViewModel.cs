using System.Collections.ObjectModel;
using Aquila.Models;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Aquila.ViewModels.Windows;

public record WidgetKindOption(DesktopWidgetKind Kind, string Name, string Description);

public record AccentOption(string Key, string Name);

/// <summary>A named colour for the background/border pickers. A short preset list rather than a full
/// colour picker: it keeps the dialog simple, and these two surfaces only really need neutrals.</summary>
public record ColorOption(string Name, string Hex);

/// <summary>One sensor as offered in the picker. Carries the component for grouping and the live node so
/// the list can show current values — picking by name alone is guesswork when a machine reports a dozen
/// similarly-named temperatures.</summary>
public record SensorOption(string Component, string Name, string Identifier, SensorNode Sensor)
{
    public string Display => $"{Component} — {Name}";
}

/// <summary>
/// Backs the add/edit widget dialog. One view model serves all three entry points — adding from scratch,
/// adding with a sensor already chosen (from the Explorer), and editing an existing widget — because they
/// differ only in what's pre-filled, not in what the form does.
///
/// Appearance (background, transparency, border, chart colours) is a planned third section; the accent
/// picker here is the first piece of it.
/// </summary>
public partial class WidgetEditorViewModel : ObservableObject
{
    public IReadOnlyList<WidgetKindOption> Kinds { get; } =
    [
        new(DesktopWidgetKind.RadialGauge, "Radial gauge", "A dial. Best for a percentage, like load."),
        new(DesktopWidgetKind.MiniSparkline, "Sparkline", "A small line chart of the recent history."),
        new(DesktopWidgetKind.SensorMeter, "Meter", "A bar with the value beside it."),
    ];

    public IReadOnlyList<AccentOption> Accents { get; } =
    [
        new("Aquila.Cpu", "CPU"),
        new("Aquila.Gpu", "GPU"),
        new("Aquila.Ram", "Memory"),
        new("Aquila.Temp", "Temperature"),
        new("Aquila.Power", "Power"),
        new("Aquila.Critical", "Critical"),
    ];

    public IReadOnlyList<ColorOption> Colors { get; } =
    [
        new("Black", "#000000"),
        new("Charcoal", "#1E1E1E"),
        new("Slate", "#2E3B4E"),
        new("White", "#FFFFFF"),
    ];

    public ObservableCollection<SensorOption> Sensors { get; } = [];

    // --- Appearance ---

    [ObservableProperty] private ColorOption? _selectedBackground;
    [ObservableProperty] private double _backgroundOpacity = 60;   // shown as a percentage
    [ObservableProperty] private double _cornerRadius = 8;
    [ObservableProperty] private ColorOption? _selectedBorder;
    [ObservableProperty] private double _borderOpacity = 25;
    [ObservableProperty] private double _borderThickness;
    [ObservableProperty] private double _widgetWidth = 170;
    [ObservableProperty] private double _widgetHeight = 190;

    partial void OnSelectedBackgroundChanged(ColorOption? value) => Apply();
    partial void OnBackgroundOpacityChanged(double value) => Apply();
    partial void OnCornerRadiusChanged(double value) => Apply();
    partial void OnSelectedBorderChanged(ColorOption? value) => Apply();
    partial void OnBorderOpacityChanged(double value) => Apply();
    partial void OnBorderThicknessChanged(double value) => Apply();
    partial void OnWidgetWidthChanged(double value) => Apply();
    partial void OnWidgetHeightChanged(double value) => Apply();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanSave))]
    private WidgetKindOption? _selectedKind;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanSave))]
    private SensorOption? _selectedSensor;

    [ObservableProperty]
    private AccentOption? _selectedAccent;

    [ObservableProperty]
    private string _title = string.Empty;

    /// <summary>
    /// Raised after the form has been written into the definition, so the host can re-render it. There is
    /// no preview inside this dialog on purpose: the definition IS the widget, so editing it updates the
    /// real thing on the real desktop — the only place where its actual size and the way its transparency
    /// reads against the user's own wallpaper are apparent.
    /// </summary>
    public event Action? Changed;

    partial void OnSelectedKindChanged(WidgetKindOption? value)
    {
        // Only for a new widget: changing the type of an existing one must not throw away a size the user
        // deliberately set.
        if (value is not null && !IsEditing)
        {
            // Shapes want different proportions — a dial roughly square, a sparkline wide and short.
            (WidgetWidth, WidgetHeight) = value.Kind switch
            {
                DesktopWidgetKind.RadialGauge => (170d, 190d),
                DesktopWidgetKind.MiniSparkline => (240d, 100d),
                _ => (240d, 80d),
            };
        }

        Apply();
    }
    partial void OnSelectedSensorChanged(SensorOption? value) => Apply();
    partial void OnSelectedAccentChanged(AccentOption? value) => Apply();
    partial void OnTitleChanged(string value) => Apply();

    /// <summary>Writes the form into the live definition and tells the host to re-render it. Position and
    /// screen are never touched — the editor changes what a widget IS, not where it sits.</summary>
    private void Apply()
    {
        if (!_loaded || _target is null) return;

        _target.Kind = SelectedKind?.Kind ?? _target.Kind;
        _target.SensorIdentifier = SelectedSensor?.Identifier ?? string.Empty;
        _target.AccentKey = SelectedAccent?.Key ?? "Aquila.Cpu";
        _target.Title = string.IsNullOrWhiteSpace(Title) ? SelectedSensor?.Name ?? string.Empty : Title.Trim();

        _target.BackgroundColor = SelectedBackground?.Hex ?? "#000000";
        _target.BackgroundOpacity = BackgroundOpacity / 100;
        _target.CornerRadius = CornerRadius;
        _target.BorderColor = SelectedBorder?.Hex ?? "#FFFFFF";
        _target.BorderOpacity = BorderOpacity / 100;
        _target.BorderThickness = BorderThickness;

        _target.Width = WidgetWidth;
        _target.Height = WidgetHeight;

        Changed?.Invoke();
    }

    [ObservableProperty]
    private string _sensorFilter = string.Empty;

    /// <summary>True when editing an existing widget, so the dialog can title itself accordingly.</summary>
    public bool IsEditing { get; private set; }

    public bool CanSave => SelectedKind is not null && SelectedSensor is not null;

    private readonly List<SensorOption> _allSensors = [];
    private bool _loaded;

    /// <summary>The live definition this form edits — the single source of truth, already in the widget
    /// list and already rendering. There is no copy to reconcile.</summary>
    private DesktopWidgetDefinition? _target;

    public void Load(HardwareNode hardware, DesktopWidgetDefinition target, bool isNew, string? presetSensorIdentifier)
    {
        _allSensors.Clear();
        foreach (var component in SensorCatalog.GetComponents(hardware))
            foreach (var entry in component.Sensors)
                if (!string.IsNullOrEmpty(entry.Sensor.Identifier))
                    _allSensors.Add(new SensorOption(component.Name, entry.Label, entry.Sensor.Identifier!, entry.Sensor));

        ApplyFilter();

        _target = target;
        IsEditing = !isNew;

        // The form is filled from the definition itself, so "default" is defined in exactly one place.
        SelectedKind = Kinds.FirstOrDefault(k => k.Kind == target.Kind) ?? Kinds[0];
        SelectedAccent = Accents.FirstOrDefault(a => a.Key == target.AccentKey) ?? Accents[0];
        Title = target.Title;

        var identifier = string.IsNullOrEmpty(target.SensorIdentifier) ? presetSensorIdentifier : target.SensorIdentifier;
        SelectedSensor = _allSensors.FirstOrDefault(s => s.Identifier == identifier);

        SelectedBackground = MatchColor(target.BackgroundColor);
        BackgroundOpacity = target.BackgroundOpacity * 100;
        CornerRadius = target.CornerRadius;
        SelectedBorder = MatchColor(target.BorderColor);
        BorderOpacity = target.BorderOpacity * 100;
        BorderThickness = target.BorderThickness;

        if (!isNew)
        {
            WidgetWidth = target.Width;
            WidgetHeight = target.Height;
        }

        // The setters above each fire a rebuild; letting them run only after this point means one preview
        // on open instead of a dozen. The first one is raised by the window once it has subscribed.
        _loaded = true;
    }


    /// <summary>Keeps a hand-edited or unknown colour usable by falling back to the first preset, rather
    /// than leaving the picker blank.</summary>
    private ColorOption MatchColor(string hex) =>
        Colors.FirstOrDefault(c => string.Equals(c.Hex, hex, StringComparison.OrdinalIgnoreCase)) ?? Colors[0];

    partial void OnSensorFilterChanged(string value) => ApplyFilter();

    private void ApplyFilter()
    {
        var keep = SelectedSensor;

        Sensors.Clear();
        foreach (var sensor in _allSensors)
            if (string.IsNullOrWhiteSpace(SensorFilter) ||
                sensor.Display.Contains(SensorFilter, StringComparison.OrdinalIgnoreCase))
                Sensors.Add(sensor);

        // Filtering must not silently drop the current choice — the user would save something they can't
        // see. Keep it selected if it survived the filter.
        if (keep is not null && Sensors.Contains(keep)) SelectedSensor = keep;
    }
}
