using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

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
public partial class SparklineChart : UserControl
{
    private LineSeries<double>? _primary;
    private LineSeries<double>? _secondary;

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

        var color = ToSKColor(SeriesColor);

        if (_primary == null)
        {
            _primary = MakeLine(values, color, PrimaryFillAlpha);

            var series = new List<ISeries> { _primary };

            if (SecondValues is { } sv)
            {
                _secondary = MakeLine(sv, ToSKColor(SecondColor), SecondaryFillAlpha);
                series.Add(_secondary);
            }

            Chart.Series = series;
            Chart.XAxes  = [new Axis { IsVisible = false, MinLimit = 0, MaxLimit = PointCount - 1 }];
            Chart.YAxes  = [new Axis { IsVisible = false, MinLimit = 0,
                                       MaxLimit = double.IsNaN(MaxY) ? null : MaxY }];
        }
        else
        {
            // Hot-path: only recolour (Values binding stays the same object).
            _primary.Values = values;
            ApplyColor(_primary, color, PrimaryFillAlpha);

            if (_secondary != null && SecondValues is { } sv)
            {
                _secondary.Values = sv;
                ApplyColor(_secondary, ToSKColor(SecondColor), SecondaryFillAlpha);
            }
        }
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

    private static LineSeries<double> MakeLine(
        IReadOnlyCollection<double> values, SKColor color, byte fillAlpha) => new()
    {
        Values          = values,
        Fill            = AreaFill(color, fillAlpha),
        Stroke          = new SolidColorPaint(color) { StrokeThickness = 1.5f },
        GeometryFill    = null,
        GeometryStroke  = null,
        GeometrySize    = 0,
        LineSmoothness  = 0.5,
        AnimationsSpeed = TimeSpan.Zero,
        IsHoverable     = false
    };

    private static void ApplyColor(LineSeries<double> line, SKColor color, byte fillAlpha)
    {
        DisposePaints(line);
        line.Fill   = AreaFill(color, fillAlpha);
        line.Stroke = new SolidColorPaint(color) { StrokeThickness = 1.5f };
    }

    private static void DisposePaints(LineSeries<double> line)
    {
        (line.Fill   as IDisposable)?.Dispose();
        (line.Stroke as IDisposable)?.Dispose();
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
