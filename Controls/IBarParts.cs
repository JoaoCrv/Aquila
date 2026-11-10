using Aquila.Models;

namespace Aquila.Controls;

/// <summary>
/// What a bars widget draws besides its bars — each a thing to read, so the widget's and never the preset's, filled by
/// <c>DesktopWidgetService.Content</c> as a chart's <see cref="IChartParts"/> are (docs/widget-barras).
/// </summary>
public interface IBarParts
{
    /// <summary>Where the readings' names and numbers are written: nowhere, in the title, beside or above each bar, or in
    /// a legend under the bars — the chart's own choice, with the places bars have.</summary>
    ReadingPlace ReadingsAt { get; set; }

    /// <summary>Whether a bar is painted in the colour of each limit its reading has passed, a VU meter's.</summary>
    bool ColourByReading { get; set; }

    /// <summary>Whether a bar holds its reading's highest of the last <see cref="PointCount"/> readings.</summary>
    bool ShowPeak { get; set; }

    /// <summary>How many readings the peak is held over — the time window, as a chart's.</summary>
    int PointCount { get; set; }
}
