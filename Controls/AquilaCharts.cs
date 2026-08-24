using System.Collections.Generic;
using LiveChartsCore;
using LiveChartsCore.Defaults;
using LiveChartsCore.Measure;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Extensions;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;

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
    /// the series each tick, or it animates from zero).
    /// </summary>
    public static (IEnumerable<ISeries> Series, ObservableValue Point, Action<bool> SetArcVisible) SolidGauge(
        SKColor color, double columnWidth = 14, double labelSize = 24, bool showLabel = true,
        double cornerRadius = 0)
    {
        // A cap cannot be rounder than the arc is thick. Past half the width the geometry starts eating
        // into the arc, and LiveCharts' rounding is already unreliable enough at the edges without being
        // handed values it cannot honour — the editor's slider stops here too, so this is a backstop for
        // layouts written by hand rather than something the user can walk into.
        var caps = (float)Math.Clamp(cornerRadius, 0, columnWidth / 2);

        // Shows and hides the value arc without rebuilding the series — rebuilding restarts the animation
        // from zero, so anything that has to change with the reading has to be reachable like this. The
        // closure also spares us naming PieSeries' three generic arguments.
        //
        // Indirect through a variable rather than returning this directly: BuildSolidGauge hands back an
        // IEnumerable and gives no promise about WHEN it runs the item builders, and a returned tuple
        // captures the value a local held at that moment.
        Action<bool>? apply = null;

        var point = new ObservableValue(0);

        var series = GaugeGenerator.BuildSolidGauge(
            new GaugeItem(point, series =>
            {
                series.MaxRadialColumnWidth = columnWidth;
                series.CornerRadius = caps;

                // Clearing the Fill hides the arc while leaving the number alone — the label has a paint of
                // its own, so the reading is still there to read when the arc is too short to draw.
                var fill = new SolidColorPaint(color);
                series.Fill = fill;
                apply = visible => series.Fill = visible ? fill : null;
                // Native centre label: integer only (default shows the raw double — the "550003..."
                // we saw was the unformatted value mid-animation).
                // Null hides the number entirely — an arc on its own, with the widget's own title above it.
                series.DataLabelsPaint = showLabel ? new SolidColorPaint(new SKColor(235, 235, 235)) : null;
                series.DataLabelsSize = labelSize;
                series.DataLabelsPosition = PolarLabelsPosition.ChartCenter;
                series.DataLabelsFormatter = p => p.Coordinate.PrimaryValue.ToString("F0");
            }),
            new GaugeItem(GaugeItem.Background, series =>
            {
                series.MaxRadialColumnWidth = columnWidth;
                series.CornerRadius = caps;
                series.Fill = new SolidColorPaint(new SKColor(255, 255, 255, 20));
                series.DataLabelsPaint = null;
            }));

        return (series, point, visible => apply?.Invoke(visible));
    }
}
