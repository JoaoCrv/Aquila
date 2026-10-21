using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using Aquila.Controls.Primitives;
using Aquila.Models;

namespace Aquila.Controls;

/// <summary>
/// A dial: one reading as an arc over its track, and the number in the middle.
///
/// Drawn by <see cref="RingArc"/>, as the clock's rings are. It was a LiveCharts gauge, and the chart cost the dial
/// three things it could not get back. It kept a margin round the figure for a slice to push out into on hover
/// — a dial has nothing to hover — so the arc never reached the edges of its box. Its round ends were fitted inside
/// the arc and had to be sized for how long the arc was, squaring off and glitching on a short one. And its number
/// was painted by Skia, which inherits nothing from the visual tree, so the face, the weight and the colour each had
/// to be carried to it by hand. Here the ring is placed by what it sweeps (<see cref="RingFit.Sweep"/>), its ends
/// are the pen's, and the number is a TextBlock like every other piece's.
///
/// The number is set, never animated — a digit counting up to the reading is wrong while it counts — and only the
/// arc travels, at the machine's speed and frame rate (<see cref="Motion"/>). Steadily rather than eased, because
/// that is how the ring moves: below the speeds Motion allows, the curve was measured not to show.
///
/// The ring sits under the number, so a widget too small for both never draws the arc over the reading.
/// </summary>
public sealed class RadialGauge : Panel, ISensorPiece, IGaugeStyle
{
    /// <summary>Where nothing names the arc's colour: the dashboard's accent, and failing that a plain blue.</summary>
    private static readonly Brush FallbackAccent = Frozen(Color.FromRgb(96, 205, 255));

    private readonly RingArc _ring = new() { Fit = RingFit.Sweep };
    private readonly TextBlock _number = new() { TextAlignment = TextAlignment.Center };

    /// <summary>The share of the dial the arc was last sent to, so a restyle or a reading that has not moved does
    /// not start a sweep over again — restarting it from where it stands would only slow it.</summary>
    private double _shown = double.NaN;

    /// <summary>Whether the next reading puts the arc in place rather than taking it there: the first after the dial
    /// comes on screen, or is given a sensor. It starts from wherever the arc happens to be, and a sweep up from
    /// nothing would be animating something that never happened.</summary>
    private bool _fresh = true;

    public RadialGauge()
    {
        // Hit-testable across its whole box, as the chart was: grabbing a dial in edit mode should not depend on
        // hitting the arc.
        Background = Brushes.Transparent;

        // Digits of one width, so the number does not shuffle sideways each time a digit changes.
        Typography.SetNumeralAlignment(_number, FontNumeralAlignment.Tabular);

        Children.Add(_ring);
        Children.Add(_number);

        // Painted again on the way in, for a dial nobody gives an accent: the dashboard's is a resource, and a
        // resource is only found once the dial is somewhere to look it up from.
        Loaded += (_, _) =>
        {
            _fresh = true;
            Paint();
            Show();
        };

        // A sweep still travelling off screen is frames drawn for nobody.
        Unloaded += (_, _) => _ring.Stop();

        Shape();
        Paint();
        Write();
    }

    // ── Layout: the ring fills the box, the number sits at its centre ────

