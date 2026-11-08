using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Shapes;
using Aquila.Controls.Primitives;
using Aquila.Models;
using Aquila.Services;

namespace Aquila.Controls;

/// <summary>
/// The Number widget: one reading, large, with its unit — and, where the widget asks for them, a bar under it that
/// fills as the reading goes into its limits, the lowest and highest readings of the session under that, and an arrow
/// beside it when it is rising or falling (docs/widget-numero).
///
/// Its own piece since the Number stopped borrowing the dashboard's StatBox. That box was made for a card — a rounded
/// fill in the theme's card colour, a caption under the number, a unit in the theme's grey — and on the desktop two of
/// those were colours no preset could reach. Here the number wears the ramp, the unit the number's colour at the
/// preset's opacity, and everything that comments on the reading — the session's range, the arrow, the bar's marks —
/// the title's colour, since they say something about the reading as the title does.
///
/// The bar is the reading's <see cref="Thresholds.Level"/>, the scale the title bar's pills draw: a third of the way
/// along is where Elevated starts, two thirds where Alert does, full is Critical — so the bar passes one of its marks
/// exactly when the number changes colour. A reading with no limits has nothing to fill towards, and draws no bar and
/// keeps no room for one. It travels at <see cref="Motion"/>'s speed, drawn by <see cref="BarFill"/>, which draws again
/// without laying anything out.
///
/// The session's lowest and highest are the node's own <see cref="SensorNode.Min"/> and <see cref="SensorNode.Max"/>,
/// which the source keeps from the moment the app started. Until both are there their line keeps its room, so the
/// number does not jump when they arrive.
///
/// The arrow is <see cref="SensorTrend"/>, asked on every reading from samples kept here — each with the time it
/// arrived and the reading as it was, missing ones included, since the node's history writes a missing reading down as
/// a nought. It stands in a column of its own beside the number, with an empty one as wide on the other side, so the
/// number does not move when it comes or goes.
///
/// It takes hold on Loaded and lets go on Unloaded, as <see cref="SensorList"/> does, so a widget off screen holds
/// nothing and one sent to another screen takes hold again when it arrives.
/// </summary>
public sealed class NumberTile : UserControl, ISensorPiece, INumberStyle, INumberParts, IBarStyle, IScaledFigure
{
    private readonly Run _reading = new();
    private readonly Run _unit = new();
    private readonly TextBlock _number = new();

    private readonly Grid _line = new() { HorizontalAlignment = HorizontalAlignment.Center };
    private readonly Path _arrow = new() { Opacity = 0, VerticalAlignment = VerticalAlignment.Top };

    private readonly Grid _level = new();
    private readonly BarFill _bar = new() { Marks = [1d / 3, 2d / 3] };

    private readonly StackPanel _notes = new()
    {
        Orientation = Orientation.Horizontal,
        HorizontalAlignment = HorizontalAlignment.Center,
    };

    private readonly TextBlock _lowest = new();
    private readonly TextBlock _highest = new();

    private readonly List<(TimeSpan At, float? Value)> _samples = [];

    private SensorNode? _sensor;
    private VitalMonitor? _monitor;
    private bool _hooked;
    private bool _recording;
    private TrendArrow _shown = TrendArrow.None;

    // Where the bar was last sent: NaN until it has been put anywhere, so its first placing is a jump — a bar never
    // sweeps up from a length it never had.
    private double _filled = double.NaN;

