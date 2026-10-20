using System.Collections.Generic;
using LiveChartsCore;
using LiveChartsCore.Defaults;
using LiveChartsCore.Measure;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Extensions;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;
using Aquila.Models;

namespace Aquila.Controls;

/// <summary>
/// Aquila-specific factory for the LiveCharts visuals the app uses (gauge, sparkline). Centralises
/// chart configuration (colours, animation) so controls and widgets just ask for what they need,
/// already styled. Not a generic "any LiveCharts chart" wrapper — grows by adding methods as needed.
/// </summary>
public static class AquilaCharts
{
    /// <summary>
    /// Builds a solid radial gauge for a PieChart configured with <c>InitialRotation = -225</c> and
    /// <c>MaxAngle = 270</c>. Returns the series plus the <see cref="ObservableValue"/> backing the
    /// value arc — update <c>point.Value</c> to animate smoothly from the current value (don't rebuild
    /// the series each tick, or it animates from zero). SetCorner rounds the arc's ends by as much as its length
    /// allows — see <see cref="CornerFor"/> — without rebuilding anything.
    /// </summary>
    public static (IEnumerable<ISeries> Series, ObservableValue Point, Action<double> SetCorner) SolidGauge(
        SKColor color, double columnWidth = 14, double labelSize = 24, bool showLabel = true,
        double cornerRadius = 0, SKColor? track = null,
        string? font = null, Aquila.Models.TextWeight weight = Aquila.Models.TextWeight.Regular,
        SKColor? label = null, string? unit = null, string decimals = "F0")
    {
        // A cap cannot be rounder than the arc is thick. Past half the width the geometry starts eating
        // into the arc, and LiveCharts' rounding is already unreliable enough at the edges without being
        // handed values it cannot honour — the editor's slider stops here too, so this is a backstop for
        // layouts written by hand rather than something the user can walk into.
        var caps = (float)Math.Clamp(cornerRadius, 0, columnWidth / 2);

        // Rounds the value arc's ends without rebuilding the series — rebuilding restarts the animation from
        // zero, so anything that has to change with the reading has to be reachable like this. The closure also
        // spares us naming PieSeries' three generic arguments. Never past the caps above, whatever it is asked.
        //
        // Indirect through a variable rather than returning this directly: BuildSolidGauge hands back an
        // IEnumerable and gives no promise about WHEN it runs the item builders, and a returned tuple
        // captures the value a local held at that moment.
        Action<double>? round = null;

        var point = new ObservableValue(0);

        var series = GaugeGenerator.BuildSolidGauge(
            new GaugeItem(point, series =>
            {
                series.MaxRadialColumnWidth = columnWidth;
                series.CornerRadius = caps;
                round = corner => series.CornerRadius = Math.Clamp(corner, 0, caps);

                series.Fill = new SolidColorPaint(color);
                // Native centre label: integer only (default shows the raw double — the "550003..."
                // we saw was the unformatted value mid-animation).
                // Null hides the number entirely — an arc on its own, with the widget's own title above it.
                // The label is PAINTED, not laid out, so it inherits nothing from the visual tree above
                // it — which is why the preset's typeface used to reach every piece except this one.
                // Through SKTypeface rather than the paint's FontFamily and SKFontStyle, which the library
                // has deprecated in favour of exactly this call. A null family asks Skia for its default,
                // which is what "the app's own face" means once we are outside WPF.
                series.DataLabelsPaint = showLabel
                    ? new SolidColorPaint(label ?? new SKColor(235, 235, 235))
                    {
                        SKTypeface = SKTypeface.FromFamilyName(
                            string.IsNullOrWhiteSpace(font) ? null : font,
                            weight.ToSkiaWeight(), SKFontStyleWidth.Normal, SKFontStyleSlant.Upright),
                    }
                    : null;
                series.DataLabelsSize = labelSize;
                series.DataLabelsPosition = PolarLabelsPosition.ChartCenter;
                // Was "F0" flat, which drew a 1.24 V rail as "1". The decimals come from the sensor's
                // unit now, by the same rule the stat and the meter already used.
                series.DataLabelsFormatter = string.IsNullOrEmpty(unit)
                    ? p => p.Coordinate.PrimaryValue.ToString(decimals)
                    : p => p.Coordinate.PrimaryValue.ToString(decimals) + unit;
            }),
            new GaugeItem(GaugeItem.Background, series =>
            {
                series.MaxRadialColumnWidth = columnWidth;
                series.CornerRadius = caps;
                series.Fill = new SolidColorPaint(track ?? new SKColor(255, 255, 255, 20));
                series.DataLabelsPaint = null;
            }));

        return (series, point, corner => round?.Invoke(corner));
    }

