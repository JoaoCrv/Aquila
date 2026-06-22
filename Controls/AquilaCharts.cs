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
    public static (IEnumerable<ISeries> Series, ObservableValue Point) SolidGauge(
        SKColor color, double columnWidth = 14, double labelSize = 24)
    {
        var point = new ObservableValue(0);

        var series = GaugeGenerator.BuildSolidGauge(
            new GaugeItem(point, series =>
            {
                series.MaxRadialColumnWidth = columnWidth;
                series.CornerRadius = 0;         // consistent flat caps (rounded flickers on short arcs)
                series.Fill = new SolidColorPaint(color);
                // Native centre label: integer only (default shows the raw double — the "550003..."
                // we saw was the unformatted value mid-animation).
                series.DataLabelsPaint = new SolidColorPaint(new SKColor(235, 235, 235));
                series.DataLabelsSize = labelSize;
                series.DataLabelsPosition = PolarLabelsPosition.ChartCenter;
                series.DataLabelsFormatter = p => p.Coordinate.PrimaryValue.ToString("F0");
            }),
            new GaugeItem(GaugeItem.Background, series =>
            {
                series.MaxRadialColumnWidth = columnWidth;
                series.CornerRadius = 0;
                series.Fill = new SolidColorPaint(new SKColor(255, 255, 255, 20));
                series.DataLabelsPaint = null;
            }));

        return (series, point);
    }
}
