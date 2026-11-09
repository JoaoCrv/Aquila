using System.Windows.Media;
using Aquila.Models;

namespace Aquila.Controls;

/// <summary>
/// What a chart draws besides its lines: a dot on each line's newest reading, which pulses as each one arrives; the
/// lines coloured by their reading along the way; the first reading's peak; the scale's ends; and where the readings'
/// numbers are written.
///
/// The widget's to say, never the preset's, for the reason <see cref="INumberParts"/> gives: a preset must be safe to
/// try on, and these change what is on the screen. Off when absent, so a chart made before them is drawn as it was.
/// </summary>
public interface IChartParts
{
    bool ShowEnd { get; set; }

    /// <summary>Each line in the colour of its reading's state — its ramp's colour for each stretch between its
    /// limits — instead of the colour of its newest reading throughout. Only for a reading with limits.</summary>
    bool ColourByReading { get; set; }

    /// <summary>The first reading's highest point in the window, ringed and written.</summary>
    bool ShowPeak { get; set; }

    /// <summary>The top and the bottom of the scale, written at the left edge.</summary>
    bool ShowScaleLabels { get; set; }

    /// <summary>Where the readings' numbers are written: nowhere, in the title, at the line ends or in a legend.</summary>
    ReadingPlace ReadingsAt { get; set; }
}

/// <summary>A piece that needs the whole ramp of each reading, not only the colour of the moment — a chart coloured
/// by its readings. Handed it wherever a reading is painted, so a change of preset reaches it too.</summary>
public interface IReadingRamp
{
    void UseRamp(int index, Ramp ramp);
}

/// <summary>What a chart writes with, from the preset: the halo kept round what it writes over its lines — the
/// panel's own colour — and the title's face, which a legend names its readings in.</summary>
public interface IPlotTextStyle
{
    Brush? HaloBrush { get; set; }

    string? TitleFont { get; set; }

    TextWeight TitleWeight { get; set; }
}
