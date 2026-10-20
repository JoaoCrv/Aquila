using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using Aquila.Helpers;
using Aquila.Models;
using Aquila.Models.Nodes;
using Aquila.Services;
using LiveChartsCore;
using LiveChartsCore.Defaults;
using LiveChartsCore.Drawing;
using LiveChartsCore.Measure;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using LiveChartsCore.SkiaSharpView.WPF;
using SkiaSharp;

namespace Aquila.Controls;

/// <summary>
/// The time or the date — written out, or in the middle of rings that go round it: the seconds outermost, then the
/// minutes, then the hours.
///
/// Not driven by the hardware poll, and that is the whole reason it is a piece rather than a Text with a clever
/// caption: every other widget is redrawn when the sensors are read, at an interval the user chooses, and a clock
/// whose seconds came every two or five of them would be wrong most of the time. It follows
/// <see cref="ClockService"/> instead — one second for every clock, so two of them never disagree about it.
///
/// It takes hold of that clock on Loaded and lets go on Unloaded, never in the constructor, for
/// <see cref="SensorList"/>'s reason: "Send to screen" moves this very element from one window to another, and a
/// clock that is removed must stop being told the time.
///
/// Without rings it is the clock it always was — the same text, alignment, face and size — so a layout written
/// before rings existed looks exactly as it did. The rings are built only when asked for, and only once the piece
/// is on screen: a clock is told whether it has any straight after it is made, and a chart built in between and
/// thrown away is a chart's worth of work for nothing.
///
/// Its own default is all three rings, so the type picker — which builds the piece and never dresses it — shows
/// what a clock made today looks like.
/// </summary>
public sealed class ClockTile : Grid, IClockStyle, IArcStyle
{
    /// <summary>
    /// A line of the clock's text.
    ///
    /// A TextBlock of a type of its own, because the piece used to BE a TextBlock subclass — and WPF looks implicit
    /// styles up by the exact type, so none for TextBlock ever reached it. A plain TextBlock here would start
    /// taking them, and a clock without rings would stop being the clock it was.
    /// </summary>
    private sealed class Line : TextBlock
    {
    }

    /// <summary>One ring: its value, and the colour and ends it is drawn with — all changed in place, so the arc goes
    /// on from where it is.</summary>
    private sealed class Hand(PieSeries<ObservableValue> series, ObservableValue point)
    {
        public ObservableValue Point { get; } = point;

        /// <summary>The radius of the ring's inner edge, from Shape — what its rounded ends are measured against.
        /// </summary>
        public double Inner { get; set; }

        /// <summary>The shortest the arc is on its current move: the shorter of where it was and where it is going.
        /// Its ends are sized for that, so they fit all the way there.</summary>
        public double Shortest { get; set; }

        private SKColor? _colour;

        public void Wear(SKColor colour)
        {
            if (_colour == colour) return;
            _colour = colour;
            series.Fill = new SolidColorPaint(colour);
        }

        /// <summary>Rounds the ends by as much as the arc can hold — see AquilaCharts.CornerFor. Changes with the
        /// arc's length, so every few seconds near the top of a turn, and redraws nothing else.</summary>
        public void Round(double corner) => series.CornerRadius = AquilaCharts.CornerFor(Shortest, corner, 360, Inner);
    }

    /// <summary>
    /// How the rings travel from one second to the next while animation is on: steadily, for nine tenths of the
    /// second, so they rest a moment on each second and move through the rest of it.
    ///
    /// The one piece allowed past <see cref="Motion"/>'s two fifths of the poll, and both reasons are about what a
    /// clock is. It is not on the poll, so the cap protects nothing here. And time does not arrive in steps the way
    /// a reading does — between two seconds there IS a time, so a ring moving through it is nearer the truth than
    /// one standing still and then jumping. Linear for the same reason: an eased ring would say a second goes
    /// faster at its start than at its end. A speed of zero still means no movement at all.
    /// </summary>
    private static readonly Animation Sweeping = new(EasingFunctions.Lineal, TimeSpan.FromMilliseconds(900));

    /// <summary>The date and the seconds, as a share of the time's size: small enough that the time is plainly the
    /// reading, large enough to be read from the chair.</summary>
    private const double Small = 0.5;

    /// <summary>The gap between two rings, as a share of their thickness — enough to read as three rings rather
    /// than one thick band, and no more, because every pixel of it is taken from the middle where the time is.
    /// Never under two pixels, or thin rings would touch.</summary>
    private const double GapToThickness = 0.3;
    private const double MinGap = 2;

    private readonly Line _face = new() { VerticalAlignment = VerticalAlignment.Center };

