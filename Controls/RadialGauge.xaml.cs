using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Aquila.Models;
using LiveChartsCore.Defaults;
using SkiaSharp;

namespace Aquila.Controls;

/// <summary>
/// AIDA64-style radial gauge for a single sensor, built on the LiveCharts gauge (AquilaCharts).
/// Bind <see cref="Sensor"/> to a live SensorNode; the gauge animates as the value changes.
/// </summary>
public partial class RadialGauge : UserControl, ISensorPiece, IGaugeStyle, IScaledFigure
{
    private ObservableValue? _point;
    private SKColor _lastColor;

    /// <summary>The look the current series was built with. Whether the number is drawn, its face, colours and
    /// format are all baked into the series when it is generated, so changing any of them means building it
    /// again — but only then, or the gauge would animate from zero on every tick.</summary>
    private (bool ShowValue, SKColor Track, string? Font, TextWeight Weight, SKColor Label, string? Unit,
        string Decimals) _lastStyle;

    /// <summary>The sizes the series was last given. Not among the look above, because they change whenever the
    /// widget's size does, and a resize must not rebuild the dial: they are set on the series as it stands.</summary>
    private (double Thickness, double Corner, double ValueSize, double Scale) _lastSize;

    /// <summary>Rounds the value arc's ends without rebuilding it.</summary>
    private Action<double>? _setCorner;

    /// <summary>Sets the thickness, the corner, the number's size and the scale without rebuilding it.</summary>
    private Action<double, double, double, double>? _resize;

    private double _figureScale = 1;

    /// <summary>For the room LiveCharts keeps round the dial (<see cref="AquilaCharts.SolidGauge"/>), the one size
    /// in this figure that is neither the preset's nor drawn from the box.</summary>
    public double FigureScale
    {
        get => _figureScale;
        set { _figureScale = value; Render(); }
    }

    /// <summary>The share of the dial the arc was last sent to — so the ends can be sized for the shorter of where
    /// it is and where it is going, which is the shortest it will be on the way.</summary>
    private double? _lastFraction;

    public RadialGauge()
    {
        InitializeComponent();
        Loaded += (_, _) => Render();
    }

    public static readonly DependencyProperty SensorProperty =
        DependencyProperty.Register(nameof(Sensor), typeof(SensorNode), typeof(RadialGauge),
            new PropertyMetadata(null, OnSensorChanged));

    public static readonly DependencyProperty AccentProperty =
        DependencyProperty.Register(nameof(Accent), typeof(Brush), typeof(RadialGauge),
            new PropertyMetadata(null, (d, _) => ((RadialGauge)d).Render()));

    public static readonly DependencyProperty TrackBrushProperty =
        DependencyProperty.Register(nameof(TrackBrush), typeof(Brush), typeof(RadialGauge),
            new PropertyMetadata(null, (d, _) => ((RadialGauge)d).Render()));

    /// <summary>The unfilled arc. Unlike the value arc it never follows the reading, so it is a plain
    /// brush and not a ramp — a track that changed colour with the value would read as a second dial.</summary>
    public Brush? TrackBrush
    {
        get => (Brush?)GetValue(TrackBrushProperty);
        set => SetValue(TrackBrushProperty, value);
    }

    public static readonly DependencyProperty ArcThicknessProperty =
        DependencyProperty.Register(nameof(ArcThickness), typeof(double), typeof(RadialGauge),
            new PropertyMetadata(14d, (d, _) => ((RadialGauge)d).Render()));

    public static readonly DependencyProperty ArcCornerProperty =
        DependencyProperty.Register(nameof(ArcCorner), typeof(double), typeof(RadialGauge),
            new PropertyMetadata(0d, (d, _) => ((RadialGauge)d).Render()));

    public static readonly DependencyProperty SweepProperty =
        DependencyProperty.Register(nameof(Sweep), typeof(GaugeSweep), typeof(RadialGauge),
            new PropertyMetadata(GaugeSweep.Dial, (d, _) => ((RadialGauge)d).Render()));

    public static readonly DependencyProperty ShowUnitProperty =
        DependencyProperty.Register(nameof(ShowUnit), typeof(bool), typeof(RadialGauge),
            new PropertyMetadata(true, (d, _) => ((RadialGauge)d).Render()));

    /// <summary>Whether the centre number carries its unit. It is drawn at the reading's own size, because
    /// the label is a single SkiaSharp paint — the stat's separate unit size has nothing to act on here.
    /// </summary>
    public bool ShowUnit
    {
        get => (bool)GetValue(ShowUnitProperty);
        set => SetValue(ShowUnitProperty, value);
    }

