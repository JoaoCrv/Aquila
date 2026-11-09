using Aquila.Models;

namespace Aquila.Controls;

/// <summary>
/// What a bars widget draws besides its bars — each a thing to read, so the widget's and never the preset's, filled by
/// <c>DesktopWidgetService.Content</c> as a chart's <see cref="IChartParts"/> are (docs/widget-barras).
/// </summary>
public interface IBarParts
{
    /// <summary>Where each bar's name and number are written: beside the bar or above it.</summary>
    BarLayout BarLayout { get; set; }

    /// <summary>Whether each bar's name is written — only with two bars or more: one bar's name is the title.</summary>
    bool ShowNames { get; set; }

    /// <summary>Whether a bar is painted in the colour of each limit its reading has passed, a VU meter's.</summary>
    bool ColourByReading { get; set; }

    /// <summary>Whether a bar holds its reading's highest of the last <see cref="PointCount"/> readings.</summary>
    bool ShowPeak { get; set; }

    /// <summary>How many readings the peak is held over — the time window, as a chart's.</summary>
    int PointCount { get; set; }
}
