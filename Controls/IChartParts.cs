using Aquila.Models;

namespace Aquila.Controls;

/// <summary>
/// What a chart draws besides its lines: a dot on the newest reading, which pulses as each one arrives; and the line
/// coloured by its reading along the way rather than in one colour throughout.
///
/// The widget's to say, never the preset's, for the reason <see cref="INumberParts"/> gives: a preset must be safe to
/// try on, and these change what is on the screen. Off when absent, so a chart made before them is drawn as it was.
/// </summary>
public interface IChartParts
{
    bool ShowEnd { get; set; }

    /// <summary>The line in the colour of each reading's state — the ramp's colour for each stretch between the
    /// limits — instead of the colour of the newest one throughout. Only for a reading with limits.</summary>
    bool ColourByReading { get; set; }
}

/// <summary>A piece that needs the whole ramp of its first reading, not only the colour of the moment — a chart
/// coloured by its reading. Handed it wherever the reading is painted, so a change of preset reaches it too.</summary>
public interface IReadingRamp
{
    void UseRamp(Ramp ramp);
}