    public static readonly DependencyProperty ValueBrushProperty =
        DependencyProperty.Register(nameof(ValueBrush), typeof(Brush), typeof(RadialGauge),
            new PropertyMetadata(null, (d, _) => ((RadialGauge)d).Render()));

    /// <summary>The centre number's colour. Null leaves the built-in near-white.</summary>
    public Brush? ValueBrush
    {
        get => (Brush?)GetValue(ValueBrushProperty);
        set => SetValue(ValueBrushProperty, value);
    }

    public static readonly DependencyProperty ValueFontProperty =
        DependencyProperty.Register(nameof(ValueFont), typeof(string), typeof(RadialGauge),
            new PropertyMetadata(null, (d, _) => ((RadialGauge)d).Render()));

    public static readonly DependencyProperty ValueWeightProperty =
        DependencyProperty.Register(nameof(ValueWeight), typeof(TextWeight), typeof(RadialGauge),
            new PropertyMetadata(TextWeight.Regular, (d, _) => ((RadialGauge)d).Render()));

    /// <summary>The centre number's face. It reaches SkiaSharp rather than WPF, which is why it never
    /// followed the font inherited down the visual tree — the gauge's label is painted, not laid out.</summary>
    public string? ValueFont
    {
        get => (string?)GetValue(ValueFontProperty);
        set => SetValue(ValueFontProperty, value);
    }

    public TextWeight ValueWeight
    {
        get => (TextWeight)GetValue(ValueWeightProperty);
        set => SetValue(ValueWeightProperty, value);
    }

    public static readonly DependencyProperty ValueSizeProperty =
        DependencyProperty.Register(nameof(ValueSize), typeof(double), typeof(RadialGauge),
            new PropertyMetadata(24d, (d, _) => ((RadialGauge)d).Render()));

    public static readonly DependencyProperty ShowValueProperty =
        DependencyProperty.Register(nameof(ShowValue), typeof(bool), typeof(RadialGauge),
            new PropertyMetadata(true, (d, _) => ((RadialGauge)d).Render()));

    /// <summary>Thickness of the arc, in DIPs.</summary>
    public double ArcThickness
    {
        get => (double)GetValue(ArcThicknessProperty);
        set => SetValue(ArcThicknessProperty, value);
    }

    /// <summary>Rounding on the ends of the arc; clamped to half the thickness.</summary>
    public double ArcCorner
    {
        get => (double)GetValue(ArcCornerProperty);
        set => SetValue(ArcCornerProperty, value);
    }

    /// <summary>How far round the dial goes.</summary>
    public GaugeSweep Sweep
    {
        get => (GaugeSweep)GetValue(SweepProperty);
        set => SetValue(SweepProperty, value);
    }

    /// <summary>Point size of the number in the middle.</summary>
    public double ValueSize
    {
        get => (double)GetValue(ValueSizeProperty);
        set => SetValue(ValueSizeProperty, value);
    }

    /// <summary>Whether the number in the middle is drawn.</summary>
    public bool ShowValue
    {
        get => (bool)GetValue(ShowValueProperty);
        set => SetValue(ShowValueProperty, value);
    }

    public static readonly DependencyProperty MinimumProperty =
        DependencyProperty.Register(nameof(Minimum), typeof(double), typeof(RadialGauge),
            new PropertyMetadata(double.NaN, (d, _) => ((RadialGauge)d).Render()));

    public static readonly DependencyProperty MaximumProperty =
        DependencyProperty.Register(nameof(Maximum), typeof(double), typeof(RadialGauge),
            new PropertyMetadata(double.NaN, (d, _) => ((RadialGauge)d).Render()));

    /// <summary>Gauge scale minimum. Unset (NaN) → 0.</summary>
    public double Minimum
    {
        get => (double)GetValue(MinimumProperty);
        set => SetValue(MinimumProperty, value);
    }

    /// <summary>Gauge scale maximum. Unset (NaN) → 100 for % sensors, else the sensor's observed max.</summary>
    public double Maximum
    {
        get => (double)GetValue(MaximumProperty);
        set => SetValue(MaximumProperty, value);
    }

    /// <summary>Live sensor to display.</summary>
    public SensorNode? Sensor
    {
        get => (SensorNode?)GetValue(SensorProperty);
        set => SetValue(SensorProperty, value);
    }

    /// <summary>Arc colour. Falls back to the CPU accent token when unset.</summary>
    public Brush? Accent
    {
        get => (Brush?)GetValue(AccentProperty);
        set => SetValue(AccentProperty, value);
    }

