using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Aquila.Models;

namespace Aquila.Controls;

/// <summary>
/// Reusable area-sparkline backed by LiveCharts2.
/// Accepts WPF <see cref="Brush"/> colours via <c>DynamicResource</c>;
/// converts to SkiaSharp internally.
/// Supports an optional second series (e.g. Network download + upload).
/// </summary>
/// <example>
/// <code>
/// &lt;controls:SparklineChart Height="64"
///     Values="{Binding CpuUsageHistory}"
///     SeriesColor="{DynamicResource Aquila.Scheme.Series1}"
///     MaxY="100"/&gt;
/// </code>
/// </example>
public partial class SparklineChart : UserControl, IChartStyle
{
    private LineSeries<double>? _primary;
    private LineSeries<double>? _secondary;

    /// <summary>Kept so the scale and the window can be changed without rebuilding the chart: the axes are
    /// created once with the series, but their limits move whenever the user changes either.</summary>
    private Axis? _yAxis;
    private Axis? _xAxis;

    // Alpha at the TOP of the area gradient, where it meets the line. Higher than the old flat fill
    // used, because the gradient gives it all back to transparency before reaching the axis.
    private const byte PrimaryFillAlpha = 80;
    private const byte SecondaryFillAlpha = 55;

    // ── Dependency properties ──────────────────────────────────────

    public static readonly DependencyProperty ValuesProperty =
        DependencyProperty.Register(nameof(Values), typeof(IReadOnlyCollection<double>), typeof(SparklineChart),
            new PropertyMetadata(null, OnPropertyInvalidated));

    public static readonly DependencyProperty SeriesColorProperty =
        DependencyProperty.Register(nameof(SeriesColor), typeof(Brush), typeof(SparklineChart),
            new PropertyMetadata(null, OnPropertyInvalidated));

    public static readonly DependencyProperty MaxYProperty =
        DependencyProperty.Register(nameof(MaxY), typeof(double), typeof(SparklineChart),
            new PropertyMetadata(double.NaN, OnPropertyInvalidated));

    public static readonly DependencyProperty PointCountProperty =
        DependencyProperty.Register(nameof(PointCount), typeof(int), typeof(SparklineChart),
            new PropertyMetadata(60, OnPropertyInvalidated));

    public static readonly DependencyProperty SecondValuesProperty =
        DependencyProperty.Register(nameof(SecondValues), typeof(IReadOnlyCollection<double>), typeof(SparklineChart),
            new PropertyMetadata(null, OnPropertyInvalidated));

    public static readonly DependencyProperty LineThicknessProperty =
        DependencyProperty.Register(nameof(LineThickness), typeof(double), typeof(SparklineChart),
            new PropertyMetadata(1.5, OnPropertyInvalidated));

    public static readonly DependencyProperty FillProperty =
        DependencyProperty.Register(nameof(Fill), typeof(ChartFill), typeof(SparklineChart),
            new PropertyMetadata(ChartFill.Gradient, OnPropertyInvalidated));

    public static readonly DependencyProperty FillOpacityProperty =
        DependencyProperty.Register(nameof(FillOpacity), typeof(double), typeof(SparklineChart),
            new PropertyMetadata(0.30, OnPropertyInvalidated));

    public static readonly DependencyProperty SmoothnessProperty =
        DependencyProperty.Register(nameof(Smoothness), typeof(double), typeof(SparklineChart),
            new PropertyMetadata(0.5, OnPropertyInvalidated));

    public static readonly DependencyProperty ScaleProperty =
        DependencyProperty.Register(nameof(Scale), typeof(ChartScale), typeof(SparklineChart),
            new PropertyMetadata(ChartScale.FromZero, OnPropertyInvalidated));

    public static readonly DependencyProperty ScaleMinProperty =
        DependencyProperty.Register(nameof(ScaleMin), typeof(double), typeof(SparklineChart),
            new PropertyMetadata(0d, OnPropertyInvalidated));

    public static readonly DependencyProperty ScaleMaxProperty =
        DependencyProperty.Register(nameof(ScaleMax), typeof(double), typeof(SparklineChart),
            new PropertyMetadata(100d, OnPropertyInvalidated));

    public static readonly DependencyProperty PointSizeProperty =
        DependencyProperty.Register(nameof(PointSize), typeof(double), typeof(SparklineChart),
            new PropertyMetadata(0d, OnPropertyInvalidated));

    public static readonly DependencyProperty SecondColorProperty =
        DependencyProperty.Register(nameof(SecondColor), typeof(Brush), typeof(SparklineChart),
            new PropertyMetadata(null, OnPropertyInvalidated));

    // ── CLR wrappers ───────────────────────────────────────────────

