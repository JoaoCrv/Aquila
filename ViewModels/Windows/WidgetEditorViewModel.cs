using System.Collections.ObjectModel;
using Aquila.Models;
using Aquila.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Aquila.ViewModels.Windows;

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

    /// <summary>The preset. Re-dresses every widget wearing it, not just the one being edited — which is
    /// the reason it is a separate case rather than a wider Style.</summary>
    Dress,
}

/// <summary>A colour worth offering by name. Only ever WRITES the hex: the preset's own value is the truth,
/// so a colour the list does not have stays exactly as the preset spells it.</summary>
public record ColorOption(string Name, string Hex);

public record SensorOption(string Component, string Name, string Identifier, SensorNode Sensor, SensorEntry Entry)
{
    public string Display => $"{Component} — {Name}";
}

/// <summary>
/// Backs the add/edit widget dialog. One view model serves all three entry points — adding from scratch,
/// adding with a sensor already chosen (from the Explorer), and editing an existing widget — because they
/// differ only in what's pre-filled, not in what the form does.
///
/// It edits CONTENT and PLACEMENT only. What a widget looks like lives in its preset, which is a file of
/// its own and shared with every widget wearing it — so a colour picker here would be editing something
/// that does not belong to this widget.
/// </summary>
public partial class WidgetEditorViewModel(PresetService presets) : ObservableObject
{
    /// <summary>Straight from the catalog, so a widget added there appears here with no second edit —
    /// this list used to be a copy, and a copy is a list that goes out of date.</summary>
    public IReadOnlyList<WidgetKindInfo> Kinds { get; } = WidgetCatalog.All;

    /// <summary>Every preset a widget can wear. Straight from the service, so one written into the folder
    /// by hand appears here without a second list to keep in step.</summary>
    public IReadOnlyList<Preset> Presets => presets.Presets;

    public ObservableCollection<SensorOption> Sensors { get; } = [];

    /// <summary>
    /// What the widget draws, in the order it was added — the shopping-basket half of the dialog. The list
    /// above picks, this one holds, and each entry can be removed.
    ///
    /// Replaced a fixed "sensor" plus an optional "second sensor". Those were the same thing under two
    /// names, and nothing on screen explained why one could be cleared and the other could not.
    /// </summary>
    public ObservableCollection<SeriesRow> Chosen { get; } = [];

    /// <summary>Which preset dresses this widget. Changing it re-dresses the widget and nothing else —
    /// editing the preset itself is what changes every widget wearing it, and that is deliberate.</summary>
    [ObservableProperty] private Preset? _selectedPreset;

    partial void OnSelectedPresetChanged(Preset? value)
    {
        Read(value);
        Apply();
    }

    /// <summary>Whether the chosen preset is one of ours. Shown to the user, because it decides what Save
    /// will do — edits to a built-in leave as a variant, and finding that out afterwards is a surprise.</summary>
    public bool PresetIsBuiltIn => SelectedPreset?.IsBuiltIn == true;

    public string PresetNote => SelectedPreset is not { } preset
        ? string.Empty
        : preset.IsBuiltIn
            ? $"{preset.Name} is built in. Your changes are saved as a variant of it, leaving the original alone."
            : $"Changes are saved into {preset.Name}, and every widget wearing it follows.";

    // --- Appearance. These write into the PRESET, so they change every widget wearing it. ---

    public IReadOnlyList<ColorOption> Colors { get; } =
    [
        new("Black", "#000000"),
        new("Charcoal", "#1E1E1E"),
        new("Ink", "#1F1A16"),
        new("Slate", "#2E3B4E"),
        new("White", "#FFFFFF"),
    ];

    [ObservableProperty] private string _backgroundColor = "#000000";
    [ObservableProperty] private double _backgroundOpacity = 60;
    [ObservableProperty] private string _borderColor = "#FFFFFF";
    [ObservableProperty] private double _borderOpacity = 25;
    [ObservableProperty] private double _borderThickness;
    [ObservableProperty] private double _cornerRadius = 8;