    protected override Size MeasureOverride(Size available)
    {
        _number.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));

        // The number's line is kept inside the box with the arc — which only ever binds for a half dial, whose centre
        // is the arc's bottom edge. Its height and not its width: the height is the face's and the size's, while the
        // width changes with the reading, and a dial that shrank when a reading gained a digit would twitch.
        var middle = new Size(0, _number.DesiredSize.Height);
        if (_ring.CentreSize != middle) _ring.CentreSize = middle;

        _ring.Measure(available);

        // As big as the number, and otherwise whatever it is given: the arc has no size of its own to ask for.
        return _number.DesiredSize;
    }

    /// <summary>The number goes where the ring says its centre is, which for a fitted dial is not the middle of the
    /// box: a 270° arc's circle sits low in it, with the open bottom below.</summary>
    protected override Size ArrangeOverride(Size finalSize)
    {
        _ring.Arrange(new Rect(finalSize));

        var centre = _ring.Centre;
        var size = _number.DesiredSize;
        _number.Arrange(new Rect(new Point(centre.X - size.Width / 2, centre.Y - size.Height / 2), size));

        return finalSize;
    }

    // ── The reading ──────────────────────────────────────────────────────

    public static readonly DependencyProperty SensorProperty =
        DependencyProperty.Register(nameof(Sensor), typeof(SensorNode), typeof(RadialGauge),
            new PropertyMetadata(null, OnSensorChanged));

    /// <summary>Live sensor to display.</summary>
    public SensorNode? Sensor
    {
        get => (SensorNode?)GetValue(SensorProperty);
        set => SetValue(SensorProperty, value);
    }

    public static readonly DependencyProperty AccentProperty =
        DependencyProperty.Register(nameof(Accent), typeof(Brush), typeof(RadialGauge),
            new PropertyMetadata(null, (d, _) => ((RadialGauge)d).Paint()));

    /// <summary>The arc's colour — the reading's, judged by its ramp. Falls back to the dashboard's accent when
    /// unset.</summary>
    public Brush? Accent
    {
        get => (Brush?)GetValue(AccentProperty);
        set => SetValue(AccentProperty, value);
    }

    public static readonly DependencyProperty MinimumProperty =
        DependencyProperty.Register(nameof(Minimum), typeof(double), typeof(RadialGauge),
            new PropertyMetadata(double.NaN, (d, _) => ((RadialGauge)d).Show()));

    public static readonly DependencyProperty MaximumProperty =
        DependencyProperty.Register(nameof(Maximum), typeof(double), typeof(RadialGauge),
            new PropertyMetadata(double.NaN, (d, _) => ((RadialGauge)d).Show()));

    /// <summary>Scale minimum. Unset (NaN) → 0.</summary>
    public double Minimum
    {
        get => (double)GetValue(MinimumProperty);
        set => SetValue(MinimumProperty, value);
    }

    /// <summary>Scale maximum. Unset (NaN) → the sensor's own, by <see cref="SensorScale.Ceiling"/>.</summary>
    public double Maximum
    {
        get => (double)GetValue(MaximumProperty);
        set => SetValue(MaximumProperty, value);
    }

    private static void OnSensorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var gauge = (RadialGauge)d;
        if (e.OldValue is SensorNode old) old.PropertyChanged -= gauge.OnSensorPropertyChanged;
        if (e.NewValue is SensorNode now) now.PropertyChanged += gauge.OnSensorPropertyChanged;

        // Another sensor's reading is not somewhere to travel from.
        gauge._fresh = true;
        gauge.Show();
    }

    private void OnSensorPropertyChanged(object? sender, PropertyChangedEventArgs e) => Show();

    /// <summary>
    /// Writes the reading and moves the arc to it.
    ///
    /// The number is the sensor's own, written by <see cref="SensorFormat"/> — so a reading past the top of the
    /// scale says what it is while the arc stands full. The arc is that reading's share of the scale.
    ///
    /// Off screen, fresh, or with motion off, the arc is set where it belongs. Otherwise it is sent there from
    /// wherever it is drawn, so a reading landing mid-sweep bends the path rather than jumping it.
    /// </summary>
    private void Show()
    {
        if (Sensor is not { } sensor) return;

        _number.Text = sensor.Reading(_showUnit);

        var min = double.IsNaN(Minimum) ? 0 : Minimum;
        var max = SensorScale.Ceiling(sensor, Maximum);
        if (max <= min) max = min + 1;

        var share = (Math.Clamp(sensor.Value ?? 0, min, max) - min) / (max - min);

        if (_fresh || !IsLoaded || !Motion.Enabled)
        {
            _ring.MoveTo(share, TimeSpan.Zero);
            _fresh = !IsLoaded;
        }
        else if (!share.Equals(_shown))
            _ring.MoveTo(share, Motion.Speed, forwardOnly: false, frameRate: Motion.FrameRate);

        _shown = share;
    }

    // ── Style: what the preset says it looks like ────────────────────────

    private double _arcThickness = 14;
    private double _arcCorner;
    private Brush? _trackBrush;
    private GaugeSweep _sweep = GaugeSweep.Dial;
    private double _valueSize = 24;
    private string? _valueFont;
    private TextWeight _valueWeight = TextWeight.Regular;
    private bool _showValue = true;
    private bool _showUnit = true;

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

    /// <summary>The unfilled arc. Unlike the value arc it never follows the reading, so it is a plain brush and not a
    /// ramp — a track that changed colour with the value would read as a second dial.</summary>
    public Brush? TrackBrush
    {
        get => _trackBrush;
        set { _trackBrush = value; Paint(); }
    }

    /// <summary>How far round the dial goes.</summary>
    public GaugeSweep Sweep
    {
        get => _sweep;
        set { _sweep = value; Shape(); }
    }

    /// <summary>Point size of the number in the middle.</summary>
    public double ValueSize
    {
        get => _valueSize;
        set { _valueSize = value; Write(); }
    }

    public string? ValueFont
    {
        get => _valueFont;
        set { _valueFont = value; Write(); }
    }

    public TextWeight ValueWeight
    {
        get => _valueWeight;
        set { _valueWeight = value; Write(); }
    }

    /// <summary>Whether the number in the middle is drawn. Without it the dial is an arc alone, with the widget's own
    /// title above it.</summary>
    public bool ShowValue
    {
        get => _showValue;
        set { _showValue = value; Write(); }
    }

    /// <summary>Whether the number carries its unit. Drawn at the reading's own size: the stat's separate unit size is
    /// for a unit set as a piece of its own, and this one is a single line.</summary>
    public bool ShowUnit
    {
        get => _showUnit;
        set { _showUnit = value; Show(); }
    }

    /// <summary>
    /// How thick the arc is, how its ends are drawn, and how far round it goes.
    ///
    /// The ends are round or square, never in between — the clock's rule, for the clock's reason: the preset's
    /// corner was a radius the chart had to fit inside the arc, and a pen's round end is a cap past the arc's end, so
    /// there is nothing for a radius to fit. Any corner at all asks for round.
    ///
    /// The start and the sweep are set together: 270 degrees starting at the top puts the gap on the right, which
    /// reads as a broken ring rather than a dial.
    /// </summary>
    private void Shape()
    {
        _ring.Thickness = Math.Max(1, _arcThickness);
        _ring.Caps = _arcCorner > 0 ? PenLineCap.Round : PenLineCap.Flat;

        (_ring.StartAngle, _ring.SweepAngle) = _sweep switch
        {
            GaugeSweep.Half => (-180d, 180d),
            GaugeSweep.Ring => (-90d, 360d),
            _ => (-225d, 270d),
        };
    }

    /// <summary>
    /// The arc's colour and the track's.
    ///
    /// The arc's is compared by colour before it is handed on, because the ramp hands over a new brush of the same
    /// colour on every reading, and a new brush is a new pen and a redraw for nothing.
    /// </summary>
    private void Paint()
    {
        var accent = Accent ?? TryFindResource("Aquila.Scheme.Accent") as Brush ?? FallbackAccent;
        if (!SameColour(_ring.Stroke, accent)) _ring.Stroke = accent;

        _ring.TrackBrush = _trackBrush ?? ArcStyle.DefaultTrack;
    }

    /// <summary>The number's size, face and whether it is there. Its colour is inherited — the tile's foreground,
    /// which is the preset's value colour — as every other piece's text is.</summary>
    private void Write()
    {
        _number.FontSize = _valueSize > 0 ? _valueSize : 1;
        _number.Wear(_valueFont, _valueWeight);
        _number.Visibility = _showValue ? Visibility.Visible : Visibility.Collapsed;
    }

    private static bool SameColour(Brush? a, Brush b) =>
        a is SolidColorBrush x && b is SolidColorBrush y && x.Color == y.Color && x.Opacity == y.Opacity;

    private static Brush Frozen(Color colour)
    {
        var brush = new SolidColorBrush(colour);
        brush.Freeze();
        return brush;
    }
}
