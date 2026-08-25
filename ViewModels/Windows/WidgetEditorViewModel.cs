using System.Collections.ObjectModel;
using Aquila.Models;
using Aquila.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Aquila.ViewModels.Windows;

public record AccentOption(string Key, string Name);

/// <summary>A named colour for the background/border pickers. A short preset list rather than a full
/// colour picker: it keeps the dialog simple, and these two surfaces only really need neutrals.</summary>
public record ColorOption(string Name, string Hex);

/// <summary>One sensor as offered in the picker. Carries the component for grouping and the live node so
/// the list can show current values — picking by name alone is guesswork when a machine reports a dozen
/// similarly-named temperatures.</summary>
/// <summary>
/// How much of a widget an edit actually disturbed.
///
/// Every edit used to mean rebuilding the whole thing — a new piece, new bindings, and for the two
/// LiveCharts-backed kinds a new chart with new Skia paints and an empty line, on every notch of every
/// slider. Saying which of the two happened lets the cheap case stay cheap.
/// </summary>
public enum WidgetChange
{
    /// <summary>Colours, opacity, corners, size, layer, title. The piece inside is untouched.</summary>
    Style,

    /// <summary>The kind, or which readings it draws. The piece has to be built again.</summary>
    Structure,
}

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
    /// <summary>Straight from the catalog, so a widget added there appears here with no second edit —
    /// this list used to be a copy, and a copy is a list that goes out of date.</summary>
    public IReadOnlyList<WidgetKindInfo> Kinds { get; } = WidgetCatalog.All;

    /// <summary>
    /// Colour choices, by role in the active scheme. These used to be hardware names — you had to pick
    /// "CPU" to get blue, even for a network widget — which is exactly the confusion the scheme model
    /// removed. Whatever scheme is active answers these roles with its own hues.
    /// </summary>
    public IReadOnlyList<AccentOption> Accents { get; } =
    [
        new("Aquila.Scheme.Accent", "Accent"),
        new("Aquila.Scheme.Series2", "Alternate"),
        new("Aquila.Scheme.Series3", "Tertiary"),
        new("Aquila.Scheme.Alert", "Alert"),
        new("Aquila.Scheme.Critical", "Critical"),
    ];

    public IReadOnlyList<ColorOption> Colors { get; } =
    [
        new("Black", "#000000"),
        new("Charcoal", "#1E1E1E"),
        new("Slate", "#2E3B4E"),
        new("White", "#FFFFFF"),
    ];

    public ObservableCollection<SensorOption> Sensors { get; } = [];

    /// <summary>
    /// What the widget draws, in the order it was added — the shopping-basket half of the dialog. The list
    /// above picks, this one holds, and each entry can be removed.
    ///
    /// Replaced a fixed "sensor" plus an optional "second sensor". Those were the same thing under two
    /// names, and nothing on screen explained why one could be cleared and the other could not.
    /// </summary>
    public ObservableCollection<SeriesRow> Chosen { get; } = [];

    // --- Appearance ---

    [ObservableProperty] private ColorOption? _selectedBackground;
    [ObservableProperty] private double _backgroundOpacity = 60;   // shown as a percentage
    [ObservableProperty] private double _cornerRadius = 8;
    [ObservableProperty] private ColorOption? _selectedBorder;
    [ObservableProperty] private double _borderOpacity = 25;
    [ObservableProperty] private double _borderThickness;
    [ObservableProperty] private double _widgetWidth = 170;
    [ObservableProperty] private double _widgetHeight = 190;

    /// <summary>Where the widget sits on its screen's canvas, in DIPs. Editable as numbers as well as by
    /// dragging: it is how a widget stranded off-screen is fetched back, and how someone laying widgets out
    /// deliberately gets them to line up.</summary>
    [ObservableProperty] private double _widgetX;
    [ObservableProperty] private double _widgetY;

    partial void OnSelectedBackgroundChanged(ColorOption? value) => Apply();
    partial void OnBackgroundOpacityChanged(double value) => Apply();
    partial void OnCornerRadiusChanged(double value) => Apply();
    partial void OnSelectedBorderChanged(ColorOption? value) => Apply();
    partial void OnBorderOpacityChanged(double value) => Apply();
    partial void OnBorderThicknessChanged(double value) => Apply();
    partial void OnWidgetWidthChanged(double value) => Apply();
    partial void OnWidgetHeightChanged(double value) => Apply();
    partial void OnWidgetXChanged(double value) => Apply();
    partial void OnWidgetYChanged(double value) => Apply();
    partial void OnLineThicknessChanged(double value) => Apply();
    partial void OnSelectedFillChanged(ChartFill value) => Apply();
    partial void OnSmoothnessChanged(double value) => Apply();
    partial void OnPointSizeChanged(double value) => Apply();
    partial void OnScaleMinChanged(double value) => Apply();
    partial void OnScaleMaxChanged(double value) => Apply();

    partial void OnSelectedScaleChanged(ChartScale value)
    {
        // Switching to Manual with the scale still at its defaults would put a temperature on a 0-100 axis
        // and make the user work out sensible ends from scratch. Seeding from what the sensor has actually
        // been observed to do gives them something to adjust instead of something to invent.
        if (value == ChartScale.Manual && ScaleMin == 0 && ScaleMax == 100 &&
            Chosen.FirstOrDefault()?.Sensor.Sensor is { Min: { } low, Max: { } high } && high > low)
        {
            var headroom = (high - low) * 0.1;
            ScaleMin = Math.Floor(low - headroom);
            ScaleMax = Math.Ceiling(high + headroom);
        }

        Apply();
    }

    /// <summary>How the chart's vertical scale is decided, and its ends when the user decides them.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ScaleIsManual))]
    private ChartScale _selectedScale = ChartScale.FromZero;

    [ObservableProperty] private double _scaleMin;
    [ObservableProperty] private double _scaleMax = 100;

    public IReadOnlyList<ChartScale> Scales { get; } = Enum.GetValues<ChartScale>();

    /// <summary>Only Manual has ends to type in — the other two work them out.</summary>
    public bool ScaleIsManual => SelectedScale == ChartScale.Manual;

    /// <summary>The line settings, for the kinds drawn as one. Style changes every one of them — none
    /// touches what the widget draws, only how the line looks doing it.</summary>
    [ObservableProperty] private double _lineThickness = 1.5;
    [ObservableProperty] private ChartFill _selectedFill = ChartFill.Gradient;
    [ObservableProperty] private double _smoothness = 0.5;
    [ObservableProperty] private double _pointSize;

    /// <summary>The three fill styles, straight off the enum so the list can never fall behind it.</summary>
    public IReadOnlyList<ChartFill> Fills { get; } = Enum.GetValues<ChartFill>();

    /// <summary>Whether this kind has a line at all. A dial has none, and a section of settings that do
    /// nothing is worse than no section.</summary>
    public bool ShowsLineOptions => SelectedKind?.HasLine == true;

    partial void OnArcThicknessChanged(double value)
    {
        // The ceiling moves with the thickness, so a value that no longer fits comes down with it. Setting
        // ArcCorner runs its own handler, which applies — hence the early return rather than a second one.
        if (ArcCorner > MaxArcCorner)
        {
            ArcCorner = MaxArcCorner;
            return;
        }

        Apply();
    }

    /// <summary>Half the arc's thickness — the roundest a cap can be before it starts eating the arc.
    /// The slider's ceiling, so there is no stretch of travel that quietly does nothing.</summary>
    public double MaxArcCorner => ArcThickness / 2;
    partial void OnArcCornerChanged(double value) => Apply();
    partial void OnSelectedSweepChanged(GaugeSweep value) => Apply();
    partial void OnValueSizeChanged(double value) => Apply();
    partial void OnShowValueChanged(bool value) => Apply();

    /// <summary>The dial settings. Style, like the line ones — none of them changes what is measured.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MaxArcCorner))]
    private double _arcThickness = 14;
    [ObservableProperty] private double _arcCorner;
    [ObservableProperty] private GaugeSweep _selectedSweep = GaugeSweep.Dial;
    [ObservableProperty] private double _valueSize = 24;
    [ObservableProperty] private bool _showValue = true;

    public IReadOnlyList<GaugeSweep> Sweeps { get; } = Enum.GetValues<GaugeSweep>();

    /// <summary>Whether this kind is a dial at all.</summary>
    public bool ShowsDialOptions => SelectedKind?.HasDial == true;

    partial void OnBarThicknessChanged(double value) => Apply();
    partial void OnBarCornerChanged(double value) => Apply();
    partial void OnSelectedLayoutChanged(MeterLayout value) => Apply();
    partial void OnBarValueSizeChanged(double value) => Apply();

    /// <summary>The bar settings. ShowValue is shared with the dial (both mean "print the number"); the
    /// size is not, because the two need very different ones.</summary>
    [ObservableProperty] private double _barThickness = 6;
    [ObservableProperty] private double _barCorner = 3;
    [ObservableProperty] private MeterLayout _selectedLayout = MeterLayout.Beside;
    [ObservableProperty] private double _barValueSize = 13;

    public IReadOnlyList<MeterLayout> Layouts { get; } = Enum.GetValues<MeterLayout>();

    /// <summary>Whether this kind is a bar at all.</summary>
    public bool ShowsBarOptions => SelectedKind?.HasBar == true;

    partial void OnStatValueSizeChanged(double value) => Apply();
    partial void OnShowUnitChanged(bool value) => Apply();
    partial void OnUnitSizeChanged(double value) => Apply();
    partial void OnShowPanelChanged(bool value) => Apply();

    /// <summary>The number settings.</summary>
    [ObservableProperty] private double _statValueSize = 20;
    [ObservableProperty] private bool _showUnit = true;
    [ObservableProperty] private double _unitSize = 13;
    [ObservableProperty] private bool _showPanel = true;

    /// <summary>Whether this kind is just a number.</summary>
    public bool ShowsNumberOptions => SelectedKind?.HasNumber == true;

    partial void OnLayerChanged(double value) => Apply();

    /// <summary>Which widget wins where two overlap. A double because that is what a Slider binds to; the
    /// definition keeps it as the whole number it really is.</summary>
    [ObservableProperty]
    private double _layer;

    [ObservableProperty]
    private WidgetKindInfo? _selectedKind;

    [ObservableProperty]
    private SensorOption? _selectedSensor;

    /// <summary>
    /// Whether the button swaps the one reading instead of appending another.
    ///
    /// A widget that draws a single value has no "add" that means anything once it has one, and leaving the
    /// button dead made changing its sensor a two-step demolition: remove the reading — which blanked the
    /// widget on the desktop, since a widget with nothing to draw draws nothing — then add the new one.
    /// Pointing at another sensor plainly means "that one instead".
    ///
    /// Only at one. A chart holding two readings has no obvious one to replace, so there it stays an add,
    /// and full means full.
    /// </summary>
    public bool ReplacesSeries => SelectedKind is { MaxSeries: 1 } && Chosen.Count == 1;

    /// <summary>How many more readings this kind will take, said plainly.</summary>
    public string SeriesHint => SelectedKind is null
        ? string.Empty
        : ReplacesSeries
            ? "This widget draws one reading. Pick another sensor to swap it."
            : Chosen.Count >= SelectedKind.MaxSeries
                ? $"This widget draws {SelectedKind.MaxSeries} readings — that is all of them."
                : $"Pick a sensor above and add it. Up to {SelectedKind.MaxSeries}.";

    /// <summary>The button says what pressing it will do, so the two behaviours are never a surprise.</summary>
    public string AddLabel => ReplacesSeries ? "Replace" : "Add";

    public bool CanAdd => SelectedSensor is not null
                          && SelectedKind is not null
                          && (ReplacesSeries || Chosen.Count < SelectedKind.MaxSeries);

    [RelayCommand]
    private void AddSeries()
    {
        if (!CanAdd || SelectedSensor is null) return;

        // Swapped in place rather than removed and re-added: the widget keeps its colour role and never
        // spends a frame with nothing to draw.
        if (ReplacesSeries) Chosen.Clear();

        Chosen.Add(new SeriesRow(SelectedSensor, Accents, DefaultAccentFor(Chosen.Count), OnSeriesEdited));
        OnSeriesEdited();
    }

    [RelayCommand]
    private void RemoveSeries(SeriesRow? row)
    {
        if (row is null) return;
        Chosen.Remove(row);
        OnSeriesEdited();
    }

    /// <summary>Spread across roles rather than all taking the accent: two lines in the same colour are
    /// one line.</summary>
    private AccentOption DefaultAccentFor(int index) =>
        Accents.Count > index ? Accents[index] : Accents[0];

    private void OnSeriesEdited()
    {
        NotifySeriesState();
        Apply(WidgetChange.Structure);
    }

    /// <summary>Everything the chosen kind and the series list decide together. Raised from every place that
    /// changes either one, so the button, its label and the hint can never disagree with the list under
    /// them — four separate notifications in three places was how they drifted apart.</summary>
    private void NotifySeriesState()
    {
        OnPropertyChanged(nameof(ReplacesSeries));
        OnPropertyChanged(nameof(CanAdd));
        OnPropertyChanged(nameof(SeriesHint));
        OnPropertyChanged(nameof(AddLabel));
        OnPropertyChanged(nameof(ShowsLineOptions));
        OnPropertyChanged(nameof(ShowsDialOptions));
        OnPropertyChanged(nameof(ShowsBarOptions));
        OnPropertyChanged(nameof(ShowsNumberOptions));
    }

    [ObservableProperty]
    private string _title = string.Empty;

    /// <summary>
    /// Raised after the form has been written into the definition, so the host can re-render it. There is
    /// no preview inside this dialog on purpose: the definition IS the widget, so editing it updates the
    /// real thing on the real desktop — the only place where its actual size and the way its transparency
    /// reads against the user's own wallpaper are apparent.
    /// </summary>
    public event Action<WidgetChange>? Changed;

    partial void OnSelectedKindChanged(WidgetKindInfo? value)
    {
        // The kind's proportions — a dial roughly square, a chart wide and tall enough to read — declared
        // once in the catalog rather than repeated here.
        //
        // Taken when the USER picks a kind (_loaded), because a size that suited a dial does not suit a
        // chart: a meter is 240x80, and a chart squeezed into 80px of height has no room left to draw a
        // line once the title and the padding have taken theirs. Also taken when a brand new widget has no
        // size at all. NOT taken while the form is being filled from a definition — there the size is being
        // read out of the widget, and the default would overwrite what the user dragged.
        if (value is not null && (_loaded || WidgetWidth <= 0 || WidgetHeight <= 0))
            (WidgetWidth, WidgetHeight) = (value.DefaultWidth, value.DefaultHeight);

        // Switching from a chart to a dial leaves readings the new kind cannot draw. Dropped here rather
        // than quietly ignored at render time, so what is saved is what is on screen.
        while (value is not null && Chosen.Count > value.MaxSeries)
            Chosen.RemoveAt(Chosen.Count - 1);

        NotifySeriesState();
        Apply(WidgetChange.Structure);
    }

    /// <summary>Selecting in the list no longer changes the widget — it only arms the Add button. Picking
    /// and adding are separate on purpose: a click that silently replaced a series would be very easy to
    /// do by accident while scrolling a list of two hundred sensors.</summary>
    partial void OnSelectedSensorChanged(SensorOption? value) => NotifySeriesState();
    partial void OnTitleChanged(string value) => Apply();

    /// <summary>Writes the form into the live definition and tells the host to re-render it. The screen is
    /// never touched — moving a widget between monitors is the desktop context menu's job, because it is
    /// the only place that can show you which monitor you are pointing at.</summary>
    private void Apply(WidgetChange change = WidgetChange.Style)
    {
        if (!_loaded || _target is null) return;

        _target.Kind = SelectedKind?.Kind ?? _target.Kind;
        _target.Series = [.. Chosen.Select(row => new WidgetSeries
        {
            SensorIdentifier = row.Sensor.Identifier,
            AccentKey = row.Accent.Key,
        })];
        _target.Title = string.IsNullOrWhiteSpace(Title)
            ? Chosen.FirstOrDefault()?.Sensor.Name ?? string.Empty
            : Title.Trim();

        _target.BackgroundColor = SelectedBackground?.Hex ?? "#000000";
        _target.BackgroundOpacity = BackgroundOpacity / 100;
        _target.CornerRadius = CornerRadius;
        _target.BorderColor = SelectedBorder?.Hex ?? "#FFFFFF";
        _target.BorderOpacity = BorderOpacity / 100;
        _target.BorderThickness = BorderThickness;

        _target.Width = WidgetWidth;
        _target.Height = WidgetHeight;
        _target.X = WidgetX;
        _target.Y = WidgetY;
        _target.ZIndex = (int)Layer;

        _target.LineThickness = LineThickness;
        _target.Fill = SelectedFill;
        _target.LineSmoothness = Smoothness;
        _target.PointSize = PointSize;
        _target.Scale = SelectedScale;
        _target.ScaleMin = ScaleMin;
        _target.ScaleMax = ScaleMax;

        _target.ArcThickness = ArcThickness;
        _target.ArcCorner = ArcCorner;
        _target.Sweep = SelectedSweep;
        _target.ValueSize = ValueSize;
        _target.ShowValue = ShowValue;

        _target.BarThickness = BarThickness;
        _target.BarCorner = BarCorner;
        _target.Layout = SelectedLayout;
        _target.BarValueSize = BarValueSize;

        _target.StatValueSize = StatValueSize;
        _target.ShowUnit = ShowUnit;
        _target.UnitSize = UnitSize;
        _target.ShowPanel = ShowPanel;

        Changed?.Invoke(change);
    }

    [ObservableProperty]
    private string _sensorFilter = string.Empty;

    /// <summary>Whether there is anything to edit. The panel is long-lived and spends part of its life
    /// pointed at nothing, and a form full of controls that change nothing reads as broken.</summary>
    public bool HasTarget => _target is not null;

    /// <summary>Points the form at nothing — the selection was cleared, or the widget was removed.</summary>
    /// <summary>
    /// Re-reads position and size from the definition, for when the desktop changed them behind the panel's
    /// back — dragging moves a widget and the wheel resizes it, and boxes still showing the old numbers
    /// would be reporting something untrue about the thing in front of the user.
    ///
    /// Applying is suppressed while they update. The desktop is already drawing this geometry; writing it
    /// back would re-render the widget on every notch of the wheel and every step of the drag, fighting the
    /// gesture that caused it.
    /// </summary>
    public void SyncGeometry()
    {
        if (_target is null) return;

        var loaded = _loaded;
        _loaded = false;
        WidgetWidth = _target.Width;
        WidgetHeight = _target.Height;
        WidgetX = _target.X;
        WidgetY = _target.Y;
        _loaded = loaded;
    }

    public void Clear()
    {
        _loaded = false;
        _target = null;
        Chosen.Clear();
        SelectedSensor = null;
        SensorFilter = string.Empty;
        OnPropertyChanged(nameof(HasTarget));
    }


    private readonly List<SensorOption> _allSensors = [];
    private bool _loaded;

    /// <summary>The live definition this form edits — the single source of truth, already in the widget
    /// list and already rendering. There is no copy to reconcile.</summary>
    private DesktopWidgetDefinition? _target;

    public void Load(HardwareNode hardware, DesktopWidgetDefinition target, string? presetSensorIdentifier)
    {
        // Disarmed FIRST, before a single value is assigned.
        //
        // The panel outlives the widget it is pointed at, so on the second and every later Load this flag
        // is still true from the one before. Every setter below would then fire Apply, and Apply writes the
        // WHOLE form into _target — which has already been switched to the new widget while the form still
        // holds the old one's. Clicking a second widget turned it into a copy of the first, and saved it.
        _loaded = false;

        _allSensors.Clear();
        foreach (var component in SensorCatalog.GetComponents(hardware))
            foreach (var entry in component.Sensors)
                if (!string.IsNullOrEmpty(entry.Sensor.Identifier))
                    _allSensors.Add(new SensorOption(component.Name, entry.Label, entry.Sensor.Identifier!, entry.Sensor));

        ApplyFilter();

        _target = target;

        // Size before kind: choosing a kind adopts its default proportions only when there is no size yet,
        // so the handler has to be able to see whether this widget already has one.
        WidgetWidth = target.Width;
        WidgetHeight = target.Height;
        WidgetX = target.X;
        WidgetY = target.Y;

        // The form is filled from the definition itself, so "default" is defined in exactly one place.
        SelectedKind = Kinds.FirstOrDefault(k => k.Kind == target.Kind) ?? Kinds[0];
        Title = target.Title;

        Chosen.Clear();
        foreach (var series in target.Series)
        {
            var sensor = _allSensors.FirstOrDefault(s => s.Identifier == series.SensorIdentifier);
            if (sensor is null) continue;   // the machine no longer reports it

            Chosen.Add(new SeriesRow(sensor, Accents,
                Accents.FirstOrDefault(a => a.Key == series.AccentKey) ?? DefaultAccentFor(Chosen.Count),
                OnSeriesEdited));
        }

        // Pinned from the Explorer: the sensor is already decided, so add it rather than making the user
        // find it again in a list of two hundred.
        if (Chosen.Count == 0 && !string.IsNullOrEmpty(presetSensorIdentifier))
        {
            var preset = _allSensors.FirstOrDefault(s => s.Identifier == presetSensorIdentifier);
            if (preset is not null)
                Chosen.Add(new SeriesRow(preset, Accents, DefaultAccentFor(0), OnSeriesEdited));
        }

        SelectedBackground = MatchColor(target.BackgroundColor);
        BackgroundOpacity = target.BackgroundOpacity * 100;
        CornerRadius = target.CornerRadius;
        SelectedBorder = MatchColor(target.BorderColor);
        BorderOpacity = target.BorderOpacity * 100;
        BorderThickness = target.BorderThickness;
        Layer = target.ZIndex;

        LineThickness = target.LineThickness;
        SelectedFill = target.Fill;
        Smoothness = target.LineSmoothness;
        PointSize = target.PointSize;
        SelectedScale = target.Scale;
        ScaleMin = target.ScaleMin;
        ScaleMax = target.ScaleMax;

        ArcThickness = target.ArcThickness;
        ArcCorner = target.ArcCorner;
        SelectedSweep = target.Sweep;
        ValueSize = target.ValueSize;
        ShowValue = target.ShowValue;

        BarThickness = target.BarThickness;
        BarCorner = target.BarCorner;
        SelectedLayout = target.Layout;
        BarValueSize = target.BarValueSize;

        StatValueSize = target.StatValueSize;
        ShowUnit = target.ShowUnit;
        UnitSize = target.UnitSize;
        ShowPanel = target.ShowPanel;

        // The setters above each fire a rebuild; letting them run only after this point means one preview
        // on open instead of a dozen. The first one is raised by the window once it has subscribed.
        _loaded = true;

        // The form is gated on this: the panel spends part of its life pointed at nothing, and everything
        // below the header is hidden until it is pointed at something.
        OnPropertyChanged(nameof(HasTarget));

        // Chosen is refilled above, after the kind was set, so anything reading both is out of date by now.
        NotifySeriesState();
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

/// <summary>
/// One chosen reading in the editor, with its colour. Observable because the colour combo edits it in
/// place; the callback is how a change reaches the widget on the desktop without the row needing to know
/// what a widget is.
/// </summary>
public partial class SeriesRow : ObservableObject
{
    private readonly Action _changed;

    public SeriesRow(SensorOption sensor, IReadOnlyList<AccentOption> accents, AccentOption accent, Action changed)
    {
        Sensor = sensor;
        Accents = accents;
        _accent = accent;
        _changed = changed;
    }

    public SensorOption Sensor { get; }
    public IReadOnlyList<AccentOption> Accents { get; }

    [ObservableProperty] private AccentOption _accent;

    partial void OnAccentChanged(AccentOption value) => _changed();
}