    public NumberTile()
    {
        _number.SetResourceReference(StyleProperty, "StatBoxValue");
        _number.Inlines.Add(_reading);
        _number.Inlines.Add(_unit);

        // The number in the middle column, the arrow in the right one, and the left one as wide and empty.
        _line.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0) });
        _line.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        _line.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0) });
        Grid.SetColumn(_number, 1);
        Grid.SetColumn(_arrow, 2);
        _line.Children.Add(_number);
        _line.Children.Add(_arrow);

        // The bar across the middle 78%, as the design draws it.
        _level.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(11, GridUnitType.Star) });
        _level.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(78, GridUnitType.Star) });
        _level.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(11, GridUnitType.Star) });
        Grid.SetColumn(_bar, 1);
        _level.Children.Add(_bar);

        foreach (var note in new[] { _lowest, _highest })
        {
            note.SetResourceReference(StyleProperty, "StatBoxValue");
            _notes.Children.Add(note);
        }

        // Centred in whatever height the widget gives it, as the number always was: given more room than it needs,
        // it does not sit against the title with the space all below it.
        var stack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        stack.Children.Add(_line);
        stack.Children.Add(_level);
        stack.Children.Add(_notes);
        Content = stack;

        Loaded += (_, _) => Hook();
        Unloaded += (_, _) => Unhook();

        SetResourceReference(SchemeTrackProperty, TrackKey);
        Apply();
    }

    // ── The reading ──────────────────────────────────────────────────

    public SensorNode? Sensor
    {
        get => _sensor;
        set
        {
            if (ReferenceEquals(_sensor, value)) return;

            var hooked = _hooked;
            Unhook();
            _sensor = value;
            _samples.Clear();
            _filled = double.NaN;

            if (hooked || IsLoaded) Hook();
            else Show(animate: false);
        }
    }

    private void Hook()
    {
        if (_hooked) return;
        _hooked = true;

        if (_sensor is not null) _sensor.PropertyChanged += OnSensorChanged;

        _monitor = VitalMonitor.Current;
        if (_monitor is not null) _monitor.Changed += OnLimitsChanged;

        Record(ShowTrend);
        PlaceArrow();

        // Drawn as things are — whatever changed while it was off screen.
        Show(animate: false);
    }

    private void Unhook()
    {
        if (!_hooked) return;
        _hooked = false;

        if (_sensor is not null) _sensor.PropertyChanged -= OnSensorChanged;
        if (_monitor is not null) _monitor.Changed -= OnLimitsChanged;
        _monitor = null;

        Record(false);

        // Everything put where it was going: a widget sent to another screen arrives with nothing in flight.
        Show(animate: false);
    }

    /// <summary>Takes a sample of every reading while the arrow is wanted, and none while it is not — the node's
    /// history changes on every read, whether the value did or not, so it is what says a reading arrived.</summary>
    private void Record(bool wanted)
    {
        wanted &= _hooked && _sensor is not null;
        if (wanted == _recording) return;
        _recording = wanted;

        if (wanted)
        {
            _sensor!.History.CollectionChanged += OnRead;
            return;
        }

        if (_sensor is not null) _sensor.History.CollectionChanged -= OnRead;
        _samples.Clear();
    }

    private void OnSensorChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(SensorNode.Value):
                Show(animate: true);
                break;
            case nameof(SensorNode.Min) or nameof(SensorNode.Max):
                ShowNotes();
                break;
        }
    }

    private void OnLimitsChanged() => ShowLevel(animate: true);

    private void OnRead(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action != NotifyCollectionChangedAction.Add || _sensor is not { } sensor) return;

        var now = Now;
        _samples.Add((now, sensor.Value));

        // Only what the rule can still count.
        var oldest = now - SensorTrend.Kept;
        _samples.RemoveAll(sample => sample.At <= oldest);

        ShowTrendOf(now);
    }

    /// <summary>A clock that only goes forward. The time of day can be changed under the app, and moves an hour twice a
    /// year; a trend measured against it would point at nothing for an hour.</summary>
    private static TimeSpan Now => TimeSpan.FromMilliseconds(Environment.TickCount64);

    // ── Drawing it ───────────────────────────────────────────────────

    /// <summary>The number, the unit, the bar and the session's range, as they are now.</summary>
    private void Show(bool animate)
    {
        var (number, unit) = _sensor.Parts();
        _reading.Text = number;
        _unit.Text = ShowUnit ? unit : string.Empty;

        ShowLevel(animate);
        ShowNotes();

        if (!_recording) HideArrow(animate: false);
    }

    /// <summary>
    /// How far the reading is into its limits — those in force now, the user's, the hardware's own or the built-in
    /// ones — and never shorter than <see cref="BarGeometry.Least"/> of its track, which the bar holds it to, so a reading
    /// at rest still shows the end of a bar rather than a bare track. No bar at all for a reading with no limits, and no
    /// room kept for one.
    /// </summary>
    private void ShowLevel(bool animate)
    {
        var limits = _sensor is not null && VitalMonitor.Current is { } monitor && monitor.KeyFor(_sensor) is { } key
            ? monitor.For(key)
            : null;

        if (!ShowBar || limits is not { } within)
        {
            _level.Visibility = Visibility.Collapsed;
            _filled = double.NaN;
            return;
        }

        _level.Visibility = Visibility.Visible;

        var target = within.Level(_sensor?.Value ?? 0);
        if (animate && Math.Abs(target - _filled) < 1e-6) return;

        animate &= !double.IsNaN(_filled);
        _filled = target;
        Motion.Move(_bar, target, animate);
    }

    /// <summary>
    /// The session's lowest and highest, written as the reading is. The unit only where it differs from the number's —
    /// which only a throughput can do, its unit following the size of the number — and never after a nought, which is
    /// nothing in any unit. Kept in place, unseen, until both are there.
    /// </summary>
    private void ShowNotes()
    {
        if (!ShowMinMax)
        {
            _notes.Visibility = Visibility.Collapsed;
            return;
        }

        if (_sensor is not { Min: { } low, Max: { } high } sensor)
        {
            _notes.Visibility = Visibility.Hidden;
            return;
        }

        var shown = sensor.Parts().Unit;
        _lowest.Text = "min " + Note(low, sensor.Unit, shown);
        _highest.Text = "max " + Note(high, sensor.Unit, shown);
        _notes.Visibility = Visibility.Visible;
    }

    private string Note(float value, string? unit, string shown)
    {
        var (number, scaled) = SensorFormat.Parts(value, unit);
        return ShowUnit && value != 0 && scaled != shown && scaled.Length > 0 ? $"{number} {scaled}" : number;
    }

    private void ShowTrendOf(TimeSpan now)
    {
        var next = _sensor is { } sensor
            ? SensorTrend.Of(_samples, now, VitalMonitor.Current?.CriticalFor(sensor), sensor.Min, sensor.Max, _shown)
            : TrendArrow.None;

        if (next == _shown) return;
        _shown = next;

        if (next == TrendArrow.None)
        {
            HideArrow(animate: true);
            return;
        }

        _arrow.Data = next == TrendArrow.Up ? _upward : _downward;
        Motion.To(_arrow, OpacityProperty, 1);
    }

    private void HideArrow(bool animate)
    {
        _shown = TrendArrow.None;
        Motion.To(_arrow, OpacityProperty, 0, animate);
    }

    // The arrow's two shapes, made at its size whenever that changes.
    private Geometry? _upward;
    private Geometry? _downward;

    // ── Colour ───────────────────────────────────────────────────────

    public static readonly DependencyProperty AccentProperty =
        DependencyProperty.Register(nameof(Accent), typeof(Brush), typeof(NumberTile),
            new PropertyMetadata(null, (d, _) => ((NumberTile)d).Paint()));

    /// <summary>The reading's colour — the ramp's, judged on every reading by whoever paints. The number, its unit
    /// and the bar's fill wear it. Unset, the number takes the text colour around it.</summary>
    public Brush? Accent
    {
        get => (Brush?)GetValue(AccentProperty);
        set => SetValue(AccentProperty, value);
    }

    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);

        // Unpainted, the unit and the bar follow the text colour, which arrives by inheritance.
        if (e.Property == ForegroundProperty && Accent is null) Paint();
    }

    private void Paint()
    {
        var ink = Accent ?? Foreground;

        if (Accent is { } accent) _number.Foreground = accent;
        else _number.ClearValue(TextBlock.ForegroundProperty);

        // A Run has no opacity of its own, so the unit is given a brush that has it — made again only when the colour
        // does change. The painter hands over a new brush on every read, most of them the colour the last one was.
        if (ink is SolidColorBrush { Color: var colour } && colour == _inkColour && _unitOpacity == _inkStrength)
            return;

        _inkColour = (ink as SolidColorBrush)?.Color;
        _inkStrength = _unitOpacity;

        _unit.Foreground = Dimmed(ink, _unitOpacity);
        _bar.Fill = ink;
    }

    // The colour the unit and the bar were last given, and the unit's strength then.
    private Color? _inkColour;
    private double _inkStrength = double.NaN;

    /// <summary>The notes' colour, and the marks' at half of it — a scale's ticks are quieter than its labels.</summary>
    private void PaintNotes()
    {
        var note = _noteBrush;

        foreach (var text in new[] { _lowest, _highest })
        {
            if (note is not null)
            {
                text.Foreground = note;
                text.ClearValue(OpacityProperty);
            }
            else
            {
                text.ClearValue(TextBlock.ForegroundProperty);
                text.Opacity = UnpaintedNote;
            }
        }

        _arrow.Fill = note ?? Dimmed(Foreground, UnpaintedNote);
        _bar.MarkBrush = Dimmed(note ?? Foreground, note is null ? UnpaintedNote / 2 : 0.5);
    }

    /// <summary>A brush as the one given, at a share of its strength. Frozen, as every brush handed round is.</summary>
    private static Brush? Dimmed(Brush? brush, double share)
    {
        if (brush is null) return null;

        var dimmed = brush.CloneCurrentValue();
        dimmed.Opacity = brush.Opacity * Math.Clamp(share, 0, 1);
        dimmed.Freeze();
        return dimmed;
    }

    // ── The figure, at the kind's default size ────────────────────────
    //
    // The design's own measurements (docs/widget-numero), drawn beside an 18-pixel number; each at the widget's scale.

    /// <summary>From the number down to the bar.</summary>
    private const double BarGap = 8;

    /// <summary>How far a mark reaches past the track, above and below it.</summary>
    private const double MarkReach = 3;

    /// <summary>From the bar down to the session's range — and, with no bar, from the number, where the bar's gap was.
    /// </summary>
    private const double NotesGap = 6;

    /// <summary>Between the lowest and the highest.</summary>
    private const double NotesApart = 12;

    /// <summary>The arrow's width, its height and its distance from the number, as shares of the number's size: 7 by 6
    /// and 4 off an 18-pixel number. Shares and not sizes, so a preset with a bigger number has a bigger arrow.</summary>
    private const double ArrowWidth = 7.0 / 18;
    private const double ArrowHeight = 6.0 / 18;
    private const double ArrowGap = 4.0 / 18;

    /// <summary>How tall a digit stands, as a share of its size — near enough for every face, and only used to put the
    /// arrow beside the middle of the digits rather than the middle of the line, which takes in the descenders.</summary>
    private const double DigitHeight = 0.7;

    /// <summary>The notes' strength when nothing has dressed the piece — the type picker — the built-in presets' title
    /// opacity.</summary>
    private const double UnpaintedNote = 0.55;

    /// <summary>Lays everything out again at the sizes and with the parts it has now, and draws it. In place: every one
    /// of these is Style, and a Style change re-dresses a piece without rebuilding it.</summary>
    private void Apply()
    {
        _number.FontSize = _valueSize;
        _number.Wear(_valueFont, _valueWeight);
        _unit.FontSize = _unitSize;

        // The arrow, at the number's size, and the two columns either side of the number that hold it.
        var width = _valueSize * ArrowWidth;
        var height = _valueSize * ArrowHeight;
        var slot = ShowTrend ? width + _valueSize * ArrowGap : 0;

        _line.ColumnDefinitions[0].Width = new GridLength(slot);
        _line.ColumnDefinitions[2].Width = new GridLength(slot);
        _arrow.Visibility = ShowTrend ? Visibility.Visible : Visibility.Collapsed;
        _arrow.HorizontalAlignment = HorizontalAlignment.Right;
        _arrow.Width = width;
        _arrow.Height = height;

        _upward = Triangle(new(0, height), new(width / 2, 0), new(width, height));
        _downward = Triangle(new(0, 0), new(width, 0), new(width / 2, height));
        if (_shown != TrendArrow.None) _arrow.Data = _shown == TrendArrow.Up ? _upward : _downward;
        PlaceArrow();

        DressBar();
        _bar.MarkReach = MarkReach * _figureScale;
        _level.Margin = new Thickness(0, BarGap * _figureScale, 0, 0);

        _notes.Margin = new Thickness(0, (ShowBar ? NotesGap : BarGap) * _figureScale, 0, 0);
        _highest.Margin = new Thickness(NotesApart * _figureScale, 0, 0, 0);

        foreach (var note in new[] { _lowest, _highest })
        {
            note.FontSize = _noteSize;
            note.Wear(_valueFont, TextWeight.Regular);
        }

        Paint();
        PaintNotes();

        Record(ShowTrend);
        Show(animate: false);
    }

    /// <summary>Beside the middle of the digits: the line's top to its baseline, less half a digit, less half the
    /// arrow. Asked again on Loaded too, since the number's face is the app's style's only once it is in a window.
    /// </summary>
    private void PlaceArrow() =>
        _arrow.Margin = new Thickness(0,
            Math.Max(0, (_number.FontFamily.Baseline - DigitHeight / 2) * _valueSize - _arrow.Height / 2), 0, 0);

    private static Geometry Triangle(Point a, Point b, Point c)
    {
        var geometry = new StreamGeometry();
        using (var context = geometry.Open())
        {
            context.BeginFigure(a, isFilled: true, isClosed: true);
            context.PolyLineTo([b, c], isStroked: false, isSmoothJoin: false);
        }

        geometry.Freeze();
        return geometry;
    }

    // ── Style ────────────────────────────────────────────────────────
    //
    // The defaults are what the type picker shows — the one place a piece is drawn undressed: StatBox's number, the
    // built-in presets' unit and notes, and the parts a Number made today starts with.

    private double _valueSize = 20;
    private string? _valueFont;
    private TextWeight _valueWeight = TextWeight.Regular;
    private double _unitSize = 13;
    private double _unitOpacity = 0.6;
    private Brush? _noteBrush;
    private double _noteSize = 11;
    private double _barThickness = 6;
    private double _barCorner = 3;
    private Brush? _trackBrush;
    private double _figureScale = 1;
    private bool _showUnit = true;
    private bool _showBar = true;
    private bool _showMinMax = true;
    private bool _showTrend;

    public double ValueSize
    {
        get => _valueSize;
        set { _valueSize = value; Apply(); }
    }

    public string? ValueFont
    {
        get => _valueFont;
        set { _valueFont = value; Apply(); }
    }

    public TextWeight ValueWeight
    {
        get => _valueWeight;
        set { _valueWeight = value; Apply(); }
    }

    public double UnitSize
    {
        get => _unitSize;
        set { _unitSize = value; Apply(); }
    }

    /// <summary>Held to 0..1 here, where it is used: the number comes from a preset, which a hand can edit.</summary>
    public double UnitOpacity
    {
        get => _unitOpacity;
        set { _unitOpacity = double.IsNaN(value) ? 0.6 : Math.Clamp(value, 0, 1); Apply(); }
    }

    public Brush? NoteBrush
    {
        get => _noteBrush;
        set { _noteBrush = value; Apply(); }
    }

    public double NoteSize
    {
        get => _noteSize;
        set { _noteSize = value; Apply(); }
    }

    public double BarThickness
    {
        get => _barThickness;
        set { _barThickness = value; Apply(); }
    }

    public double BarCorner
    {
        get => _barCorner;
        set { _barCorner = value; Apply(); }
    }

    private const string TrackKey = "Aquila.Scheme.Track";

    /// <summary>The bar's track from the widget's own preset; unset, the dashboard's.</summary>
    public Brush? TrackBrush
    {
        get => _trackBrush;
        set { _trackBrush = value; Apply(); }
    }

    /// <summary>The dashboard's track, followed through the resource so a change of dashboard preset reaches a bar whose
    /// own preset names none. A look holds a brush, not a resource, so the brush is taken from here each time the look
    /// is made — and made again when it changes.</summary>
    private static readonly DependencyProperty SchemeTrackProperty =
        DependencyProperty.Register("SchemeTrack", typeof(Brush), typeof(NumberTile),
            new PropertyMetadata(null, (piece, _) => ((NumberTile)piece).DressBar()));

    /// <summary>The bar at two thirds of the preset's, as every bar under text is, never under a pixel thick.</summary>
    private void DressBar()
    {
        var look = new BarLook(_barThickness, _barCorner, _trackBrush ?? (Brush?)GetValue(SchemeTrackProperty))
            .Scaled(IBarStyle.UnderTextShare);
        _bar.Look = look with { Thickness = Math.Max(1, look.Thickness) };
    }

    /// <summary>For the figure's own gaps: number to bar, bar to notes, between the notes, and the marks' reach.
    /// </summary>
    public double FigureScale
    {
        get => _figureScale;
        set { _figureScale = value; Apply(); }
    }

    public bool ShowUnit
    {
        get => _showUnit;
        set { _showUnit = value; Apply(); }
    }

    public bool ShowBar
    {
        get => _showBar;
        set { _showBar = value; Apply(); }
    }

    public bool ShowMinMax
    {
        get => _showMinMax;
        set { _showMinMax = value; Apply(); }
    }

    public bool ShowTrend
    {
        get => _showTrend;
        set { _showTrend = value; Apply(); }
    }
}
