using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Aquila.Controls.Primitives;
using Aquila.Models;

namespace Aquila.Controls;

/// <summary>
/// A sparkline with the area under it: one series, or two on one scale (a download and its upload) — the trend in the
/// dashboard's cards and the storage page. The Chart widget is <see cref="ChartTile"/>; this piece is the cards' alone,
/// and takes its look from the same translation (<see cref="PlotDress"/>).
///
/// Drawn by <see cref="LinePlot"/>. This piece only says, in the terms the cards already use — the readings, their
/// colours, the top of the scale — what the plot should draw; it draws nothing itself and keeps nothing of its own. It
/// was a LiveCharts chart until docs/graficos-proprios. Its style properties went when the widgets stopped wearing it
/// (docs/widget-grafico): no card ever set one, so the look is the one the cards always had.
///
/// Every property is applied in place, the series included: nothing is rebuilt, so a line never restarts from empty
/// because a colour changed.
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
    /// <summary>The colour a series without one is drawn in — the chart's default, kept from when it drew its own.
    /// </summary>
    private static readonly Brush Unpainted = Frozen(Color.FromRgb(0x60, 0xCD, 0xFF));

    /// <summary>The cards' look: a 1.5 px line over an area fading from 30%, half smoothed, no dots.</summary>
    private static readonly LineLook Look = PlotDress.Look(1.5, ChartFill.Gradient, 0.30, 0.5, 0);

    /// <summary>How many readings fit across — the last of them are drawn: a sensor's whole default history.</summary>
    private const int Window = 60;

    // ── Dependency properties ──────────────────────────────────────

    public static readonly DependencyProperty ValuesProperty = Register<IReadOnlyCollection<double>?>(nameof(Values), null);
    public static readonly DependencyProperty SeriesColorProperty = Register<Brush?>(nameof(SeriesColor), null);
    public static readonly DependencyProperty MaxYProperty = Register(nameof(MaxY), double.NaN);
    public static readonly DependencyProperty SecondValuesProperty = Register<IReadOnlyCollection<double>?>(nameof(SecondValues), null);
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

    /// <summary>The series' colour: its line and its area.</summary>
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

    /// <summary>An optional second series, on the same scale.</summary>
    public IReadOnlyCollection<double>? SecondValues
    {
        get => (IReadOnlyCollection<double>?)GetValue(SecondValuesProperty);
        set => SetValue(SecondValuesProperty, value);
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
        (Plot.Window, Plot.Look) = (Window, Look);
        Translate();
    }

    /// <summary>
    /// Says the lines and the scale to the plot again. Every property lands here, because each is a plain assignment:
    /// the plot draws again for what changed and never rebuilds anything — new lines of the same readings are the same
    /// lines repainted.
    /// </summary>
    private void Translate()
    {
        if (Plot is null) return;

        var lines = new List<PlotLine>(2);
        if (ListOf(Values) is { } first) lines.Add(new PlotLine(first, SeriesColor ?? Unpainted));
        if (ListOf(SecondValues) is { } second) lines.Add(new PlotLine(second, SecondColor ?? Unpainted));
        if (Plot.Lines is not { } drawn || !drawn.SequenceEqual(lines)) Plot.Lines = lines;

        (Plot.RangeMin, Plot.RangeMax) = (0d, MaxY);
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
