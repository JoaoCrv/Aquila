using System.Collections.ObjectModel;
using System.Windows.Media;
using Aquila.Helpers;
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


/// <summary>One ramp, flattened for display. A record rather than the Ramp itself so the summary cannot
/// accidentally become a second way to edit one.</summary>
/// <summary>
/// One ramp in the editor: four stops, or one when it is fixed.
///
/// "Fixed colour" writes the same colour into all four stops rather than adding a mode. That keeps the
/// format's own rule intact — a colour that never changes IS a ramp that goes nowhere — so nothing
/// downstream needs to know the difference, and turning it back off leaves four stops to spread again.
/// </summary>
public partial class RampRow : ObservableObject
{
    private readonly Action<RampRow> _changed;
    private readonly Action<RampRow> _delete;
    private readonly Func<string, string, bool> _rename;

    /// <summary>The name this row currently answers to. Kept beside Name because a rename arrives as "it
    /// is now X" and the preset has to be told what it was called BEFORE — and because a refused rename
    /// has to be put back.</summary>
    private string _committed;
    private bool _renaming;

    public RampRow(string name, Ramp ramp, Action<RampRow> changed, Action<RampRow> delete,
                   Func<string, string, bool> rename)
    {
        _name = name;
        _committed = name;
        _changed = changed;
        _delete = delete;
        _rename = rename;

        _normal = ramp.Normal;
        _elevated = ramp.Elevated;
        _alert = ramp.Alert;
        _critical = ramp.Critical;

        // Read from the colours themselves rather than stored: four equal stops is what fixed MEANS, so
        // there is no second fact that could disagree with them.
        _fixed = ramp.Normal == ramp.Elevated && ramp.Elevated == ramp.Alert && ramp.Alert == ramp.Critical;
    }

    /// <summary>
    /// The ramp's name, and the key it is stored under — there is no separate label.
    ///
    /// A display name over a stable key was the other way to do this and was not worth it: it puts two
    /// names on one thing, and the one the user reads stops being the one the file holds. A ramp's name
    /// IS how a series points at it, so renaming is renaming an identifier and every reference moves with
    /// it. DesktopWidgetService does the moving.
    /// </summary>
    [ObservableProperty] private string _name;

    /// <summary>Primary is where every fallback ends, so it is the one ramp that cannot be removed — nor
    /// renamed, which would be the same removal with extra steps.</summary>
    public bool CanDelete => !string.Equals(_committed, Ramp.Primary, StringComparison.OrdinalIgnoreCase);

    public bool CanRename => CanDelete;

    /// <summary>
    /// Takes the typed name to the preset, and puts it back if it is refused.
    ///
    /// Blank and already-taken are both ordinary things to be holding for a moment in a text box, so they
    /// are answered by restoring the old name rather than by an error. The guard is what stops that
    /// restore from arriving back here as another rename.
    /// </summary>
    partial void OnNameChanged(string value)
    {
        if (_renaming) return;

        _renaming = true;
        try
        {
            if (_rename(_committed, value)) _committed = value.Trim();
            Name = _committed;
        }
        finally
        {
            _renaming = false;
        }
    }

    [ObservableProperty] private string _normal;
    [ObservableProperty] private string _elevated;
    [ObservableProperty] private string _alert;
    [ObservableProperty] private string _critical;
    [ObservableProperty] private bool _fixed;

    public bool Varies => !Fixed;

    partial void OnNormalChanged(string value)
    {
        if (Fixed) Flatten();
        _changed(this);
    }

    partial void OnElevatedChanged(string value) => _changed(this);
    partial void OnAlertChanged(string value) => _changed(this);
    partial void OnCriticalChanged(string value) => _changed(this);

    partial void OnFixedChanged(bool value)
    {
        OnPropertyChanged(nameof(Varies));
        if (value) Flatten();
        _changed(this);
    }

    private void Flatten()
    {
        Elevated = Normal;
        Alert = Normal;
        Critical = Normal;
    }

    [RelayCommand]
    private void Delete() => _delete(this);
}