    /// <summary>Primary data source (e.g. <c>ObservableCollection&lt;double&gt;</c>).</summary>
    public IReadOnlyCollection<double>? Values
    {
        get => (IReadOnlyCollection<double>?)GetValue(ValuesProperty);
        set => SetValue(ValuesProperty, value);
    }

    /// <summary>WPF <see cref="SolidColorBrush"/> for the primary series stroke &amp; fill.</summary>
    public Brush? SeriesColor
    {
        get => (Brush?)GetValue(SeriesColorProperty);
        set => SetValue(SeriesColorProperty, value);
    }

    /// <summary>Fixed Y-axis ceiling.  Leave unset (<c>NaN</c>) for auto-scale.</summary>
    public double MaxY
    {
        get => (double)GetValue(MaxYProperty);
        set => SetValue(MaxYProperty, value);
    }

    /// <summary>Number of data points on the X axis (default 60).</summary>
    public int PointCount
    {
        get => (int)GetValue(PointCountProperty);
        set => SetValue(PointCountProperty, value);
    }

    /// <summary>Optional second data source (dual-series sparkline).</summary>
    public IReadOnlyCollection<double>? SecondValues
    {
        get => (IReadOnlyCollection<double>?)GetValue(SecondValuesProperty);
        set => SetValue(SecondValuesProperty, value);
    }

    /// <summary>Line width in DIPs.</summary>
    public double LineThickness
    {
        get => (double)GetValue(LineThicknessProperty);
        set => SetValue(LineThicknessProperty, value);
    }

    /// <summary>How the area under the line is filled.</summary>
    public ChartFill Fill
    {
        get => (ChartFill)GetValue(FillProperty);
        set => SetValue(FillProperty, value);
    }

    /// <summary>How opaque the area under the line starts, at the line itself.</summary>
    public double FillOpacity
    {
        get => (double)GetValue(FillOpacityProperty);
        set => SetValue(FillOpacityProperty, value);
    }

    /// <summary>0 = straight segments between readings, 1 = a fully rounded curve.</summary>
    public double Smoothness
    {
        get => (double)GetValue(SmoothnessProperty);
        set => SetValue(SmoothnessProperty, value);
    }

    /// <summary>How the vertical scale is decided.</summary>
    public ChartScale Scale
    {
        get => (ChartScale)GetValue(ScaleProperty);
        set => SetValue(ScaleProperty, value);
    }

    /// <summary>Bottom of the scale in <see cref="ChartScale.Manual"/>.</summary>
    public double ScaleMin
    {
        get => (double)GetValue(ScaleMinProperty);
        set => SetValue(ScaleMinProperty, value);
    }

    /// <summary>Top of the scale in <see cref="ChartScale.Manual"/>.</summary>
    public double ScaleMax
    {
        get => (double)GetValue(ScaleMaxProperty);
        set => SetValue(ScaleMaxProperty, value);
    }

    /// <summary>Diameter of the dot at each reading; 0 hides them.</summary>
    public double PointSize
    {
        get => (double)GetValue(PointSizeProperty);
        set => SetValue(PointSizeProperty, value);
    }

    /// <summary>WPF <see cref="SolidColorBrush"/> for the optional second series.</summary>
    public Brush? SecondColor
    {
        get => (Brush?)GetValue(SecondColorProperty);
        set => SetValue(SecondColorProperty, value);
    }

    // ── Constructor ────────────────────────────────────────────────

    public SparklineChart()
    {
        InitializeComponent();
        Loaded   += (_, _) => Rebuild();
        Unloaded += (_, _) => Teardown();
    }

    // ── Core logic ─────────────────────────────────────────────────

