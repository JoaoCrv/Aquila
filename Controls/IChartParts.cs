namespace Aquila.Controls;

/// <summary>
/// What a chart draws besides its lines: a dot on the newest reading, which pulses as each one arrives.
///
/// The widget's to say, never the preset's, for the reason <see cref="INumberParts"/> gives: a preset must be safe to
/// try on, and these add to what is on the screen. Off when absent, so a chart made before them is drawn as it was.
/// </summary>
public interface IChartParts
{
    bool ShowEnd { get; set; }
}