    partial void OnBackgroundColorChanged(string value) => Dress();
    partial void OnBackgroundOpacityChanged(double value) => Dress();
    partial void OnBorderColorChanged(string value) => Dress();
    partial void OnBorderOpacityChanged(double value) => Dress();
    partial void OnBorderThicknessChanged(double value) => Dress();
    partial void OnCornerRadiusChanged(double value) => Dress();

    [RelayCommand] private void PickBackground(string? hex) => BackgroundColor = hex ?? BackgroundColor;
    [RelayCommand] private void PickBorder(string? hex) => BorderColor = hex ?? BorderColor;

    /// <summary>True while the form is being filled FROM a preset, so the writes that causes are not read
    /// back as edits. Without it, merely switching preset would mark the new one as edited — it would be
    /// written with its own values, which changes nothing and still ends in being asked to save it.</summary>
    private bool _reading;

    /// <summary>Fills the appearance controls from a preset without touching it.</summary>
    private void Read(Preset? preset)
    {
        OnPropertyChanged(nameof(PresetIsBuiltIn));
        OnPropertyChanged(nameof(PresetNote));

        if (preset is null) return;

        _reading = true;
        try
        {
            BackgroundColor = preset.Background.Color;
            BackgroundOpacity = preset.Background.Opacity * 100;
            BorderColor = preset.Border.Color;
            BorderOpacity = preset.Border.Opacity * 100;
            BorderThickness = preset.Border.Thickness;
            CornerRadius = preset.Border.CornerRadius;
        }
        finally
        {
            _reading = false;
        }
    }

    /// <summary>
    /// Writes the appearance controls into the preset and asks the host to re-dress everything wearing it.
    ///
    /// Drafting happens here, on the first actual edit, so the preset is only marked as changed by someone
    /// changing it. Opacity is a percentage on screen and a fraction in the file: sliders that read 0-100
    /// are what people expect, and a file that stores 0.6 is what every other colour format does.
    /// </summary>
    private void Dress()
    {
        if (_reading || !_loaded || SelectedPreset is not { } chosen) return;

        var preset = presets.Draft(chosen);

        preset.Background.Color = BackgroundColor;
        preset.Background.Opacity = BackgroundOpacity / 100;
        preset.Border.Color = BorderColor;
        preset.Border.Opacity = BorderOpacity / 100;
        preset.Border.Thickness = BorderThickness;
        preset.Border.CornerRadius = CornerRadius;

        Changed?.Invoke(WidgetChange.Dress);
    }

    partial void OnLayerChanged(double value) => Apply();

    /// <summary>Which widget wins where two overlap. A double because that is what a Slider binds to; the
    /// definition keeps it as the whole number it really is.</summary>
    [ObservableProperty] private double _layer;

    [ObservableProperty] private double _widgetWidth = 170;
    [ObservableProperty] private double _widgetHeight = 190;

    /// <summary>Where the widget sits on its screen's canvas, in DIPs. Editable as numbers as well as by
    /// dragging: it is how a widget stranded off-screen is fetched back, and how someone laying widgets out
    /// deliberately gets them to line up.</summary>
    [ObservableProperty] private double _widgetX;
    [ObservableProperty] private double _widgetY;

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

    /// <summary>Whether this kind reads anything at all. A backdrop does not, and neither will a label —
    /// showing them a sensor list would be offering a choice with nowhere to go.</summary>
    public bool ShowsDataSection => SelectedKind is { MaxSeries: > 0 };

    partial void OnCaptionChanged(string value) => Apply();
    partial void OnTextSizeChanged(double value) => Apply();
    partial void OnSelectedClockFormatChanged(ClockFormat value) => Apply();

    /// <summary>The words a Text widget says, and how any piece made of words is drawn.</summary>
    [ObservableProperty] private string _caption = string.Empty;
    [ObservableProperty] private double _textSize = 18;
    [ObservableProperty] private ClockFormat _selectedClockFormat = ClockFormat.Time;

    public IReadOnlyList<ClockFormat> ClockFormats { get; } = Enum.GetValues<ClockFormat>();

    /// <summary>Whether this kind is made of words at all — a caption, a clock, or both.</summary>
    public bool ShowsTextOptions => ShowsCaptionOptions || ShowsClockOptions;