    // The middle of the rings: the date small above, the time, the seconds small below.
    private readonly Line _date = new() { Opacity = 0.6 };
    private readonly Line _time = new();
    private readonly Line _seconds = new();
    private readonly StackPanel _middle = new()
    {
        HorizontalAlignment = HorizontalAlignment.Center,
        VerticalAlignment = VerticalAlignment.Center,
    };

    private PieChart? _chart;
    private PieSeries<ObservableValue>[] _series = [];
    private PieSeries<ObservableValue>? _track;
    private SKColor? _trackPainted;

    /// <summary>The rings, OUTERMOST first — seconds, minutes, hours — which is the order everything about them is
    /// decided in. LiveCharts holds them the other way round.</summary>
    private Hand[] _hands = [];

    private bool _hooked;
    private ClockNode? _clock;

    /// <summary>The instant the rings were last moved to. Null makes the next move a jump: the first after the
    /// rings are built, and the first after the piece comes back on screen, both start from wherever the arcs
    /// happen to be, and sweeping from there would be animating something that never happened.</summary>
    private DateTime? _shown;

    public ClockTile()
    {
        foreach (var line in new[] { _date, _time, _seconds })
        {
            line.HorizontalAlignment = HorizontalAlignment.Center;
            line.TextAlignment = TextAlignment.Center;

            // Digits of one width in whatever face the preset names, so the time does not shuffle sideways every
            // second. Asked of the face rather than forced by choosing a monospaced one: a preset's typeface is a
            // design decision, and most faces carry tabular figures for exactly this.
            Typography.SetNumeralAlignment(line, FontNumeralAlignment.Tabular);

            _middle.Children.Add(line);
        }

        Children.Add(_face);
        Children.Add(_middle);

        Loaded += (_, _) => Hook();
        Unloaded += (_, _) => Unhook();

        Refresh();
    }

    /// <summary>The time to show: the shared clock's, or — before the clock is running, or in a test — the
    /// machine's own.</summary>
    private DateTime Now => (_clock ?? ClockService.Current)?.Now ?? DateTime.Now;

    private void Hook()
    {
        if (_hooked) return;
        _hooked = true;

        _clock = ClockService.Current;
        if (_clock is not null) _clock.PropertyChanged += OnTick;

        _shown = null;
        Sizes();
        Refresh();
    }

    private void Unhook()
    {
        if (!_hooked) return;
        _hooked = false;

        if (_clock is not null) _clock.PropertyChanged -= OnTick;
        _clock = null;
    }

    private void OnTick(object? sender, PropertyChangedEventArgs e) => Show(Now);

    /// <summary>Everything the settings decide, from the top. Each part does nothing that is already so, so this is
    /// as cheap to call twice as once.</summary>
    private void Refresh()
    {
        ShowRings();
        Paint();
        Show(Now);
        Shape();
    }

    /// <summary>
    /// Puts up as many rings as the setting asks for, or takes them down.
    ///
    /// In place: going from none to some, or from one to three, is a change of the widget's look and not of what it
    /// is, so the editor treats it as Style, and nothing outside the piece is rebuilt. A different number of rings
    /// does need different series, and those are built here; the arcs leaving go at once rather than unwinding.
    /// </summary>
    private void ShowRings()
    {
        var count = ClockFace.RingCount(_rings);

        _face.Visibility = count == 0 ? Visibility.Visible : Visibility.Collapsed;
        _middle.Visibility = count == 0 ? Visibility.Collapsed : Visibility.Visible;

        if (count == 0)
        {
            DropChart();
            return;
        }

        if (!_hooked || _hands.Length == count) return;

        if (_chart is null)
        {
            _chart = new PieChart
            {
                Background = Brushes.Transparent,
                InitialRotation = -90,
                // A clock is always a full turn. The preset's sweep is a dial's, and a clock that stopped at 270°
                // would no longer be a clock.
                MaxAngle = 360,
                MinValue = 0,
                // The hands are shares of a turn — see ClockFace.Fractions.
                MaxValue = 1,
                IsClockwise = true,
                TooltipPosition = TooltipPosition.Hidden,
            };

            // The room left in the middle is worked out from the chart's size — see Shape.
            _chart.SizeChanged += (_, _) => Shape();
            Children.Insert(Children.IndexOf(_middle), _chart);
        }
        else
        {
            foreach (var hand in _hands) AquilaCharts.Travel(hand.Point, null);
        }

        var points = Enumerable.Range(0, count).Select(_ => new ObservableValue(0d)).ToArray();
        var (rings, track, all) = AquilaCharts.SolidRings(points);

        _hands = [.. Enumerable.Range(0, count).Select(k => new Hand(rings[count - 1 - k], points[count - 1 - k]))];
        _series = all;
        _track = track;
        _trackPainted = null;
        _shown = null;

        _chart.Series = all;
    }

