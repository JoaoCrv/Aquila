using System.Collections.ObjectModel;
using System.Windows.Media;
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

    /// <summary>
    /// Every preset a widget can wear. Straight from the service, so one written into the folder by hand
    /// appears here without a second list to keep in step.
    ///
    /// Copied on the way out, and that copy is load-bearing. Handing back the service list itself gave the
    /// same reference every time, so raising PropertyChanged told WPF nothing had changed and ItemsSource
    /// was never reapplied — a deleted preset stayed in the picker and the button looked broken.
    /// </summary>
    public IReadOnlyList<Preset> Presets => [.. presets.Presets];

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

    /// <summary>Whether the chosen preset can be written over. A built-in never can, which is what
    /// guarantees there is always something to go back to.</summary>
    public bool CanUpdatePreset => SelectedPreset is { IsBuiltIn: false };

    public string PresetNote => SelectedPreset is not { } preset
        ? string.Empty
        : preset.IsBuiltIn
            ? "Built in, so it cannot be changed. Edit freely — you are asked where to keep it when you finish."
            : $"Every widget wearing {preset.Name} follows what you change here.";

    /// <summary>Raised when a preset is removed, so the host can put the widgets wearing it back on the
    /// default — something the panel has no way to reach on its own.</summary>
    public event Action<Preset>? PresetRemoved;

    /// <summary>
    /// Removes a preset from the list.
    ///
    /// Takes the preset as a parameter rather than acting on the selected one, which is the whole reason
    /// the button lives in the list: tidying away an old variant should not require wearing it first, and
    /// a delete button beside the picker would change what the open widget looks like on the way past.
    /// </summary>
    [RelayCommand]
    private void DeletePreset(Preset? preset)
    {
        if (preset is null || preset.IsBuiltIn) return;

        // Deleting some other variant must not re-dress the widget whose panel is open, so what it was
        // wearing is put back afterwards — reapplying ItemsSource can clear the selection on the way past,
        // and a cleared selection would write itself into the widget.
        var keep = ReferenceEquals(preset, SelectedPreset) ? presets.For(null) : SelectedPreset;

        PresetRemoved?.Invoke(preset);

        OnPropertyChanged(nameof(Presets));
        SelectedPreset = keep;
    }

    /// <summary>
    /// Makes a new preset from the one in use and puts this widget in it.
    ///
    /// An action rather than a promise about what Save will do. A tick-box said "this becomes something
    /// else later", so in between the panel claimed you were editing Ember while you were not. Pressing
    /// this, the new preset exists now, is selected now, and everything after goes into it.
    /// </summary>
    [RelayCommand]
    private void NewPreset()
    {
        if (SelectedPreset is not { } source) return;

        // Cloned from the LIVE preset, so whatever has already been changed this session comes along.
        // Someone who spent ten minutes on colours and only then decided to keep them separately should
        // not lose the ten minutes.
        var copy = presets.Duplicate(source);

        // The list first. It gained a member and the picker is bound to a plain list, which cannot say so
        // itself — and re-reading it can clear the selection, so the selection is set after, not before.
        OnPropertyChanged(nameof(Presets));
        SelectedPreset = copy;
    }

    /// <summary>What the preset is called. Renaming changes the name alone — the id it is stored under
    /// never moves, because that is what the widgets reference.</summary>
    [ObservableProperty] private string _presetName = string.Empty;

    partial void OnPresetNameChanged(string value) => Dress();

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

    // Which sections apply. Asked of the CATALOG rather than of the piece, so the editor knows what to
    // show before it has built anything — and a kind that is not a dial simply cannot be given dial
    // settings, which no switch over kinds could promise.
    public bool ShowsDial => SelectedKind?.HasDial == true;
    public bool ShowsLine => SelectedKind?.HasLine == true;
    public bool ShowsBar => SelectedKind?.HasBar == true;
    public bool ShowsNumber => SelectedKind?.HasNumber == true;

    /// <summary>Whether this kind draws a reading at all, and so whether a size for one means anything.</summary>
    public bool ShowsValue => SelectedKind?.HasValue == true;

    /// <summary>Whether the reading can be turned off. Only where the graphic still says something without
    /// it — a dial or a bar. Hiding the words of a Text widget leaves an empty widget.</summary>
    public bool CanHideValue => ShowsDial || ShowsBar;

    // --- Dial ---

    [ObservableProperty] private double _arcThickness = 14;
    [ObservableProperty] private double _arcCorner;
    [ObservableProperty] private GaugeSweep _sweep = GaugeSweep.Dial;
    [ObservableProperty] private string _trackColor = "#FFFFFF";
    [ObservableProperty] private double _trackOpacity = 8;

    public IReadOnlyList<GaugeSweep> Sweeps { get; } = Enum.GetValues<GaugeSweep>();

    partial void OnArcThicknessChanged(double value) => Dress();
    partial void OnArcCornerChanged(double value) => Dress();
    partial void OnSweepChanged(GaugeSweep value) => Dress();
    partial void OnTrackColorChanged(string value) => Dress();
    partial void OnTrackOpacityChanged(double value) => Dress();

    [RelayCommand] private void PickTrack(string? hex) => TrackColor = hex ?? TrackColor;

    // --- Line ---

    [ObservableProperty] private double _lineThickness = 1.5;
    [ObservableProperty] private ChartFill _lineFill = ChartFill.Gradient;
    [ObservableProperty] private double _fillOpacity = 30;
    [ObservableProperty] private double _smoothness = 50;
    [ObservableProperty] private double _pointSize;

    public IReadOnlyList<ChartFill> Fills { get; } = Enum.GetValues<ChartFill>();

    partial void OnLineThicknessChanged(double value) => Dress();
    partial void OnLineFillChanged(ChartFill value) => Dress();
    partial void OnFillOpacityChanged(double value) => Dress();
    partial void OnSmoothnessChanged(double value) => Dress();
    partial void OnPointSizeChanged(double value) => Dress();

    // --- Bar ---

    [ObservableProperty] private double _barThickness = 6;
    [ObservableProperty] private double _barCorner = 3;
    [ObservableProperty] private MeterLayout _barLayout = MeterLayout.Beside;

    public IReadOnlyList<MeterLayout> Layouts { get; } = Enum.GetValues<MeterLayout>();

    partial void OnBarThicknessChanged(double value) => Dress();
    partial void OnBarCornerChanged(double value) => Dress();
    partial void OnBarLayoutChanged(MeterLayout value) => Dress();

    // --- Number ---

    [ObservableProperty] private double _unitSize = 13;
    [ObservableProperty] private bool _numberPanel;

    partial void OnUnitSizeChanged(double value) => Dress();
    partial void OnNumberPanelChanged(bool value) => Dress();

    // --- Words: the typeface, the label, and the reading ---

    /// <summary>Stands for "whatever the theme uses". A real family name here would override the theme
    /// instead of deferring to it, and the two are not the same answer.</summary>
    public const string ThemeFont = "Theme default";

    public IReadOnlyList<string> FontFamilies { get; } =
        [ThemeFont, .. Fonts.SystemFontFamilies.Select(f => f.Source).Distinct().OrderBy(n => n)];

    [ObservableProperty] private string _fontFamily = ThemeFont;
    [ObservableProperty] private double _titleSize = 11;
    [ObservableProperty] private double _titleOpacity = 60;
    [ObservableProperty] private TitlePlacement _titlePlacement = TitlePlacement.Top;
    [ObservableProperty] private double _valueSize = 18;
    [ObservableProperty] private TextAlign _valueAlign = TextAlign.Center;
    [ObservableProperty] private bool _valueShown = true;

    public IReadOnlyList<TitlePlacement> Placements { get; } = Enum.GetValues<TitlePlacement>();
    public IReadOnlyList<TextAlign> Alignments { get; } = Enum.GetValues<TextAlign>();

    partial void OnFontFamilyChanged(string value) => Dress();
    partial void OnTitleSizeChanged(double value) => Dress();
    partial void OnTitleOpacityChanged(double value) => Dress();
    partial void OnTitlePlacementChanged(TitlePlacement value) => Dress();
    partial void OnValueSizeChanged(double value) => Dress();
    partial void OnValueAlignChanged(TextAlign value) => Dress();
    partial void OnValueShownChanged(bool value) => Dress();

    /// <summary>True while the form is being filled FROM a preset, so the writes that causes are not read
    /// back as edits. Without it, merely switching preset would mark the new one as edited — it would be
    /// written with its own values, which changes nothing and still ends in being asked to save it.</summary>
    private bool _reading;

    /// <summary>Fills the appearance controls from a preset without touching it.</summary>
    private void Read(Preset? preset)
    {
        OnPropertyChanged(nameof(CanUpdatePreset));
        OnPropertyChanged(nameof(PresetNote));

        if (preset is null) return;

        _reading = true;
        try
        {
            PresetName = preset.Name;

            BackgroundColor = preset.Background.Color;
            BackgroundOpacity = preset.Background.Opacity * 100;
            BorderColor = preset.Border.Color;
            BorderOpacity = preset.Border.Opacity * 100;
            BorderThickness = preset.Border.Thickness;
            CornerRadius = preset.Border.CornerRadius;

            ArcThickness = preset.Gauge.Thickness;
            ArcCorner = preset.Gauge.Corner;
            Sweep = preset.Gauge.Sweep;
            TrackColor = preset.Gauge.Track.Color;
            TrackOpacity = preset.Gauge.Track.Opacity * 100;

            LineThickness = preset.Line.Thickness;
            LineFill = preset.Line.Fill;
            FillOpacity = preset.Line.FillOpacity * 100;
            Smoothness = preset.Line.Smoothness * 100;
            PointSize = preset.Line.PointSize;

            BarThickness = preset.Bar.Thickness;
            BarCorner = preset.Bar.Corner;
            BarLayout = preset.Bar.Layout;

            UnitSize = preset.Number.UnitSize;
            NumberPanel = preset.Number.Panel;

            FontFamily = preset.FontFamily ?? ThemeFont;
            TitleSize = preset.Title.Size;
            TitleOpacity = preset.Title.Opacity * 100;
            TitlePlacement = preset.Title.Placement;
            ValueSize = preset.Value.Size;
            ValueAlign = preset.Value.Align;
            ValueShown = preset.Value.Show;
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

        // Blank is never a name. Cleared, the box would rename the preset to nothing on the way past.
        if (!string.IsNullOrWhiteSpace(PresetName)) preset.Name = PresetName.Trim();

        preset.Background.Color = BackgroundColor;
        preset.Background.Opacity = BackgroundOpacity / 100;
        preset.Border.Color = BorderColor;
        preset.Border.Opacity = BorderOpacity / 100;
        preset.Border.Thickness = BorderThickness;
        preset.Border.CornerRadius = CornerRadius;

        preset.Gauge.Thickness = ArcThickness;
        preset.Gauge.Corner = ArcCorner;
        preset.Gauge.Sweep = Sweep;
        preset.Gauge.Track.Color = TrackColor;
        preset.Gauge.Track.Opacity = TrackOpacity / 100;

        preset.Line.Thickness = LineThickness;
        preset.Line.Fill = LineFill;
        preset.Line.FillOpacity = FillOpacity / 100;
        preset.Line.Smoothness = Smoothness / 100;
        preset.Line.PointSize = PointSize;

        preset.Bar.Thickness = BarThickness;
        preset.Bar.Corner = BarCorner;
        preset.Bar.Layout = BarLayout;

        preset.Number.UnitSize = UnitSize;
        preset.Number.Panel = NumberPanel;

        // Null rather than the label, so a preset that defers to the theme says so in the file instead of
        // freezing today's theme font into itself.
        preset.FontFamily = FontFamily == ThemeFont ? null : FontFamily;
        preset.Title.Size = TitleSize;
        preset.Title.Opacity = TitleOpacity / 100;
        preset.Title.Placement = TitlePlacement;
        preset.Value.Size = ValueSize;
        preset.Value.Align = ValueAlign;
        preset.Value.Show = ValueShown;

        OnPropertyChanged(nameof(PresetNote));
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
    partial void OnSelectedClockFormatChanged(ClockFormat value) => Apply();

    /// <summary>The words a Text widget says. How they are DRAWN is the preset's.</summary>
    [ObservableProperty] private string _caption = string.Empty;
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
        OnPropertyChanged(nameof(ShowsDial));
        OnPropertyChanged(nameof(ShowsLine));
        OnPropertyChanged(nameof(ShowsBar));
        OnPropertyChanged(nameof(ShowsNumber));
        OnPropertyChanged(nameof(ShowsValue));
        OnPropertyChanged(nameof(CanHideValue));
        OnPropertyChanged(nameof(RampNames));
        OnPropertyChanged(nameof(PanelTitle));
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
    partial void OnTitleChanged(string value)
    {
        OnPropertyChanged(nameof(PanelTitle));
        Apply();
    }

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

    /// <summary>
    /// The widget the panel is pointed at, in the words the widget itself uses.
    ///
    /// The heading used to read "Widget" whatever was open, and the subtitle the same sentence for all of
    /// them — so a panel docked at the edge of a desktop holding six widgets never said which one it was
    /// editing. Falls back to the kind's name, because a title is allowed to be empty and a heading is not.
    /// </summary>
    public string PanelTitle =>
        _target is null ? "Widget"
        : !string.IsNullOrWhiteSpace(Title) ? Title.Trim()
        : SelectedKind?.Name ?? "Widget";

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
        OnPropertyChanged(nameof(PanelTitle));
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

        // Cleared, or the list keeps the row highlighted from the widget before — and since this is what
        // decides whether Add is offered and what it is called, the panel goes on looking like it is still
        // pointed at the previous widget.
        SelectedSensor = null;

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