    public bool ShowsCaptionOptions => SelectedKind?.HasCaption == true;
    public bool ShowsClockOptions => SelectedKind?.HasClock == true;

    /// <summary>Whether this kind draws a trend, and so has a window and a scale worth asking about.</summary>
    public bool ShowsChartData => SelectedKind?.HasLine == true;

    partial void OnPointCountChanged(double value) => Apply();
    partial void OnScaleMinChanged(double value) => Apply();
    partial void OnScaleMaxChanged(double value) => Apply();

    partial void OnSelectedScaleChanged(ChartScale value)
    {
        // Switching to Manual with the scale still at its defaults would put a temperature on a 0-100 axis
        // and leave the user to work out sensible ends from scratch. Seeding from what the sensor has
        // actually been observed to do gives them something to adjust instead of something to invent.
        if (value == ChartScale.Manual && ScaleMin == 0 && ScaleMax == 100 &&
            Chosen.FirstOrDefault()?.Sensor.Sensor is { Min: { } low, Max: { } high } && high > low)
        {
            var headroom = (high - low) * 0.1;
            ScaleMin = Math.Floor(low - headroom);
            ScaleMax = Math.Ceiling(high + headroom);
        }

        Apply();
    }

    /// <summary>How much time the chart covers. A double because that is what a Slider binds to; one
    /// reading arrives per poll tick, so the number is also the trend's length in seconds.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(WindowLabel))]
    private double _pointCount = 60;