    private void DropChart()
    {
        if (_chart is null) return;

        Children.Remove(_chart);
        _chart = null;
        _series = [];
        _track = null;
        _hands = [];
    }

    /// <summary>
    /// The colours: the preset's primary ramp, Normal outermost, then Alert, then Critical — and the seconds written
    /// under the time in the seconds ring's colour, so the line reads as the ring's.
    ///
    /// Elevated is the stop left out: in most ramps it sits close to Normal — it is the first step away from calm —
    /// and two neighbouring rings in nearly one colour would read as one thick ring.
    /// </summary>
    private void Paint()
    {
        string[] stops = [_ramp.Normal, _ramp.Alert, _ramp.Critical];
        for (var k = 0; k < _hands.Length; k++) _hands[k].Wear(Sk(HexBrush.From(stops[k])));

        var seconds = HexBrush.From(_ramp.Normal);
        if ((_seconds.Foreground as SolidColorBrush)?.Color != seconds.Color) _seconds.Foreground = seconds;

        if (_track is null) return;

        var track = TrackBrush is SolidColorBrush t ? Sk(t) : new SKColor(255, 255, 255, 20);
        if (_trackPainted == track) return;

        _track.Fill = new SolidColorPaint(track);
        _trackPainted = track;
    }

    private static SKColor Sk(SolidColorBrush brush) =>
        new(brush.Color.R, brush.Color.G, brush.Color.B, (byte)(brush.Color.A * brush.Opacity));

    /// <summary>
    /// How thick the rings are, how round their ends, and where they sit.
    ///
    /// LiveCharts gives each ring an equal share of the radius from the middle outwards, so with the middle left
    /// empty three rings would each be a third of the dial, however thin the preset asks for. The room in the
    /// middle is therefore sized from the chart: whatever is left once the rings and their gaps have been laid in
    /// from the edge. That is what keeps them together at the rim with the time inside, at any widget size.
    ///
    /// The track is never rounded. It is a whole circle, so it has no ends; rounding them only nicks it at twelve.
    /// </summary>
    private void Shape()
    {
        if (_chart is null) return;

        var thickness = Thickness;
        var gap = Math.Max(MinGap, thickness * GapToThickness);
        var size = Math.Min(_chart.ActualWidth, _chart.ActualHeight);
        var middle = Math.Max(0, size / 2 - _hands.Length * (thickness + gap));

        // Assigned every time, equal or not. LiveCharts ignores an equal value, but it also lets its theme set any
        // property nobody has assigned — and its theme rounds a gauge's ends by 8. A corner of 0 skipped as
        // "already 0" came out round. (The rings' corners are assigned below, by Round.)
        foreach (var series in _series)
        {
            series.MaxRadialColumnWidth = thickness;
            series.InnerRadius = middle;
            if (series.IsFillSeries) series.CornerRadius = 0;
        }

        // Where each ring's inner edge falls, which is what its rounded ends are measured against. Each ring has an
        // equal slot from the middle out and sits at the outside of it — narrower than the preset asks only when the
        // widget is too small to give it the room.
        if (_hands.Length == 0) return;
        var slot = (size / 2 - middle) / _hands.Length;

        for (var k = 0; k < _hands.Length; k++)
        {
            _hands[k].Inner = size / 2 - k * slot - Math.Min(thickness, slot);
            _hands[k].Round(Caps);
        }
    }

    /// <summary>At least a pixel: the thickness comes from a preset, which a hand can edit to nothing, and a clamp
    /// whose ceiling fell below its floor would throw.</summary>
    private double Thickness => Math.Max(1, _arcThickness);

    /// <summary>The corner the preset asks for, no rounder than half the ring is thick — past that the ends would
    /// eat into the ring itself. How much of it a ring can hold at its length is CornerFor's question.</summary>
    private double Caps => Math.Clamp(_arcCorner, 0, Thickness / 2);

    /// <summary>The ring lines' sizes, from the time's. The line without rings takes the reading's size directly, as
    /// the piece always did.</summary>
    private void Sizes()
    {
        _time.FontSize = _face.FontSize;
        _date.FontSize = _face.FontSize * Small;
        _seconds.FontSize = _face.FontSize * Small;
    }

