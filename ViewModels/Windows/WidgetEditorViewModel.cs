using System.Collections.ObjectModel;
using Aquila.Models;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Aquila.ViewModels.Windows;

public record WidgetKindOption(DesktopWidgetKind Kind, string Name, string Description);

public record AccentOption(string Key, string Name);

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

    public ObservableCollection<SensorOption> Sensors { get; } = [];

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

    /// <summary>The widget exactly as it will appear on the desktop — built by the same code that builds
    /// the real thing, so the preview can't drift from reality.</summary>
    [ObservableProperty]
    private System.Windows.UIElement? _preview;

    partial void OnSelectedKindChanged(WidgetKindOption? value) => RebuildPreview();
    partial void OnSelectedSensorChanged(SensorOption? value) => RebuildPreview();
    partial void OnSelectedAccentChanged(AccentOption? value) => RebuildPreview();
    partial void OnTitleChanged(string value) => RebuildPreview();

    private void RebuildPreview()
    {
        if (!_loaded || SelectedKind is null || SelectedSensor is null)
        {
            Preview = null;
            return;
        }

        // A throwaway definition: previewing must never mutate the one being edited, or cancelling would
        // still have changed it.
        Preview = Aquila.Services.DesktopWidgetService.Build(ToDefinition(null), SelectedSensor.Sensor);
    }

    [ObservableProperty]
    private string _sensorFilter = string.Empty;

    /// <summary>True when editing an existing widget, so the dialog can title itself accordingly.</summary>
    public bool IsEditing { get; private set; }

    public bool CanSave => SelectedKind is not null && SelectedSensor is not null;

    private readonly List<SensorOption> _allSensors = [];
    private bool _loaded;

    public void Load(HardwareNode hardware, DesktopWidgetDefinition? existing, string? presetSensorIdentifier)
    {
        _allSensors.Clear();
        foreach (var component in SensorCatalog.GetComponents(hardware))
            foreach (var entry in component.Sensors)
                if (!string.IsNullOrEmpty(entry.Sensor.Identifier))
                    _allSensors.Add(new SensorOption(component.Name, entry.Label, entry.Sensor.Identifier!, entry.Sensor));

        ApplyFilter();

        IsEditing = existing is not null;

        SelectedKind = Kinds.FirstOrDefault(k => k.Kind == (existing?.Kind ?? DesktopWidgetKind.RadialGauge));
        SelectedAccent = Accents.FirstOrDefault(a => a.Key == existing?.AccentKey) ?? Accents[0];
        Title = existing?.Title ?? string.Empty;

        var identifier = existing?.SensorIdentifier ?? presetSensorIdentifier;
        SelectedSensor = _allSensors.FirstOrDefault(s => s.Identifier == identifier);

        // Only now: the setters above each fire a rebuild, and doing it once at the end avoids building
        // three throwaway previews on open.
        _loaded = true;
        RebuildPreview();
    }

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

    /// <summary>Writes the form into a definition. Existing widgets keep their position and screen — the
    /// editor changes what a widget IS, not where it sits.</summary>
    public DesktopWidgetDefinition ToDefinition(DesktopWidgetDefinition? existing)
    {
        var definition = existing ?? new DesktopWidgetDefinition { X = 32, Y = 150 };

        definition.Kind = SelectedKind!.Kind;
        definition.SensorIdentifier = SelectedSensor!.Identifier;
        definition.AccentKey = SelectedAccent?.Key ?? "Aquila.Cpu";
        definition.Title = string.IsNullOrWhiteSpace(Title) ? SelectedSensor!.Name : Title.Trim();

        if (existing is null)
        {
            // Defaults that suit each shape — a dial wants to be square, a sparkline wide and short.
            (definition.Width, definition.Height) = SelectedKind!.Kind switch
            {
                DesktopWidgetKind.RadialGauge => (170d, 190d),
                DesktopWidgetKind.MiniSparkline => (240d, 100d),
                _ => (240d, 80d),
            };
        }

        return definition;
    }
}
