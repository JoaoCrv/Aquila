using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Aquila.Controls.Primitives;
using Aquila.Models;
using Aquila.Services;

namespace Aquila.Controls;

/// <summary>
/// A sparkline with the area under it: one series, or two on one scale (a download and its upload) — the trend in the
/// dashboard's cards and the storage page. The Chart widget is <see cref="ChartTile"/>; the two share the translation of
/// a chart's settings into the plot's terms (<see cref="PlotDress"/>).
///
/// Drawn by <see cref="LinePlot"/>. This piece only says, in the terms its callers and presets already use — a fill, a
/// scale, a window — what the plot should draw; it draws nothing itself and keeps nothing of its own. It was a
/// LiveCharts chart until docs/graficos-proprios, and its properties are the ones that chart had, so no card, preset or
/// editor changed when it stopped being one.
///
/// Every property is applied in place, the series and the window included: nothing is rebuilt, so a line never
/// restarts from empty because a colour or a slider changed.
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
    /// <summary>The colour a series without one is drawn in — the chart's default, kept from when it drew its own.
    /// </summary>
    private static readonly Brush Unpainted = Frozen(Color.FromRgb(0x60, 0xCD, 0xFF));

    // ── Dependency properties ──────────────────────────────────────

    public static readonly DependencyProperty ValuesProperty = Register<IReadOnlyCollection<double>?>(nameof(Values), null);
    public static readonly DependencyProperty SeriesColorProperty = Register<Brush?>(nameof(SeriesColor), null);
    public static readonly DependencyProperty MaxYProperty = Register(nameof(MaxY), double.NaN);
    public static readonly DependencyProperty PointCountProperty = Register(nameof(PointCount), 60);
    public static readonly DependencyProperty SecondValuesProperty = Register<IReadOnlyCollection<double>?>(nameof(SecondValues), null);
    public static readonly DependencyProperty LineThicknessProperty = Register(nameof(LineThickness), 1.5);
    public static readonly DependencyProperty FillProperty = Register(nameof(Fill), ChartFill.Gradient);
    public static readonly DependencyProperty FillOpacityProperty = Register(nameof(FillOpacity), 0.30);
    public static readonly DependencyProperty SmoothnessProperty = Register(nameof(Smoothness), 0.5);
    public static readonly DependencyProperty ScaleProperty = Register(nameof(Scale), ChartScale.FromZero);
    public static readonly DependencyProperty ScaleMinProperty = Register(nameof(ScaleMin), 0d);
    public static readonly DependencyProperty ScaleMaxProperty = Register(nameof(ScaleMax), 100d);
    public static readonly DependencyProperty PointSizeProperty = Register(nameof(PointSize), 0d);
    public static readonly DependencyProperty SecondColorProperty = Register<Brush?>(nameof(SecondColor), null);

    private static DependencyProperty Register<T>(string name, T fallback) =>
        DependencyProperty.Register(name, typeof(T), typeof(SparklineChart),
            new PropertyMetadata(fallback, (chart, _) => ((SparklineChart)chart).Translate()));

    // ── CLR wrappers ───────────────────────────────────────────────

    /// <summary>The series, oldest first — a sensor's <see cref="SensorNode.History"/>, read where it is.</summary>
    public IReadOnlyCollection<double>? Values
    {
        get => (IReadOnlyCollection<double>?)GetValue(ValuesProperty);
        set => SetValue(ValuesProperty, value);
    }

    /// <summary>The series' colour: its line, its area and its dots.</summary>
    public Brush? SeriesColor
    {
        get => (Brush?)GetValue(SeriesColorProperty);
        set => SetValue(SeriesColorProperty, value);
    }

    /// <summary>The top of a scale from nought. Unset (<c>NaN</c>), the top is fitted to the readings.</summary>
    public double MaxY
    {
        get => (double)GetValue(MaxYProperty);
        set => SetValue(MaxYProperty, value);
    }

    /// <summary>How many readings fit across — the last of them are drawn (default 60).</summary>
    public int PointCount
    {
        get => (int)GetValue(PointCountProperty);
        set => SetValue(PointCountProperty, value);
    }

    /// <summary>An optional second series, on the same scale.</summary>
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

    /// <summary>0 = straight segments between readings, 1 = the full curve, which never passes a reading.</summary>
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

    /// <summary>The second series' colour.</summary>
    public Brush? SecondColor
    {
        get => (Brush?)GetValue(SecondColorProperty);
        set => SetValue(SecondColorProperty, value);
    }

    public SparklineChart()
    {
        InitializeComponent();
        Translate();
    }

    /// <summary>
    /// Says it all to the plot again, in the plot's terms (<see cref="PlotDress"/>): the two lines, the window, the
    /// scale's ends and the look. Every property lands here, because each is a plain assignment: the plot draws again
    /// for what changed and never rebuilds anything — new lines of the same readings are the same lines repainted.
    /// </summary>
    private void Translate()
    {
        if (Plot is null) return;

        var lines = new List<PlotLine>(2);
        if (ListOf(Values) is { } first) lines.Add(new PlotLine(first, SeriesColor ?? Unpainted));
        if (ListOf(SecondValues) is { } second) lines.Add(new PlotLine(second, SecondColor ?? Unpainted));
        if (Plot.Lines is not { } drawn || !drawn.SequenceEqual(lines)) Plot.Lines = lines;

        Plot.Window = PointCount;
        (Plot.RangeMin, Plot.RangeMax) = PlotDress.Range(Scale, ScaleMin, ScaleMax, MaxY);

        // Equal looks are left alone, so a restyle that changed nothing here draws nothing.
        var look = PlotDress.Look(LineThickness, Fill, FillOpacity, Smoothness, PointSize);
        if (!Equals(Plot.Look, look)) Plot.Look = look;
    }

    /// <summary>The series as the plot reads it. Every caller hands over a sensor's history, which is a list already
    /// and is read in place; anything else is copied as it stands, and drawn as it was then.</summary>
    private static IReadOnlyList<double>? ListOf(IReadOnlyCollection<double>? series) =>
        series as IReadOnlyList<double> ?? series?.ToList();

    private static Brush Frozen(Color colour)
    {
        var brush = new SolidColorBrush(colour);
        brush.Freeze();
        return brush;
    }
}