    /// <summary>Writes the time, and moves the rings to it. Never animates the digits — a number counting up to the
    /// next second is wrong while it counts.</summary>
    private void Show(DateTime now)
    {
        if (_rings == ClockRings.None)
        {
            _face.Text = ClockFace.Text(now, _format);
            return;
        }

        _date.Text = ClockFace.ShortDate(now);
        _time.Text = ClockFace.Time(now);
        _seconds.Text = ClockFace.Seconds(now);

        // Hidden rather than collapsed: the two small lines keep their room either way, so the time stays in the
        // middle of the rings whichever of them is shown.
        _date.Visibility = _showDate ? Visibility.Visible : Visibility.Hidden;
        _seconds.Visibility = _showSeconds ? Visibility.Visible : Visibility.Hidden;

        Sweep(now);
    }

    /// <summary>
    /// Moves each ring to where it stands now — sweeping there when animation is on, jumping when it is off, and
    /// jumping whatever the setting when the ring would otherwise travel backwards (see ClockFace.Wrapped).
    /// </summary>
    private void Sweep(DateTime now)
    {
        if (_hands.Length == 0) return;

        var at = ClockFace.Fractions(now);
        (bool Seconds, bool Minutes, bool Hours) wrapped =
            _shown is { } before ? ClockFace.Wrapped(before, now) : (true, true, true);
        _shown = now;

        for (var k = 0; k < _hands.Length; k++)
        {
            var (fraction, jumps) = k switch
            {
                0 => (at.Seconds, wrapped.Seconds),
                1 => (at.Minutes, wrapped.Minutes),
                _ => (at.Hours, wrapped.Hours),
            };

            var hand = _hands[k];
            if (hand.Point.Value == fraction) continue;

            var sweeps = Motion.Enabled && !jumps;

            // The ends first, sized for the shortest the arc will be on the way: a sweep from :02 to :03 starts at
            // :02's length, and ends rounded for :03 would not fit it for the first part of the second.
            hand.Shortest = sweeps && hand.Point.Value is { } was ? Math.Min(was, fraction) : fraction;
            hand.Round(Caps);

            // Then the way there, then the value: the arc takes its transition when its new angle is set.
            AquilaCharts.Travel(hand.Point, sweeps ? Sweeping : null);
            hand.Point.Value = fraction;
        }
    }

    // ── Content: what the widget says it shows ───────────────────────────

    private ClockFormat _format = ClockFormat.Time;
    private ClockRings _rings = ClockRings.All;
    private bool _showSeconds = true;
    private bool _showDate = true;

    public ClockFormat Format
    {
        get => _format;
        set { _format = value; Show(Now); }
    }

    public ClockRings Rings
    {
        get => _rings;
        set { _rings = value; Refresh(); }
    }

    public bool ShowSeconds
    {
        get => _showSeconds;
        set { _showSeconds = value; Show(Now); }
    }

    public bool ShowDate
    {
        get => _showDate;
        set { _showDate = value; Show(Now); }
    }

    // ── Style: what the preset says it looks like ────────────────────────

    private string? _valueFont;
    private TextWeight _valueWeight = TextWeight.Regular;
    private double _arcThickness = 14;
    private double _arcCorner;
    private Brush? _trackBrush;
    private Ramp _ramp = Ramp.Neutral;

    public double ValueSize
    {
        get => _face.FontSize;
        set { _face.FontSize = value; Sizes(); }
    }

    public string? ValueFont
    {
        get => _valueFont;
        set { _valueFont = value; Faces(); }
    }

    public TextWeight ValueWeight
    {
        get => _valueWeight;
        set { _valueWeight = value; Faces(); }
    }

    /// <summary>Where the line without rings sits. The middle of the rings is always the middle.</summary>
    public TextAlign Align
    {
        get => _face.TextAlignment.ToAlign();
        set => _face.TextAlignment = value.ToTextAlignment();
    }

    public double ArcThickness
    {
        get => _arcThickness;
        set { _arcThickness = value; Shape(); }
    }

    public double ArcCorner
    {
        get => _arcCorner;
        set { _arcCorner = value; Shape(); }
    }

    public Brush? TrackBrush
    {
        get => _trackBrush;
        set { _trackBrush = value; Paint(); }
    }

    /// <summary>Re-read every time it is set, even to the ramp it already holds: editing a preset changes that
    /// ramp's stops in place, and the same object arriving again is how the edit is announced.</summary>
    public Ramp Ramp
    {
        get => _ramp;
        set { _ramp = value; Paint(); }
    }

    private void Faces()
    {
        foreach (var line in new[] { _face, _date, _time, _seconds }) line.Wear(_valueFont, _valueWeight);
    }
}