    private static void OnPropertyInvalidated(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is SparklineChart sc && sc.IsLoaded)
            sc.Rebuild();
    }

    private void Rebuild()
    {
        var values = Values;
        if (values == null) return;

        if (_primary == null)
        {
            _primary = MakeLine(values);

            var series = new List<ISeries> { _primary };

            if (SecondValues is { } sv)
            {
                _secondary = MakeLine(sv);
                series.Add(_secondary);
            }

            _yAxis = new Axis { IsVisible = false };
            _xAxis = new Axis { IsVisible = false, MinLimit = 0 };

            Chart.Series = series;
            Chart.XAxes  = [_xAxis];
            Chart.YAxes  = [_yAxis];
        }
        else
        {
            // Hot path: the Values binding stays the same object, so only the collection is re-pointed.
            _primary.Values = values;
            if (_secondary != null && SecondValues is { } sv) _secondary.Values = sv;
        }

        // Applied on BOTH paths. Colour, width, fill, smoothness and point size all change far more often
        // than the shape of the chart does — they are what the user drags sliders over — and re-making a
        // few paints costs nothing next to tearing down the series and starting the line from empty.
        // The fill's opacity is the preset's, not two constants tuned by hand — a second line keeps the
        // lighter share it always had, expressed as a fraction of the first rather than a number of its own.
        var alpha = (byte)Math.Clamp(FillOpacity * 255, 0, 255);
        ApplyStyle(_primary, ToSKColor(SeriesColor), alpha);
        if (_secondary != null) ApplyStyle(_secondary, ToSKColor(SecondColor), (byte)(alpha * 0.7));

        ApplyScale();
    }

    /// <summary>
    /// Sets the ends of the vertical scale. Null is LiveCharts' own "fit to what is plotted", which is why
    /// FitToData is simply both ends left unset rather than anything computed here.
    ///
    /// Applied on every Rebuild rather than baked into the axis at construction, so changing the scale is a
    /// style change like any other and never costs a rebuild of the series.
    /// </summary>
    private void ApplyScale()
    {
        // The window is the X axis' ceiling. Held rather than baked for the same reason the scale is: a
        // longer trend must not cost a rebuild, which would throw away the history it exists to show.
        if (_xAxis is not null) _xAxis.MaxLimit = Math.Max(1, PointCount - 1);

        if (_yAxis is null) return;

        // Every arm typed as (double?, double?): a bare null has no type of its own, so leaving one
        // untyped gives the switch no common type to settle on.
        (_yAxis.MinLimit, _yAxis.MaxLimit) = Scale switch
        {
            ChartScale.FitToData => ((double?)null, (double?)null),

            // Guarded: a max at or below the min collapses the plot area, and the editor lets the two boxes
            // be typed into in any order — passing through the moment they cross would look like a crash.
            ChartScale.Manual when ScaleMax > ScaleMin => ((double?)ScaleMin, (double?)ScaleMax),

            _ => ((double?)0d, double.IsNaN(MaxY) ? null : (double?)MaxY),
        };
    }

    private void Teardown()
    {
        if (_primary != null)
        {
            DisposePaints(_primary);
            _primary.Values = null;
            _primary = null;
        }
        if (_secondary != null)
        {
            DisposePaints(_secondary);
            _secondary.Values = null;
            _secondary = null;
        }
        Chart.Series = [];
    }

    // ── Helpers ────────────────────────────────────────────────────

    /// <summary>
    /// Area fill: a vertical gradient from the series colour down to nothing.
    ///
    /// A flat semi-transparent fill only looks like a chart when the line is low. Let the value sit
    /// high and steady — memory at half, a die temperature parked at 51 °C — and the area becomes a
    /// uniform slab that reads as a filled box rather than as data. Fading it down keeps the eye on
    /// the line, which is the part that carries the information.
    /// </summary>
    private static LinearGradientPaint AreaFill(SKColor color, byte topAlpha) =>
        new(color.WithAlpha(topAlpha), color.WithAlpha(0),
            new SKPoint(0.5f, 0f), new SKPoint(0.5f, 1f));

    /// <summary>The parts that never change once the series exists. Everything the user can influence is
    /// set by <see cref="ApplyStyle"/> instead, so there is exactly one place that decides how a line
    /// looks.</summary>
    private static LineSeries<double> MakeLine(IReadOnlyCollection<double> values) => new()
    {
        Values          = values,
        GeometryStroke  = null,
        AnimationsSpeed = TimeSpan.Zero,
        IsHoverable     = false,
    };

    private void ApplyStyle(LineSeries<double> line, SKColor color, byte fillAlpha)
    {
        DisposePaints(line);

        line.Stroke = new SolidColorPaint(color) { StrokeThickness = (float)LineThickness };

        line.Fill = Fill switch
        {
            ChartFill.None => null,
            ChartFill.Flat => new SolidColorPaint(color.WithAlpha(fillAlpha)),
            _ => AreaFill(color, fillAlpha),
        };

        line.LineSmoothness = Smoothness;

        // The marker paint is only made when a marker will actually be drawn — LiveCharts draws nothing at
        // size 0, so keeping one around would be an allocation per style change for an invisible dot.
        line.GeometrySize = PointSize;
        line.GeometryFill = PointSize > 0 ? new SolidColorPaint(color) : null;
    }

    private static void DisposePaints(LineSeries<double> line)
    {
        (line.Fill         as IDisposable)?.Dispose();
        (line.Stroke       as IDisposable)?.Dispose();
        (line.GeometryFill as IDisposable)?.Dispose();
    }

    private static SKColor ToSKColor(Brush? brush)
    {
        if (brush is SolidColorBrush scb)
        {
            var c = scb.Color;
            return new SKColor(c.R, c.G, c.B, c.A);
        }
        return new SKColor(0x60, 0xCD, 0xFF); // fallback: bright blue
    }
}
