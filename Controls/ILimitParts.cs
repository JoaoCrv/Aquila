using System.Windows.Media;
using Aquila.Models;
using Aquila.Services;

namespace Aquila.Controls;

/// <summary>
/// A piece that can show where its reading's limits fall: lines across a chart, marks across a bar, at Elevated and
/// at Alert — where the reading turns from one colour to the next, as the Number's bar marks them.
///
/// The widget's to say, never the preset's, for the reason <see cref="INumberParts"/> gives: a preset must be safe to
/// try on, and these add to what is on the screen. Off when absent, so a widget made before them is drawn as it was.
/// </summary>
public interface ILimitParts
{
    bool ShowLimits { get; set; }
}

/// <summary>The colour the limits are drawn in: the title's at half its strength — they comment on the reading, as
/// the title does, and must never be mistaken for one.</summary>
public interface ILimitStyle
{
    Brush? LimitBrush { get; set; }
}

/// <summary>Where a reading's limits fall, for the pieces that draw them.</summary>
public static class LimitMarks
{
    /// <summary>How far a mark reaches past a bar either side, before the widget's scale — the Number's marks' reach.
    /// </summary>
    public const double Reach = 3;

    /// <summary>The colour of limits nobody has dressed — the type picker: a faint white.</summary>
    public static Brush Unpainted { get; } = Frozen(Color.FromArgb(0x50, 0xFF, 0xFF, 0xFF));

    /// <summary>The limits a reading turns elevated and alert at, in its own units — for a chart, which places them on
    /// its own scale. Null for a reading with no limits.</summary>
    public static IReadOnlyList<double>? Of(SensorNode? sensor) =>
        sensor is not null && VitalMonitor.Current?.LimitsOf(sensor) is { } limits ? [limits.Elevated, limits.Alert] : null;

    /// <summary>The same limits as shares of a bar's scale, <paramref name="min"/> to <paramref name="max"/> — those
    /// that fall on it. Null when none does.</summary>
    public static IReadOnlyList<double>? On(SensorNode? sensor, double min, double max)
    {
        if (Of(sensor) is not { } values || !(max > min)) return null;

        var shares = values.Select(value => (value - min) / (max - min)).Where(share => share is > 0 and < 1).ToArray();
        return shares.Length > 0 ? shares : null;
    }

    /// <summary>Whether two lists of marks are the same — so a bar is not handed a new list, and drawn again, for marks
    /// that have not moved.</summary>
    public static bool Same(IReadOnlyList<double>? a, IReadOnlyList<double>? b) =>
        a is null ? b is null : b is not null && a.SequenceEqual(b);

    private static Brush Frozen(Color colour)
    {
        var brush = new SolidColorBrush(colour);
        brush.Freeze();
        return brush;
    }
}
