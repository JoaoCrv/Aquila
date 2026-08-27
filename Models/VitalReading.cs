namespace Aquila.Models;

/// <summary>
/// One watched reading, already judged: the number, how to write it, and how far into trouble it is.
///
/// This exists so that "read a vital" is a thing the app does once rather than a loop each surface writes
/// for itself. The title bar's strip had it inline — pick the node, decide absence, format the text, clamp
/// the bar, normalise the level — which is four decisions a second surface would have had to copy, and get
/// subtly wrong.
/// </summary>
/// <param name="HasValue">False when the machine has no such part. Absence is not a reading of zero.</param>
/// <param name="Value">The raw number, for anything that wants to do its own arithmetic.</param>
/// <param name="Unit">The sensor's own unit, so callers never have to guess it.</param>
/// <param name="Text">The number and unit written out, e.g. "62°C".</param>
/// <param name="Level">0–1 on the shared scale of <see cref="Thresholds.Level"/> — comparable across units.</param>
public readonly record struct VitalReading(
    bool HasValue,
    double Value,
    string Unit,
    string Text,
    double Level)
{
    /// <summary>A part this machine does not have. Level 0 so anything that reads it without checking
    /// HasValue is quiet rather than alarming — but HasValue is the honest answer.</summary>
    public static readonly VitalReading Absent = new(false, 0, string.Empty, string.Empty, 0);

    /// <summary>The level as a percentage, which is the scale the colour roles and the bars are expressed
    /// on (see <see cref="Thresholds.Pressure"/>). Here rather than at each binding, so no surface has to
    /// remember which of the two scales it is holding.</summary>
    public double Percent => Level * 100;
}