public record RampPreview(string Name, string Normal, string Elevated, string Alert, string Critical);

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

    /// <summary>
    /// Whether the panel is showing the preset instead of the widget.
    ///
    /// One panel, two views, because it was doing two jobs at once and saying so with decoration —
    /// headings, then rules, then cards — none of which made the scope unmistakable. Sliding to a view of
    /// its own does: on the left you are editing this widget, on the right you are editing something every
    /// widget wearing it shares. The boundary stops needing to be marked when it becomes the navigation.
    /// </summary>
    [ObservableProperty] private bool _editingPreset;

    /// <summary>Which view is on screen. Both carry HasTarget, because a panel pointed at nothing shows
    /// neither — an editor full of controls that change nothing is worse than an empty panel saying so.</summary>
    public bool ShowsWidgetView => HasTarget && !EditingPreset;

    public bool ShowsPresetView => HasTarget && EditingPreset;

    /// <summary>
    /// Pointed at nothing, and not part-way through a preset. The panel says so, and offers the one thing
    /// that means something here: a large Add in the middle.
    ///
    /// That Add is load-bearing. Adding was once hidden along with the form, and the panel could then edit
    /// a widget and remove one but never make one — the only way to a first widget was the Widgets page.
    /// The header's add and remove show only while a widget is open, so this is the Add for every other
    /// moment, and it must never be made to depend on a selection.
    /// </summary>
    public bool ShowsNothing => !HasTarget && !EditingPreset;

    partial void OnEditingPresetChanged(bool value) => NotifyView();

    private void NotifyView()
    {
        OnPropertyChanged(nameof(ShowsWidgetView));
        OnPropertyChanged(nameof(ShowsPresetView));
        OnPropertyChanged(nameof(ShowsNothing));
        OnPropertyChanged(nameof(PanelTitle));
        OnPropertyChanged(nameof(PanelSubtitle));
    }

    [RelayCommand]
    private void EditPreset() => EditingPreset = SelectedPreset is not null;

    [RelayCommand]
    private void ClosePreset() => EditingPreset = false;

    /// <summary>How many widgets wear the chosen preset. Supplied by the host, which is the only thing that
    /// knows the layout — and shown BEFORE the preset is opened, because the number is the whole reason to
    /// hesitate before changing one.</summary>
    public Func<string, int>? CountWearers { get; set; }

    public string WornBy => SelectedPreset is not { } preset
        ? string.Empty
        : (CountWearers?.Invoke(preset.Id) ?? 0) switch
        {
            0 => "Worn by nothing yet",
            1 => "Worn by 1 widget",
            var n => $"Worn by {n} widgets",
        };

    /// <summary>Whether this preset is the one new widgets wear. Shown rather than acted on twice: the
    /// menu entry is pointless when it already is.</summary>
    public bool IsDefaultPreset =>
        SelectedPreset is { } preset &&
        string.Equals(preset.Id, presets.DefaultId, StringComparison.OrdinalIgnoreCase);

    [RelayCommand]
    private void SetAsDefault()
    {
        if (SelectedPreset is not { } preset) return;

        presets.SetDefault(preset);
        OnPropertyChanged(nameof(IsDefaultPreset));
        OnPropertyChanged(nameof(PresetNote));
    }

    /// <summary>
    /// What a preset contains, at a glance: every ramp, end to end.
    ///
    /// A summary rather than a sample of one colour. A preset IS its ramps — the rest is measurement — so
    /// four swatches per ramp says more about what you are about to wear than any single square could.
    /// </summary>
    public IReadOnlyList<RampPreview> RampPreviews =>
        SelectedPreset is not { } preset
            ? []
            : [.. preset.Ramps.Select(r =>
                new RampPreview(r.Key, r.Value.Normal, r.Value.Elevated, r.Value.Alert, r.Value.Critical))];

    /// <summary>
    /// What the type picker's previews read: the CPU's two temperatures.
    ///
    /// One sample for every kind, not one chosen per kind — each takes as many as it can draw, so a gauge
    /// shows the first and the chart draws both, and a new kind needs nothing added here. The preview's job
    /// is to show the KIND; the reading this widget actually has is right below it, in Data.
    ///
    /// Only two, because that is what the model holds: CpuTemperatureNode has a Primary and a Secondary and
    /// no per-core list, unlike load. So Bars previews as two bars rather than a row — honest, if a smaller
    /// picture of what it is for, and padding the row with loads would put °C and % side by side in one
    /// preview and teach the wrong thing about it.
    ///
    /// Temperature rather than load, and that was learned by looking: at rest a core's load sits at two or
    /// three percent, so every dial came out empty, every bar flat and every sparkline pressed to the floor.
    /// Correct, and demonstrating nothing. A temperature sits somewhere visible and wanders.
    ///
    /// Load is the fallback, not temperature's equal: without administrator rights there ARE no
    /// temperatures, and a picker of blank previews would greet exactly the user who is already seeing less
    /// than everyone else. Readings that were never filled are left out, so no bar in the sample reads "--".
    /// </summary>
    [ObservableProperty] private IReadOnlyList<SensorNode> _previewSensors = [];

    /// <summary>The previews wear the chosen preset's primary colour at rest, so the picker looks like the
    /// widget will rather than like a catalogue in someone else's colours.</summary>
    public Brush? PreviewAccent =>
        RampPreviews.FirstOrDefault() is { } first ? HexBrush.From(first.Normal) : null;

    /// <summary>
    /// PreviewAccent is derived from RampPreviews and follows it here, in one place.
    ///
    /// RampPreviews is announced from six different edits — a ramp added, removed, renamed or recoloured,
    /// the preset swapped, a session read. Echoing the second name at each of the six is how the seventh
    /// forgets to, and the previews quietly keep last week's colour.
    /// </summary>
    protected override void OnPropertyChanged(System.ComponentModel.PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);

        if (e.PropertyName == nameof(RampPreviews))
        {
            base.OnPropertyChanged(new System.ComponentModel.PropertyChangedEventArgs(nameof(PreviewAccent)));

            // What each reading is drawn in follows the ramps too — a preset switched under it, a ramp
            // renamed out from under its name — while nothing about the reading itself changed. After the
            // list's own announcement, so the picker already holds the new list when it is asked to select.
            foreach (var row in Chosen) row.RefreshShown();
        }
    }

    /// <summary>
    /// A state to draw every widget in, regardless of what its reading actually says.
    ///
    /// Null is the normal case — the readings speak for themselves. Set, it is the only practical way to
    /// judge the critical colour: the alternative is heating the machine up on purpose and editing while
    /// it is hot.
    /// </summary>
    [ObservableProperty] private string? _previewState;

    partial void OnPreviewStateChanged(string? value) => PreviewRequested?.Invoke(value);

    public event Action<string?>? PreviewRequested;

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

    /// <summary>A file name to offer when exporting: the id, which is what the file would be called in
    /// the presets folder.</summary>
    public string ExportFileName => SelectedPreset is { } preset ? $"{preset.Id}.json" : "preset.json";

    /// <summary>Writes the preset on screen to a file, drafts and all.</summary>
    public void Export(string path)
    {
        if (SelectedPreset is { } preset) presets.Export(preset, path);
    }

    /// <summary>
    /// Reads a preset in and wears it. False when the file is not a preset.
    ///
    /// Selected afterwards, like a duplicate: importing one is asking to use it, and leaving it sitting
    /// unselected in the list would make the gesture look as though it had failed. The list is announced
    /// first — it gained a member and the picker is bound to a plain list, which cannot say so itself.
    /// </summary>
    public bool Import(string path, out bool newer)
    {
        if (presets.Import(path, out newer) is not { } imported) return false;

        OnPropertyChanged(nameof(Presets));
        SelectedPreset = imported;
        return true;
    }

    /// <summary>What the preset is called. Renaming changes the name alone — the id it is stored under
    /// never moves, because that is what the widgets reference.</summary>
    [ObservableProperty] private string _presetName = string.Empty;

    partial void OnPresetNameChanged(string value) => Dress();

    // --- Appearance. These write into the PRESET, so they change every widget wearing it. ---

    /// <summary>
    /// The colours a picker offers first: the ones this preset already uses, then a few neutrals.
    ///
    /// Its own first because most of building a preset is matching what is already in it — a second ramp
    /// that belongs beside the first, a border that picks up the background. A generic rainbow answers a
    /// question nobody asked.
    ///
    /// Rebuilt on demand rather than kept, and raised only when the preset changes rather than on every
    /// edit: a list that reshuffled itself under the pointer while a colour was being dragged would be
    /// worse than one that is a few minutes out of date.
    /// </summary>
    public IReadOnlyList<string> Swatches
    {
        get
        {
            var offered = new List<string>();

            if (SelectedPreset is { } preset)
            {
                foreach (var ramp in preset.Ramps.Values)
                    offered.AddRange([ramp.Normal, ramp.Elevated, ramp.Alert, ramp.Critical]);

                offered.Add(preset.Background.Color);
                offered.Add(preset.Border.Color);
                offered.Add(preset.Title.Color);
                offered.Add(preset.Value.Color);
            }

            offered.AddRange(Neutrals);
            return [.. offered.Distinct(StringComparer.OrdinalIgnoreCase)];
        }
    }

    private static readonly string[] Neutrals =
        ["#000000", "#1E1E1E", "#2E3B4E", "#8A8886", "#FFFFFF"];

    [ObservableProperty] private string _backgroundColor = "#000000";
    [ObservableProperty] private double _backgroundOpacity = 60;
    [ObservableProperty] private string _borderColor = "#FFFFFF";
    [ObservableProperty] private double _borderOpacity = 25;
    [ObservableProperty] private double _borderThickness;
    [ObservableProperty] private double _cornerRadius = 8;
    [ObservableProperty] private double _padding = 10;

    [ObservableProperty] private string _titleColor = "#FFFFFF";
    [ObservableProperty] private string _valueColor = "#FFFFFF";

    partial void OnBackgroundColorChanged(string value) => Dress();
    partial void OnBackgroundOpacityChanged(double value) => Dress();
    partial void OnBorderColorChanged(string value) => Dress();
    partial void OnBorderOpacityChanged(double value) => Dress();
    partial void OnBorderThicknessChanged(double value) => Dress();
    partial void OnCornerRadiusChanged(double value) => Dress();
    partial void OnPaddingChanged(double value) => Dress();

    // Which sections apply. Asked of the CATALOG rather than of the piece, so the editor knows what to
    // show before it has built anything — and a kind that is not a dial simply cannot be given dial
    // settings, which no switch over kinds could promise.
    public bool ShowsDial => SelectedKind?.HasDial == true;
    public bool ShowsLine => SelectedKind?.HasLine == true;
    public bool ShowsBar => SelectedKind?.HasBar == true;
    public bool ShowsNumber => SelectedKind?.HasNumber == true;

    /// <summary>Whether this kind draws a reading at all, and so whether a size for one means anything.</summary>
    public bool ShowsValue => SelectedKind?.HasValue == true;

    /// <summary>
    /// Whether the reading can be turned off: there has to be a reading, and something has to be left when
    /// it goes. A dial becomes a ring, a bar a bar, a sparkline a trend.
    ///
    /// The full Chart is excluded by <see cref="ShowsValue"/> — it draws no number to hide — and the stat
    /// and the two word kinds by having no graphic, so hiding the reading would leave an empty widget.
    /// </summary>
    public bool CanHideValue => ShowsValue && (ShowsDial || ShowsBar || ShowsLine);

    /// <summary>Whether this kind's reading has a unit to hide. The clock draws a reading and has none.
    /// </summary>
    public bool ShowsUnit => SelectedKind?.HasUnit == true;

    /// <summary>Whether this kind can be turned on its side.</summary>
    public bool ShowsDirection => SelectedKind?.HasDirection == true;

    /// <summary>Whether there is more than one reading, and so whether making them match means anything.
    /// </summary>
    public bool CanMatchRamps => Chosen.Count > 1;

    /// <summary>
    /// Gives every reading the first one's ramp.
    ///
    /// A convenience over the data, not a second place to store a ramp. A widget-level "they all share
    /// one" would be a ramp decided in two places — the widget's and each series' — and those two would
    /// have to be kept agreeing forever. This writes the answer into the rows, where a ramp already lives,
    /// and afterwards nothing can disagree because there is still only one copy of it.
    ///
    /// Sixteen bars is where it earns its place: the default gives series N the preset's Nth ramp, so an
    /// equaliser on a two-ramp preset came out with exactly one bar a different colour, which reads as a
    /// fault rather than as a design.
    /// </summary>
    [RelayCommand]
    private void MatchRamps()
    {
        if (Chosen.Count < 2) return;

        // What the first reading is SHOWN in, not what it stored: the button is pressed by someone looking
        // at the picker, and "the ramp of the first one" means the one they can see.
        var ramp = Chosen[0].Shown;
        foreach (var row in Chosen) row.SetRampQuietly(ramp);

        Apply();
    }

    public IReadOnlyList<BarDirection> BarDirections { get; } = Enum.GetValues<BarDirection>();

    // --- Colours. The only part of a preset that carries meaning rather than measurement: four stops
    // from ordinary to critical, and which one shows is decided by the reading, not by the preset. ---

    /// <summary>
    /// Every ramp the preset has, all editable at once.
    ///
    /// A picker showing one at a time hid the thing worth seeing: a preset's ramps are a system, and the
    /// second one is chosen against the first. Side by side you can tell whether they belong together,
    /// which is the whole question being asked.
    /// </summary>
    public ObservableCollection<RampRow> RampRows { get; } = [];

    /// <summary>
    /// Writes a row back into the preset it came from.
    ///
    /// Through the dictionary by name, never through RampFor — that answers with primary for a name the
    /// preset does not have, so writing through it would pour one ramp's colours into another.
    /// </summary>
    private void EditRamp(RampRow row)
    {
        if (_reading || !_loaded || SelectedPreset is not { } chosen) return;

        var preset = presets.Draft(chosen);
        if (!preset.Ramps.TryGetValue(row.Name, out var ramp)) return;

        ramp.Normal = row.Normal;
        ramp.Elevated = row.Elevated;
        ramp.Alert = row.Alert;
        ramp.Critical = row.Critical;

        OnPropertyChanged(nameof(RampPreviews));
        Changed?.Invoke(WidgetChange.Dress);
    }

    /// <summary>Rebuilds the rows from a preset. They are recreated rather than updated: a ramp added or
    /// removed changes how many there are, and a row bound to a name that no longer exists writes into
    /// nothing.</summary>
    private void ReadRamps(Preset preset)
    {
        RampRows.Clear();
        foreach (var (name, ramp) in preset.Ramps)
            RampRows.Add(new RampRow(name, ramp, EditRamp, row => DeleteRamp(row.Name), RenameRamp));
    }

    /// <summary>
    /// Renames a ramp and repoints everything that named it.
    ///
    /// The rows are deliberately NOT rebuilt. A rename arrives from the text box the user is still in, and
    /// clearing the collection underneath it would take the caret with it — so the row keeps itself and
    /// only the things that read a name from elsewhere are refreshed.
    ///
    /// The series pickers in THIS widget are repointed quietly, and before RampPreviews is announced: a
    /// list that changed first would look for the old name, find it missing, and show nothing.
    /// </summary>
    private bool RenameRamp(string from, string to)
    {
        if (_reading || !_loaded || SelectedPreset is not { } chosen) return false;
        if (string.Equals(from, Ramp.Primary, StringComparison.OrdinalIgnoreCase)) return false;

        var preset = presets.Draft(chosen);
        if (!presets.RenameRamp(preset, from, to)) return false;

        var renamed = to.Trim();
        foreach (var row in Chosen)
            if (string.Equals(row.Ramp, from, StringComparison.OrdinalIgnoreCase))
                row.SetRampQuietly(renamed);

        OnPropertyChanged(nameof(RampPreviews));
        Changed?.Invoke(WidgetChange.Dress);
        return true;
    }

    /// <summary>
    /// Adds a ramp, starting from the one on screen.
    ///
    /// A second series usually wants a variation on the first rather than an unrelated colour, and a ramp
    /// that begins as a copy is one edit away from being right — where one that begins as the default blue
    /// is four.
    /// </summary>
    [RelayCommand]
    private void AddRamp()
    {
        if (SelectedPreset is not { } chosen) return;

        var preset = presets.Draft(chosen);
        var name = FreeRampName(preset);

        // From the last one rather than from blue: a second series usually wants a variation on the
        // first, and a ramp that starts as a copy is one edit from right where a default is four.
        var last = preset.Ramps.Values.LastOrDefault() ?? Ramp.Neutral;

        preset.Ramps[name] = new Ramp
        {
            Normal = last.Normal,
            Elevated = last.Elevated,
            Alert = last.Alert,
            Critical = last.Critical,
        };

        ReadRamps(preset);
        OnPropertyChanged(nameof(RampPreviews));
        Changed?.Invoke(WidgetChange.Dress);
    }

    /// <summary>Removes a ramp. Any series naming it draws in primary from the next tick, because that is
    /// what RampFor already answers for a name a preset does not have — so nothing has to be repointed and
    /// no reading is left with nothing to be drawn in.</summary>
    private void DeleteRamp(string name)
    {
        if (SelectedPreset is not { } chosen) return;
        if (string.Equals(name, Ramp.Primary, StringComparison.OrdinalIgnoreCase)) return;

        var preset = presets.Draft(chosen);
        preset.Ramps.Remove(name);

        ReadRamps(preset);
        OnPropertyChanged(nameof(RampPreviews));
        Changed?.Invoke(WidgetChange.Dress);
    }

    private static string FreeRampName(Preset preset)
    {
        if (!preset.Ramps.ContainsKey("secondary")) return "secondary";

        for (var n = 3; ; n++)
            if (!preset.Ramps.ContainsKey($"ramp {n}")) return $"ramp {n}";
    }

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

    [ObservableProperty] private string _titleFont = ThemeFont;
    [ObservableProperty] private TextWeight _titleWeight = TextWeight.Regular;
    [ObservableProperty] private double _titleSize = 11;
    [ObservableProperty] private double _titleOpacity = 60;
    [ObservableProperty] private TitlePlacement _titlePlacement = TitlePlacement.Top;
    [ObservableProperty] private TextAlign _titleAlign = TextAlign.Center;
    [ObservableProperty] private string _valueFont = ThemeFont;
    [ObservableProperty] private TextWeight _valueWeight = TextWeight.Regular;
    [ObservableProperty] private double _valueSize = 18;
    [ObservableProperty] private TextAlign _valueAlign = TextAlign.Center;
    [ObservableProperty] private bool _valueShown = true;
    [ObservableProperty] private bool _unitShown = true;
    [ObservableProperty] private BarDirection _barDirection = BarDirection.Vertical;

    public IReadOnlyList<TitlePlacement> Placements { get; } = Enum.GetValues<TitlePlacement>();
    public IReadOnlyList<TextWeight> Weights { get; } = Enum.GetValues<TextWeight>();
    public IReadOnlyList<TextAlign> Alignments { get; } = Enum.GetValues<TextAlign>();

    partial void OnTitleAlignChanged(TextAlign value) => Dress();
    partial void OnTitleColorChanged(string value) => Dress();
    partial void OnValueColorChanged(string value) => Dress();
    partial void OnTitleFontChanged(string value) => Dress();
    partial void OnTitleWeightChanged(TextWeight value) => Dress();
    partial void OnValueFontChanged(string value) => Dress();
    partial void OnValueWeightChanged(TextWeight value) => Dress();
    partial void OnTitleSizeChanged(double value) => Dress();
    partial void OnTitleOpacityChanged(double value) => Dress();
    partial void OnTitlePlacementChanged(TitlePlacement value) => Dress();
    partial void OnValueSizeChanged(double value) => Dress();
    partial void OnValueAlignChanged(TextAlign value) => Dress();
    partial void OnValueShownChanged(bool value) => Apply();
    partial void OnUnitShownChanged(bool value) => Apply();
    partial void OnBarDirectionChanged(BarDirection value) => Apply();

    /// <summary>True while the form is being filled FROM a preset, so the writes that causes are not read
    /// back as edits. Without it, merely switching preset would mark the new one as edited — it would be
    /// written with its own values, which changes nothing and still ends in being asked to save it.</summary>
    private bool _reading;

    /// <summary>Fills the appearance controls from a preset without touching it.</summary>
    private void Read(Preset? preset)
    {
        OnPropertyChanged(nameof(CanUpdatePreset));
        OnPropertyChanged(nameof(PresetNote));
        OnPropertyChanged(nameof(Swatches));
        OnPropertyChanged(nameof(WornBy));
        OnPropertyChanged(nameof(PanelTitle));
        OnPropertyChanged(nameof(IsDefaultPreset));
        OnPropertyChanged(nameof(RampPreviews));

        if (preset is null) return;

        _reading = true;
        try
        {
            PresetName = preset.Name;

            BackgroundColor = preset.Background.Color;
            BackgroundOpacity = preset.Background.Opacity * 100;
            BorderColor = preset.Border.Color;
            Padding = preset.Border.Padding;
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

            ReadRamps(preset);

            TitleColor = preset.Title.Color;
            TitleAlign = preset.Title.Align;
            TitleFont = preset.Title.FontFamily ?? ThemeFont;
            TitleWeight = preset.Title.Weight;
            TitleSize = preset.Title.Size;
            TitleOpacity = preset.Title.Opacity * 100;
            TitlePlacement = preset.Title.Placement;
            ValueColor = preset.Value.Color;
            ValueFont = preset.Value.FontFamily ?? ThemeFont;
            ValueWeight = preset.Value.Weight;
            ValueSize = preset.Value.Size;
            ValueAlign = preset.Value.Align;
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
        preset.Border.Padding = Padding;
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
        preset.Title.Color = TitleColor;
        preset.Title.Align = TitleAlign;
        preset.Title.FontFamily = TitleFont == ThemeFont ? null : TitleFont;
        preset.Title.Weight = TitleWeight;
        preset.Title.Size = TitleSize;
        preset.Title.Opacity = TitleOpacity / 100;
        preset.Title.Placement = TitlePlacement;
        preset.Value.Color = ValueColor;
        preset.Value.FontFamily = ValueFont == ThemeFont ? null : ValueFont;
        preset.Value.Weight = ValueWeight;
        preset.Value.Size = ValueSize;
        preset.Value.Align = ValueAlign;

        OnPropertyChanged(nameof(PresetNote));
        OnPropertyChanged(nameof(RampPreviews));
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
                : $"Pick a sensor above. Up to {SelectedKind.MaxSeries}.";

    /// <summary>
    /// The button says what pressing it will do, so the two behaviours are never a surprise.
    ///
    /// "Use this sensor", naming the thing the press acts on — the one picked in the list above — rather than
    /// a bare "Add", which left the reader to work out what was being added and to where. The difference
    /// between adding and replacing is carried by one word, "instead", which is only offered when there is
    /// really something to replace (see ReplacesSeries): a one-reading widget that has none yet is simply
    /// being given its first.
    /// </summary>
    public string AddLabel => ReplacesSeries ? "Use this sensor instead" : "Use this sensor";

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

        Chosen.Add(new SeriesRow(SelectedSensor, DefaultRamp(Chosen.Count), OnSeriesEdited, ShownItem));

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
        SelectedPreset?.RampNameFor(null, index) ?? Ramp.Primary;

    /// <summary>The ramp a row is actually drawn in under the chosen preset. Asked of the same rule the
    /// renderer asks, <see cref="Preset.RampNameFor"/>, so the picker shows what the desktop shows — it
    /// used to be a copy of that rule, kept in step by a comment.</summary>
    private string? ShownRamp(SeriesRow row) =>
        SelectedPreset is { } preset
            ? preset.RampNameFor(row.Ramp, Math.Max(0, Chosen.IndexOf(row)))
            : Ramp.Primary;

    /// <summary>The entry in the picker's own list for what a row is drawn in. Looked up by name in a list
    /// built afresh, which is fine: the picker matches records by value, and this one is equal to its own.</summary>
    private RampPreview? ShownItem(SeriesRow row)
    {
        var name = ShownRamp(row);
        return RampPreviews.FirstOrDefault(p => p.Name == name);
    }

    /// <summary>The colours a given reading may be drawn in. "By reading" is withheld from anything with no
    /// scale to follow — offering a choice that silently does nothing is worse than not offering it.</summary>


    private void OnSeriesEdited()
    {
        NotifySeriesState();
        Apply(WidgetChange.Structure);
    }

    /// <summary>
    /// Everything the chosen kind and the series list decide together. Raised from every place that changes
    /// either one, so the button, its label and the hint can never disagree with the list under them — four
    /// separate notifications in three places was how they drifted apart.
    ///
    /// An empty name, which WPF reads as "every property on this object", rather than the eighteen it used
    /// to name one by one. That list was a promise to remember, and it was broken the first time it was
    /// tested: a nineteenth computed property arrived, went unlisted, and its control was evaluated once
    /// against a null kind and never asked again — a checkbox that could not appear, with nothing wrong
    /// where anybody would look for it.
    ///
    /// The cost of asking for all of them is a re-read of the bindings on one panel, and only when the kind
    /// or the series change — a click, never a tick. Cheaper than the class of bug it removes.
    /// </summary>
    private void NotifySeriesState() => OnPropertyChanged(string.Empty);

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
        _target.ShowValue = ValueShown;
        _target.ShowUnit = UnitShown;
        _target.BarDirection = BarDirection;

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
        EditingPreset ? SelectedPreset?.Name ?? "Preset"
        : _target is null ? "Widget"
        : !string.IsNullOrWhiteSpace(Title) ? Title.Trim()
        : SelectedKind?.Name ?? "Widget";

    /// <summary>The line under the title. In the preset view it says how far the changes reach, which is
    /// the one thing somebody needs to know before touching anything there.</summary>
    public string PanelSubtitle =>
        EditingPreset ? $"Editing a preset — {WornBy.ToLowerInvariant()}."
        : _target is null ? "Click a widget on the desktop to edit it."
        : "Changes land on the desktop as you make them.";

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
        EditingPreset = false;
        PreviewState = null;
        _target = null;
        Chosen.Clear();
        SelectedSensor = null;
        SensorFilter = string.Empty;
        OnPropertyChanged(nameof(HasTarget));
        NotifyView();
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

        // Set once, on the first Load — every kind in the picker rebuilds its preview when this is assigned,
        // and the hardware tree does not grow cores between one widget and the next.
        if (PreviewSensors.Count == 0 && hardware.Cpus.Count > 0)
        {
            var cpu = hardware.Cpus[0];
            SensorNode[] temperatures = [cpu.Temperature.Primary, cpu.Temperature.Secondary];

            PreviewSensors = cpu.Temperature.Primary.Value is not null
                ? [.. temperatures.Where(s => s.Value is not null)]
                : [.. cpu.Load.Cores.Where(s => s.Value is not null)];
        }

        _target = target;

        // Back to the widget. Reaching for another widget while a preset is open is asking about the
        // widget, and leaving the panel on the shared asset would answer a question nobody asked.
        EditingPreset = false;

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

            // The stored name as it is, even when this preset lacks it. Replacing it with a default here
            // was a second door to the same loss: opening the widget in the editor quietly rewrote its
            // choice, and the next save kept the rewrite. What the picker SHOWS is ShownRamp's business.
            Chosen.Add(new SeriesRow(sensor, series.Ramp, OnSeriesEdited, ShownItem));
        }

        // Pinned from the Explorer: the sensor is already decided, so add it rather than making the user
        // find it again in a list of two hundred.
        if (Chosen.Count == 0 && !string.IsNullOrEmpty(presetSensorIdentifier))
        {
            var pinned = _allSensors.FirstOrDefault(s => s.Identifier == presetSensorIdentifier);
            if (pinned is not null)
                Chosen.Add(new SeriesRow(pinned, DefaultRamp(0), OnSeriesEdited, ShownItem));
        }

        // Cleared, or the list keeps the row highlighted from the widget before — and since this is what
        // decides whether Add is offered and what it is called, the panel goes on looking like it is still
        // pointed at the previous widget.
        SelectedSensor = null;

        Caption = target.Text;
        SelectedClockFormat = target.ClockFormat;
        ValueShown = target.ShowValue;
        UnitShown = target.ShowUnit;
        BarDirection = target.BarDirection;

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
        NotifyView();

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

    /// <summary>True while a caller is changing every row at once. The picker still updates — this is not
    /// a way to write a value nobody sees — but the rebuild is left to the caller, which does it once at
    /// the end instead of once per row.</summary>
    private bool _bulk;

    public void SetRampQuietly(string ramp)
    {
        _bulk = true;
        Ramp = ramp;
        _bulk = false;
    }

    private readonly Func<SeriesRow, RampPreview?> _shown;

    public SeriesRow(SensorOption sensor, string ramp, Action changed, Func<SeriesRow, RampPreview?> shown)
    {
        Sensor = sensor;
        _ramp = ramp;
        _changed = changed;
        _shown = shown;
    }

    public SensorOption Sensor { get; }

    // No list of ramps here. It used to be handed in at construction, which made it a photograph: adding a
    // ramp left every existing row still offering the names that existed when it was built. The row's
    // picker reads the view model's list directly instead.

    private string _ramp;

    /// <summary>
    /// The ramp this reading CHOSE, by name — what is stored on the widget.
    ///
    /// Kept even under a preset that has no ramp of that name, because a preset must be safe to try on:
    /// switching to it and back must not cost the reading its choice. What the picker shows is
    /// <see cref="ShownItem"/>, which is a different question.
    /// </summary>
    public string Ramp
    {
        get => _ramp;
        set
        {
            if (value is null) return;

            if (SetProperty(ref _ramp, value))
            {
                RefreshShown();
                if (!_bulk) _changed();
            }
        }
    }

    /// <summary>
    /// The ramp this reading is ACTUALLY drawn in under the preset now chosen — what the picker shows.
    ///
    /// Separate from <see cref="Ramp"/> because "what was chosen" and "what is on the screen" differ exactly
    /// when the chosen name is missing from this preset. Bound to the stored name, the picker came up EMPTY
    /// there while the widget beside it was plainly drawing in the primary. It shows the truth now, and the
    /// choice underneath is still there for the next preset that has it. Setting it is the user picking a
    /// ramp, which is a real choice and is stored.
    ///
    /// AN ITEM, NOT A NAME — and that is what finally made the picker follow a preset switch, after two
    /// fixes that did not. Bound by name, the picker's value was "primary" before the switch and "primary"
    /// after it. Swapping its list drops the selection, but its SelectedValue is still "primary", so when
    /// the binding said "primary" again WPF saw no change and never looked the item up in the new list: a
    /// value with nothing selected, an empty box. Found by logging every read and write (the binding WAS
    /// told "primary", three times) and then reproduced in isolation — with SelectedValue="primary" and
    /// SelectedItem=null side by side. Bound to the record, the value does change: Ember's primary and Sky's
    /// are different records, because their colours differ, so the picker looks it up again.
    ///
    /// REFUSES NULL. The list swap makes the picker push null down this two-way binding; nothing in the
    /// code assigns null, so a null can only be that lost selection. It is ignored, and the item announced
    /// again once the new list has settled. Without this the null reached the widget and the reading's
    /// choice was wiped on every preset switch.
    /// </summary>
    public RampPreview? ShownItem
    {
        get => _shown(this);
        set
        {
            if (value is null)
            {
                System.Windows.Threading.Dispatcher.CurrentDispatcher.BeginInvoke(
                    () => OnPropertyChanged(nameof(ShownItem)),
                    System.Windows.Threading.DispatcherPriority.Background);
                return;
            }

            Ramp = value.Name;
        }
    }

    /// <summary>The shown ramp's name — what "Same ramp for all" copies, since it is pressed by someone
    /// looking at the picker.</summary>
    public string Shown => ShownItem?.Name ?? _ramp;

    /// <summary>Called when the preset or its ramps change, which changes what this row is drawn in
    /// without anything about the row itself having changed.</summary>
    public void RefreshShown()
    {
        OnPropertyChanged(nameof(ShownItem));
        OnPropertyChanged(nameof(Shown));
    }
}
