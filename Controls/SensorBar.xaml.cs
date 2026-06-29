using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Aquila.Models;

namespace Aquila.Controls;

/// <summary>
/// Horizontal sensor bar: a single SensorNode rendered through the shared ThinBar progress-bar
/// template (sibling of <see cref="RadialGauge"/> / <see cref="MiniSparkline"/>). Reuses the
/// existing template — it only maps the sensor onto Value/Maximum and applies the accent colour.
/// </summary>
public partial class SensorBar : UserControl
{
    public SensorBar()
    {
        InitializeComponent();
        Loaded += (_, _) => Render();
    }

    public static readonly DependencyProperty SensorProperty =
        DependencyProperty.Register(nameof(Sensor), typeof(SensorNode), typeof(SensorBar),
            new PropertyMetadata(null, OnSensorChanged));

    public static readonly DependencyProperty AccentProperty =
        DependencyProperty.Register(nameof(Accent), typeof(Brush), typeof(SensorBar),
            new PropertyMetadata(null, (d, _) => ((SensorBar)d).Render()));

    public static readonly DependencyProperty MinimumProperty =
        DependencyProperty.Register(nameof(Minimum), typeof(double), typeof(SensorBar),
            new PropertyMetadata(double.NaN, (d, _) => ((SensorBar)d).Render()));

    public static readonly DependencyProperty MaximumProperty =
        DependencyProperty.Register(nameof(Maximum), typeof(double), typeof(SensorBar),
            new PropertyMetadata(double.NaN, (d, _) => ((SensorBar)d).Render()));

    /// <summary>Bar scale minimum. Unset (NaN) → 0.</summary>
    public double Minimum
    {
        get => (double)GetValue(MinimumProperty);
        set => SetValue(MinimumProperty, value);
    }

    /// <summary>Bar scale maximum. Unset (NaN) → 100 for % sensors, else the sensor's observed max.</summary>
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

    /// <summary>Fill colour. Falls back to the CPU accent token when unset.</summary>
    public Brush? Accent
    {
        get => (Brush?)GetValue(AccentProperty);
        set => SetValue(AccentProperty, value);
    }

    private static void OnSensorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var b = (SensorBar)d;
        if (e.OldValue is SensorNode old) old.PropertyChanged -= b.OnSensorPropertyChanged;
        if (e.NewValue is SensorNode now) now.PropertyChanged += b.OnSensorPropertyChanged;
        b.Render();
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

        // Same scale rule as RadialGauge: explicit Min/Max win; otherwise % → 0..100, else the
        // sensor's observed range (Sensor.Min/Max are observed extremes, not the true scale).
        double min = double.IsNaN(Minimum) ? 0 : Minimum;
        double max =
            !double.IsNaN(Maximum) ? Maximum
            : Sensor.Unit == "%" ? 100
            : (Sensor.Max ?? 100);
        if (max <= min) max = min + 1;

        Bar.Foreground = Accent ?? TryFindResource("Aquila.Cpu") as Brush;
        Bar.Minimum = min;
        Bar.Maximum = max;
        Bar.Value = System.Math.Clamp(Sensor.Value ?? 0, min, max);
    }
}
