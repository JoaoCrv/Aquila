using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using Aquila.Controls.Primitives;
using Aquila.Helpers;
using Aquila.Models;
using Aquila.Models.Nodes;
using Aquila.Services;

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
/// before rings existed looks exactly as it did. The rings are <see cref="RingArc"/>s, three small elements that
/// draw themselves, so they are put up and taken down with the setting and leave the tree entirely at none. (They
/// were a LiveCharts chart until the pilot for our own primitives: a chart's render loop, axes and series, for three
/// arcs.)
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

    /// <summary>The date and the seconds, as a share of the time's size — the design the rings came from, measured:
    /// both about 0.38 of the time there. Small enough that the time is plainly the reading; the date had been 0.5
    /// and, being by far the widest line ("quarta, 21/10" against "49"), reached past the inner ring.</summary>
    private const double Small = 0.4;

    /// <summary>
    /// How thick a ring is against the preset's gauge thickness. The gauge section was written for a dial — ONE arc
    /// — and three rings at a dial's thickness took two thirds of the radius between them, leaving the time a circle
    /// 35% of the clock across. The design they came from, measured: rings 4.3% of the diameter, an inner circle of
    /// 61%. 0.55 of Ember's 14 is that, at the default size (João, 2026-10-21). A share and not a number, so a preset
    /// with a heavier dial still gets a heavier clock, in proportion.
    /// </summary>
    private const double RingShare = 0.55;

    /// <summary>How much of the inner circle the text fills, when it fills it: the half-diagonal of the block of
    /// three lines against the circle's radius. 0.86 in the design, measured.</summary>
    private const double FillShare = 0.86;

    /// <summary>The gap between two rings, as a share of their thickness — 0.8, the design's (8 px against 10):
    /// enough to read as three rings rather than one banded one, and no more, because every pixel of it is taken from
    /// the middle where the time is. Never under two pixels, or thin rings would touch.
    ///
    /// A share, so it grows with the widget as the thickness does — the thickness arrives scaled. The floor does not:
    /// what it guards against, two rings touching, is a matter of pixels whatever the scale.</summary>
    private const double GapToThickness = 0.8;
    private const double MinGap = 2;

    private static readonly TimeSpan OneSecond = TimeSpan.FromSeconds(1);

    private readonly Line _face = new() { VerticalAlignment = VerticalAlignment.Center };

    // The middle of the rings: the date small above, the time, the seconds small below.
    private readonly Line _date = new() { Opacity = 0.6 };
    private readonly Line _time = new();
    private readonly Line _seconds = new();
    private readonly StackPanel _middle = new();

    /// <summary>Holds the middle, and shrinks it when it would not fit inside the innermost ring — see
    /// <see cref="FitMiddle"/>. Its own element so the middle can be measured as it is, without the shrink.</summary>
    private readonly Border _fit = new()
    {
        HorizontalAlignment = HorizontalAlignment.Center,
        VerticalAlignment = VerticalAlignment.Center,
    };

    /// <summary>The rings, OUTERMOST first — seconds, minutes, hours — which is the order everything about them is
    /// decided in.</summary>
    private RingArc[] _hands = [];

    private bool _hooked;
    private ClockNode? _clock;

    /// <summary>Whether the next move puts the rings in place rather than taking them there: the first after the
    /// piece comes on screen, and the first after rings are put up. Both start from wherever the arcs happen to be,
    /// and travelling from there would be animating something that never happened.</summary>
    private bool _fresh = true;

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

        _fit.Child = _middle;
        Children.Add(_face);
        Children.Add(_fit);

        SizeChanged += (_, _) => FitMiddle();
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

        _fresh = true;
        Sizes();
        Refresh();
    }

    /// <summary>Lets go of the clock, and stops the rings where they are: a ring still travelling off screen is
    /// frames drawn for nobody.</summary>
    private void Unhook()
    {
        if (!_hooked) return;
        _hooked = false;

        if (_clock is not null) _clock.PropertyChanged -= OnTick;
        _clock = null;

        foreach (var hand in _hands) hand.Stop();
    }

    private void OnTick(object? sender, PropertyChangedEventArgs e) => Show(Now);

    /// <summary>Everything the settings decide, from the top. Each part does nothing that is already so, so this is
    /// as cheap to call twice as once.</summary>
    private void Refresh()
    {
        ShowRings();
        Paint();
        Shape();
        Show(Now);
    }

    /// <summary>
    /// Puts up as many rings as the setting asks for, or takes them down.
    ///
    /// In place: going from none to some, or from one to three, is a change of the widget's look and not of what it
    /// is, so the editor treats it as Style, and nothing outside the piece is rebuilt. Rings leaving go at once
    /// rather than unwinding, and at none they leave the tree, so the piece is the text clock and nothing else.
    /// </summary>
    private void ShowRings()
    {
        var count = ClockFace.RingCount(_rings);

        _face.Visibility = count == 0 ? Visibility.Visible : Visibility.Collapsed;
        _fit.Visibility = count == 0 ? Visibility.Collapsed : Visibility.Visible;

        if (_hands.Length == count) return;

        foreach (var hand in _hands)
        {
            hand.Stop();
            Children.Remove(hand);
        }

        // Under the text, so the time is never drawn over by a ring on a widget too small for both.
        _hands = [.. Enumerable.Range(0, count).Select(_ => new RingArc())];
        foreach (var hand in _hands) Children.Insert(Children.IndexOf(_fit), hand);

        _fresh = true;
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
        var track = _trackBrush ?? ArcStyle.DefaultTrack;

        for (var k = 0; k < _hands.Length; k++)
        {
            var hand = _hands[k];

            // Compared by colour, because a preset hands over a new brush of the same colour on every restyle,
            // and a new brush is a new pen and a redraw for nothing.
            var colour = HexBrush.From(stops[k]);
            if ((hand.Stroke as SolidColorBrush)?.Color != colour.Color) hand.Stroke = colour;

            hand.TrackBrush = track;
        }

        var seconds = HexBrush.From(_ramp.Normal);
        if ((_seconds.Foreground as SolidColorBrush)?.Color != seconds.Color) _seconds.Foreground = seconds;
    }

    /// <summary>
    /// How thick the rings are, how their ends are drawn, and where they sit.
    ///
    /// Each ring is inset from the one outside it by a thickness and a gap, so all three keep together at the rim
    /// whatever the widget's size, and the time has the middle. On a widget too small for them, the inner rings run
    /// out of room and are not drawn rather than drawn over each other.
    ///
    /// The ends are round or square, never in between, as the dial's are. The preset's corner was a radius the dial's
    /// chart had to fit inside its arc; a ring's round end is a cap past the arc's end, so there is nothing for a
    /// radius to fit, and any corner at all asks for round.
    /// </summary>
    private void Shape()
    {
        var thickness = RingThickness;
        var gap = Math.Max(MinGap, thickness * GapToThickness);
        var caps = _arcCorner > 0 ? PenLineCap.Round : PenLineCap.Flat;

        for (var k = 0; k < _hands.Length; k++)
        {
            var hand = _hands[k];
            hand.Thickness = thickness;
            hand.Caps = caps;
            hand.Margin = new Thickness(k * (thickness + gap));
        }

        FitMiddle();
    }

    /// <summary>
    /// Sizes the date, the time and the seconds — all three alike — to the middle of the innermost ring.
    ///
    /// Filling (<see cref="FillRings"/>, the default): grown or shrunk until the corners of the block they make
    /// reach <see cref="FillShare"/> of the way to the ring's inside edge, so the time is as large as the rings leave
    /// room for at any size, as in the design. Not filling: the preset's reading size, shrunk only when the block
    /// would not clear the ring, and never grown.
    ///
    /// Measured rather than predicted, because what has to fit is text: its width depends on the face, the weight,
    /// the day's name in the machine's language, and whether the date is shown at all. A layout transform on the
    /// holder, not other font sizes, so the three lines keep their proportions to each other; WPF draws text under a
    /// transform as text, so nothing is blurred.
    /// </summary>
    private void FitMiddle()
    {
        if (_hands.Length == 0 || ActualWidth <= 0 || ActualHeight <= 0)
        {
            Zoom(1);
            return;
        }

        var thickness = RingThickness;
        var gap = Math.Max(MinGap, thickness * GapToThickness);

        // The inside edge of the innermost ring.
        var inside = Math.Min(ActualWidth, ActualHeight) / 2 - (_hands.Length - 1) * (thickness + gap) - thickness;
        if (inside <= 0)
        {
            Zoom(1);
            return;
        }

        _middle.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        var (width, height) = (_middle.DesiredSize.Width, _middle.DesiredSize.Height);
        var corner = Math.Sqrt(width * width + height * height) / 2;
        if (corner <= 0)
        {
            Zoom(1);
            return;
        }

        if (_fillRings)
        {
            Zoom(FillShare * inside / corner);
            return;
        }

        // Clear of the ring by a gap, so the text never touches it.
        var room = inside - gap;
        Zoom(room > 0 && corner > room ? room / corner : 1);
    }

    private double _zoom = 1;

    /// <summary>Applied only when it changes: this is asked every second, and a new transform each time would lay
    /// the whole piece out again every second for nothing.</summary>
    private void Zoom(double by)
    {
        if (Math.Abs(by - _zoom) < 0.001) return;
        _zoom = by;
        _fit.LayoutTransform = Math.Abs(by - 1) < 0.001 ? Transform.Identity : new ScaleTransform(by, by);
    }

    /// <summary>A share of the preset's gauge thickness (see <see cref="RingShare"/>), and at least a pixel: the
    /// thickness comes from a preset, which a hand can edit to nothing.</summary>
    private double RingThickness => Math.Max(1, _arcThickness * RingShare);

    /// <summary>The ring lines' sizes, from the time's. The line without rings takes the reading's size directly, as
    /// the piece always did.</summary>
    private void Sizes()
    {
        _time.FontSize = _face.FontSize;
        _date.FontSize = _face.FontSize * Small;
        _seconds.FontSize = _face.FontSize * Small;
        FitMiddle();
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

        // The text may have changed width — a new day's name, the time going from 9:59 to 10:00.
        FitMiddle();

        Sweep(now);
    }

    /// <summary>
    /// Moves each ring for the second just shown.
    ///
    /// With animation off, each stands on the second, as the text does. With it on, each is aimed at where it will
    /// be at the NEXT whole second and given exactly the time left until it, so the rings move all the way through
    /// every second and are on time at each one — never a tick behind, which is what sweeping to the second just
    /// shown would be. Forward only: at the top of a turn a ring goes on to full and starts again from nothing,
    /// never back round. Steady, because time is: an eased ring would say a second goes faster at its start.
    ///
    /// The time left is measured on the machine's clock rather than from the second shown, because that is the
    /// clock the movement is drawn on; a second shown late only shortens the move.
    /// </summary>
    private void Sweep(DateTime now)
    {
        if (_hands.Length == 0) return;

        var at = ClockFace.Fractions(now);

        // Off screen — being built, being dressed, or taken away — the rings are set and never sent travelling:
        // a move nobody sees is frames drawn for nobody, and the first move once on screen is set anyway.
        if (!Motion.Enabled || !_hooked)
        {
            for (var k = 0; k < _hands.Length; k++) _hands[k].MoveTo(Hand(at, k), TimeSpan.Zero);
            _fresh = false;
            return;
        }

        var next = ClockFace.NextSecond(now);
        var to = ClockFace.Fractions(next);
        var left = Clamp(next - DateTime.Now);

        for (var k = 0; k < _hands.Length; k++)
        {
            var hand = _hands[k];

            // Put where it would be by now had it been moving all along, and moved on from there.
            if (_fresh)
                hand.MoveTo(RingMotion.At(Hand(at, k), Hand(to, k), 1 - left / OneSecond, forwardOnly: true),
                    TimeSpan.Zero);

            hand.MoveTo(Hand(to, k), left, forwardOnly: true, frameRate: Motion.FrameRate);
        }

        _fresh = false;
    }

    private static double Hand(ClockFace.Hands hands, int ring) => ring switch
    {
        0 => hands.Seconds,
        1 => hands.Minutes,
        _ => hands.Hours,
    };

    /// <summary>The time left, between none and a second: a clock shown late has no time left, and one woken from
    /// a stall is never given longer than a second to catch up.</summary>
    private static TimeSpan Clamp(TimeSpan left) =>
        left < TimeSpan.Zero ? TimeSpan.Zero : left > OneSecond ? OneSecond : left;

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

    private bool _fillRings = true;

    public bool FillRings
    {
        get => _fillRings;
        set { _fillRings = value; FitMiddle(); }
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
