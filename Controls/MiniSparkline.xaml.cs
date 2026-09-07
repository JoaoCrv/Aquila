using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Aquila.Models;

namespace Aquila.Controls;

/// <summary>
/// Live sparkline for a single sensor: the line comes from <see cref="SensorNode.History"/>, the
/// number from <see cref="SensorNode.Value"/>. A thin wrapper over <see cref="SparklineChart"/> so
/// the dashboard and desktop widgets share one piece (sibling of <see cref="RadialGauge"/>).
/// </summary>
public partial class MiniSparkline : UserControl, ISensorPiece, IChartStyle, IValueStyle
{
    public MiniSparkline()
    {
        InitializeComponent();
        Loaded += (_, _) => Render();
    }

    public static readonly DependencyProperty SensorProperty =
        DependencyProperty.Register(nameof(Sensor), typeof(SensorNode), typeof(MiniSparkline),
            new PropertyMetadata(null, OnSensorChanged));

    public static readonly DependencyProperty AccentProperty =
        DependencyProperty.Register(nameof(Accent), typeof(Brush), typeof(MiniSparkline),
            new PropertyMetadata(null, (d, _) => ((MiniSparkline)d).Render()));

    /// <summary>Live sensor to display.</summary>
    public SensorNode? Sensor
    {
        get => (SensorNode?)GetValue(SensorProperty);
        set => SetValue(SensorProperty, value);
    }

    /// <summary>Line colour. Falls back to the SparklineChart default when unset.</summary>
    public Brush? Accent
    {
        get => (Brush?)GetValue(AccentProperty);
        set => SetValue(AccentProperty, value);
    }

    // Straight through to the chart inside, with no properties of its own. Spark exists from
    // InitializeComponent onwards, so there is no window in which a value could be set and lost — which is
    // exactly the trap Render falls into, having to wait for IsLoaded before it can touch anything.
    /// <summary>The number drawn over the line. Its size used to be fixed in the XAML, which is why the
    /// preset could not reach it.</summary>
    public double ValueSize { get => ValueText.FontSize; set => ValueText.FontSize = value; }

    private string? _valueFont;
    private TextWeight _valueWeight = TextWeight.Regular;

    public string? ValueFont
    {
        get => _valueFont;
        set { _valueFont = value; ValueText.Wear(_valueFont, _valueWeight); }
    }

    public TextWeight ValueWeight
    {
        get => _valueWeight;
        set { _valueWeight = value; ValueText.Wear(_valueFont, _valueWeight); }
    }

    public double LineThickness { get => Spark.LineThickness; set => Spark.LineThickness = value; }
    public Models.ChartFill Fill { get => Spark.Fill; set => Spark.Fill = value; }
    public double Smoothness { get => Spark.Smoothness; set => Spark.Smoothness = value; }
    public double PointSize { get => Spark.PointSize; set => Spark.PointSize = value; }
    public int PointCount { get => Spark.PointCount; set => Spark.PointCount = value; }
    public double FillOpacity { get => Spark.FillOpacity; set => Spark.FillOpacity = value; }
    public Models.ChartScale Scale { get => Spark.Scale; set => Spark.Scale = value; }
    public double ScaleMin { get => Spark.ScaleMin; set => Spark.ScaleMin = value; }
    public double ScaleMax { get => Spark.ScaleMax; set => Spark.ScaleMax = value; }

    private static void OnSensorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var s = (MiniSparkline)d;
        if (e.OldValue is SensorNode old) old.PropertyChanged -= s.OnSensorPropertyChanged;
        if (e.NewValue is SensorNode now) now.PropertyChanged += s.OnSensorPropertyChanged;
        s.Render();
    }

    private void OnSensorPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        // The line redraws itself from the History collection; we only refresh the number here.
        if (!Dispatcher.CheckAccess()) { Dispatcher.Invoke(UpdateValue); return; }
        UpdateValue();
    }

    private void Render()
    {
        if (!IsLoaded || Sensor is null)
            return;

        Spark.SeriesColor = Accent;
        Spark.MaxY = Sensor.Unit == "%" ? 100 : double.NaN; // % sensors share one 0..100 scale
        Spark.Values = Sensor.History;
        UpdateValue();
    }

    private void UpdateValue() =>
        ValueText.Text = Sensor?.Value is { } v ? $"{v:F0} {Sensor.Unit}" : string.Empty;
}