    /// <summary>Seconds up to two minutes, minutes past that — nobody reads "600 s" as ten minutes.</summary>
    public string WindowLabel => PointCount < 120
        ? $"{PointCount:F0} s"
        : $"{PointCount / 60:0.#} min";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ScaleIsManual))]
    private ChartScale _selectedScale = ChartScale.FromZero;

    [ObservableProperty] private double _scaleMin;
    [ObservableProperty] private double _scaleMax = 100;

    public IReadOnlyList<ChartScale> Scales { get; } = Enum.GetValues<ChartScale>();

    /// <summary>Only Manual has ends to type in — the other two work them out.</summary>
    public bool ScaleIsManual => SelectedScale == ChartScale.Manual;

    /// <summary>How many more readings this kind will take, said plainly.</summary>
    public string SeriesHint => SelectedKind is null or { MaxSeries: 0 }
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

        Chosen.Add(new SeriesRow(SelectedSensor, RampNames, DefaultRamp(Chosen.Count), OnSeriesEdited));

        // Offered, not imposed: an untitled widget takes the sensor's name the first time it is given one,
        // and keeps whatever it has after that — including nothing, if that is what the user chose.
        if (string.IsNullOrWhiteSpace(Title)) Title = SelectedSensor.Name;

        OnSeriesEdited();
    }

    [RelayCommand]
    private void RemoveSeries(SeriesRow? row)
    {
        if (row is null) return;
        Chosen.Remove(row);
        OnSeriesEdited();
    }

    /// <summary>
    /// What a new reading is coloured by before the user says otherwise.
    ///
    /// One reading means one line, so it follows its own value — the same behaviour the dashboard's
    /// temperatures have, and what someone adding a gauge expects without hunting for a setting.
    ///
    /// More than one, and they take separate roles instead. Two lines both following their readings would
    /// be the same colour whenever both were in the same state, and two lines in one colour are one line —
    /// which is the reason this method spread them across roles in the first place.
    /// </summary>
    private string DefaultRamp(int index) =>
        index < RampNames.Count ? RampNames[index] : Ramp.Primary;

    /// <summary>The ramps the chosen preset declares, in order.</summary>
    public IReadOnlyList<string> RampNames =>
        SelectedPreset is { } preset ? [.. preset.Ramps.Keys] : [Ramp.Primary];

    /// <summary>The colours a given reading may be drawn in. "By reading" is withheld from anything with no
    /// scale to follow — offering a choice that silently does nothing is worse than not offering it.</summary>


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
        OnPropertyChanged(nameof(ShowsDataSection));
        OnPropertyChanged(nameof(ShowsTextOptions));
        OnPropertyChanged(nameof(ShowsCaptionOptions));
        OnPropertyChanged(nameof(ShowsClockOptions));
        OnPropertyChanged(nameof(ShowsChartData));
        OnPropertyChanged(nameof(RampNames));
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

        // A kind that reads nothing exists to sit behind, so it starts behind. Without this a backdrop
        // arrives on layer 0 like everything else and, being the most recently added, covers the widgets
        // it was meant to back — which is the first thing anyone would try and the first thing to go wrong.
        if (_loaded && value is { MaxSeries: 0 } && Layer >= 0) Layer = -1;

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
            Ramp = row.Ramp,
            Metric = row.Sensor.Entry.Key.ToString(),
        })];
        // Written as given, empty included. Clearing it used to bring the sensor's name back, so a widget
        // could never be untitled — which the Text and Clock kinds need, and which a gauge under a Backdrop
        // that already names the group is better off without. The name is offered when a reading is first
        // added instead: a suggestion at the moment it is useful, not a floor the user cannot get below.
        _target.Title = Title.Trim();

        _target.Preset = SelectedPreset?.Id ?? string.Empty;

        _target.Width = WidgetWidth;
        _target.Height = WidgetHeight;
        _target.X = WidgetX;
        _target.Y = WidgetY;
        _target.ZIndex = (int)Layer;

        _target.Text = Caption;
        _target.ClockFormat = SelectedClockFormat;

        _target.PointCount = (int)PointCount;
        _target.Scale = SelectedScale;
        _target.ScaleMin = ScaleMin;
        _target.ScaleMax = ScaleMax;

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
                    _allSensors.Add(new SensorOption(
                        component.Name, entry.Label, entry.Sensor.Identifier!, entry.Sensor, entry));

        ApplyFilter();

        _target = target;

        // Before the rows, which offer this preset's ramps. Set after them and every row would be listing
        // the ramps of whichever widget was open before.
        SelectedPreset = presets.For(target.Preset);

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

            Chosen.Add(new SeriesRow(sensor, RampNames,
                RampNames.Contains(series.Ramp) ? series.Ramp : DefaultRamp(Chosen.Count),
                OnSeriesEdited));
        }

        // Pinned from the Explorer: the sensor is already decided, so add it rather than making the user
        // find it again in a list of two hundred.
        if (Chosen.Count == 0 && !string.IsNullOrEmpty(presetSensorIdentifier))
        {
            var pinned = _allSensors.FirstOrDefault(s => s.Identifier == presetSensorIdentifier);
            if (pinned is not null)
                Chosen.Add(new SeriesRow(pinned, RampNames, DefaultRamp(0), OnSeriesEdited));
        }

        Caption = target.Text;
        SelectedClockFormat = target.ClockFormat;

        PointCount = target.PointCount;
        SelectedScale = target.Scale;
        ScaleMin = target.ScaleMin;
        ScaleMax = target.ScaleMax;

        // The setters above each fire a rebuild; letting them run only after this point means one preview
        // on open instead of a dozen. The first one is raised by the window once it has subscribed.
        _loaded = true;

        // The form is gated on this: the panel spends part of its life pointed at nothing, and everything
        // below the header is hidden until it is pointed at something.
        OnPropertyChanged(nameof(HasTarget));

        // Chosen is refilled above, after the kind was set, so anything reading both is out of date by now.
        NotifySeriesState();
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
}

/// <summary>
/// One chosen reading in the editor, with its colour. Observable because the colour combo edits it in
/// place; the callback is how a change reaches the widget on the desktop without the row needing to know
/// what a widget is.
/// </summary>
public partial class SeriesRow : ObservableObject
{
    private readonly Action _changed;

    public SeriesRow(SensorOption sensor, IReadOnlyList<string> ramps, string ramp, Action changed)
    {
        Sensor = sensor;
        Ramps = ramps;
        _ramp = ramp;
        _changed = changed;
    }

    public SensorOption Sensor { get; }

    /// <summary>The ramps the chosen preset offers. Names rather than colours: which colour a ramp shows
    /// depends on how the reading is doing, so there is nothing fixed to put in a swatch.</summary>
    public IReadOnlyList<string> Ramps { get; }

    [ObservableProperty] private string _ramp;

    partial void OnRampChanged(string value) => _changed();
}