    /// <summary>
    /// How round an arc's ends can be at this length: the corner asked for, or less when the arc is too short to
    /// hold it. Asked by the dial (the clock's rings are drawn by Primitives.RingArc now, whose round ends sit outside
    /// the arc and need no limit).
    ///
    /// Read in LiveCharts 2.0.2 (DoughnutGeometry.RoundedDraw): each rounded end takes an angle of
    /// atan(r / (inner + r)) out of the arc's INNER edge, and when the two ends together take more than the arc
    /// has, that edge is drawn backwards — the glitch between a round end and a stub that short arcs showed. The
    /// library's own guard measures the outer edge, which is longer, so it lets those lengths through. Solved for
    /// r, the most an arc of θ can hold is inner · tan(θ/2) / (1 − tan(θ/2)); from a quarter turn up, any corner
    /// fits. A tenth is kept in hand, so the inner edge is never asked to be exactly nothing.
    ///
    /// The ends shrink with the arc instead of the arc being hidden. Hiding it below 4% of the scale was the rule
    /// before, and a clock with round ends showed what that costs: its seconds vanished every minute, from :00 to
    /// :03. Now any reading above nothing is drawn, square at first and round as soon as it is long enough to be.
    /// A reading of nothing — a fraction of 0 — gets no corner and is drawn as nothing.
    ///
    /// Near a full turn the two ends meet at the start and overlap; rendered, that is a clean notch and needs no
    /// limit of its own.
    /// </summary>
    /// <param name="fraction">How much of the sweep the arc covers, 0 to 1.</param>
    /// <param name="corner">The corner asked for.</param>
    /// <param name="sweepDegrees">The whole sweep the fraction is of — 360 for a ring, 270 for a dial.</param>
    /// <param name="innerRadius">The radius of the arc's inner edge. Unknown — zero — rounds nothing, since the
    /// limit cannot be worked out.</param>
    public static double CornerFor(double fraction, double corner, double sweepDegrees, double innerRadius)
    {
        // Written as "not above zero" so a NaN from anywhere upstream lands here too, rather than in Math.Min,
        // which would hand it on.
        if (!(fraction > 0) || !(corner > 0) || !(sweepDegrees > 0) || !(innerRadius > 0)) return 0;

        var half = Math.Min(fraction, 1) * sweepDegrees * Math.PI / 360;
        if (half >= Math.PI / 4) return corner;

        var tan = Math.Tan(half);
        return Math.Min(corner, CornerRoom * innerRadius * tan / (1 - tan));
    }

    private const double CornerRoom = 0.9;

    /// <summary>The radius LiveCharts drew a gauge point's inner edge at — or null before it has drawn it, which is
    /// the only way to know it for a dial: the chart works it out from its own size, less room it reserves.</summary>
    public static double? InnerRadiusOf(ObservableValue point) =>
        point.MetaData?.ChartPoints.Values
            .Select(drawn => drawn.Context.Visual)
            .OfType<LiveChartsCore.Drawing.BaseDoughnutGeometry>()
            .FirstOrDefault()?.InnerRadius;
}
