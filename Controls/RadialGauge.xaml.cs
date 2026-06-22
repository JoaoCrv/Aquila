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
public partial class RadialGauge : UserControl
{
    private ObservableValue? _point;
    private SKColor _lastColor;

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
        if (!Dispatcher.CheckAccess()) { Dispatcher.Invoke(Render); return; }
        Render();
    }

    private void Render()
    {
        if (!IsLoaded || Sensor is null)
            return;

        double value = Sensor.Value ?? 0;

        // Scale: explicit Min/Max win; otherwise % sensors are 0..100, others fall back to the
        // sensor's observed range. (Sensor.Min/Max are observed extremes, not the true scale, so
        // they must not drive a percentage gauge.)
        double min = double.IsNaN(Minimum) ? 0 : Minimum;
        double max =
            !double.IsNaN(Maximum) ? Maximum
            : Sensor.Unit == "%" ? 100
            : (Sensor.Max ?? 100);
        if (max <= min) max = min + 1;
        value = System.Math.Clamp(value, min, max);

        var color = ResolveColor();

        // Build the series once; afterwards only update the point's value so LiveCharts animates
        // smoothly from the current value instead of resetting to zero each tick.
        if (_point is null || color != _lastColor)
        {
            var (series, point) = AquilaCharts.SolidGauge(color);
            _point = point;
            _lastColor = color;
            Chart.Series = series;
        }

        Chart.MaxValue = max - min;
        _point.Value = value - min;
    }

    private SKColor ResolveColor()
    {
        var brush = Accent as SolidColorBrush
            ?? TryFindResource("Aquila.Cpu") as SolidColorBrush;
        if (brush is null) return new SKColor(96, 205, 255);
        var c = brush.Color;
        return new SKColor(c.R, c.G, c.B, c.A);
    }
}