    private static void OnSensorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var g = (RadialGauge)d;
        if (e.OldValue is SensorNode old) old.PropertyChanged -= g.OnSensorPropertyChanged;
        if (e.NewValue is SensorNode now) now.PropertyChanged += g.OnSensorPropertyChanged;
        g.Render();
    }

    private void OnSensorPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        Render();
    }

    private void Render()
    {
        if (!IsLoaded || Sensor is null)
            return;

        double value = Sensor.Value ?? 0;

        double min = double.IsNaN(Minimum) ? 0 : Minimum;
        double max = SensorScale.Ceiling(Sensor, Maximum);
        if (max <= min) max = min + 1;
        value = System.Math.Clamp(value, min, max);

        // Set on the CHART and not on the series: the series bakes its style in when it is generated,
        // so putting the speed there would rebuild the gauge — and a rebuilt gauge animates up from zero.
        // Assigned only when it differs, because this runs on every tick.
        if (Chart.AnimationsSpeed != Motion.ChartSpeed) Chart.AnimationsSpeed = Motion.ChartSpeed;
        if (!ReferenceEquals(Chart.EasingFunction, Motion.ChartEasing))
            Chart.EasingFunction = Motion.ChartEasing;

        var color = ResolveColor();
        var track = TrackBrush is SolidColorBrush t
            ? new SKColor(t.Color.R, t.Color.G, t.Color.B, (byte)(t.Color.A * t.Opacity))
            : new SKColor(255, 255, 255, 20);
        var label = ValueBrush is SolidColorBrush l
            ? new SKColor(l.Color.R, l.Color.G, l.Color.B, (byte)(l.Color.A * l.Opacity))
            : new SKColor(235, 235, 235);
        // Both baked into the series when it is generated, so they belong in the comparison below or a
        // preset that turns the unit off would not be noticed until something else forced a rebuild.
        var unit = ShowUnit ? Sensor.Unit : null;
        var decimals = Sensor.Decimals();
        var style = (ShowValue, track, ValueFont, ValueWeight, label, unit, decimals);
        var size = (ArcThickness, ArcCorner, ValueSize, _figureScale);

        // The start angle and the sweep have to be set together — 270 degrees starting at the top puts the
        // gap on the right, which reads as a broken ring rather than a dial.
        (Chart.InitialRotation, Chart.MaxAngle) = Sweep switch
        {
            GaugeSweep.Half => (-180d, 180d),
            GaugeSweep.Ring => (-90d, 360d),
            _ => (-225d, 270d),
        };

        // Build the series once; afterwards only update the point's value so LiveCharts animates
        // smoothly from the current value instead of resetting to zero each tick. Colour and the baked-in
        // style are the only things that can force it to be built again.
        if (_point is null || color != _lastColor || style != _lastStyle)
        {
            var (series, point, setCorner, resize) =
                AquilaCharts.SolidGauge(color, ArcThickness, ValueSize, ShowValue, ArcCorner, track,
                    ValueFont, ValueWeight, label, unit, decimals, _figureScale);
            _point = point;
            _setCorner = setCorner;
            _resize = resize;
            _lastFraction = null;
            _lastColor = color;
            _lastStyle = style;
            _lastSize = size;
            Chart.Series = series;
        }
        else if (size != _lastSize)
        {
            _resize?.Invoke(ArcThickness, ArcCorner, ValueSize, _figureScale);
            _lastSize = size;
        }

        // Also on the series, not only on the chart above. A series that names no speed is documented to
        // inherit the chart's, but that could not be confirmed from the assembly, and a setting that
        // silently does nothing is worse than one that is set twice. Plain property assignment on ISeries,
        // so nothing is regenerated and the arc does not restart.
        foreach (var s in Chart.Series ?? [])
        {
            if (s.AnimationsSpeed != Motion.ChartSpeed) s.AnimationsSpeed = Motion.ChartSpeed;
            if (!ReferenceEquals(s.EasingFunction, Motion.ChartEasing)) s.EasingFunction = Motion.ChartEasing;
        }

        // Ends as round as the arc can hold — sized for the shorter of where it is and where it is going, since
        // a reading that falls sweeps the arc down to the new length on the way. The inner radius is the one
        // LiveCharts drew last; before the first drawing it is unknown, and a low reading starts square for a poll.
        var fraction = (value - min) / (max - min);
        _setCorner?.Invoke(AquilaCharts.CornerFor(Math.Min(fraction, _lastFraction ?? fraction), ArcCorner,
            Chart.MaxAngle, AquilaCharts.InnerRadiusOf(_point) ?? 0));
        _lastFraction = fraction;

        Chart.MaxValue = max - min;
        _point.Value = value - min;
    }

    private SKColor ResolveColor()
    {
        var brush = Accent as SolidColorBrush
            ?? TryFindResource("Aquila.Scheme.Accent") as SolidColorBrush;
        if (brush is null) return new SKColor(96, 205, 255);
        var c = brush.Color;
        return new SKColor(c.R, c.G, c.B, c.A);
    }
}
