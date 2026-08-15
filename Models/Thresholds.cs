using System.Globalization;

namespace Aquila.Models;

/// <summary>
/// When a reading stops being ordinary. Three steps, in the value's own units.
///
/// This is the policy half of the colour model: the colour profile says what "alert" looks like, this
/// says when a value is alerting. They are kept apart so restyling cannot silently change meaning, and
/// so a shared profile cannot impose someone else's limits.
///
/// Until now these numbers lived as strings at each call site ('50,70,85' here, '60,80,92' there). They
/// are gathered here because a second consumer arrived — the system pressure reading — and two consumers
/// reading different copies of the same numbers is how a card and the title bar end up disagreeing about
/// whether the machine is hot.
/// </summary>
public readonly record struct Thresholds(double Elevated, double Alert, double Critical)
{
    /// <summary>Anything measured 0–100: load, memory use, disk space.</summary>
    public static readonly Thresholds Percent = new(60, 80, 92);

    /// <summary>
    /// Degrees Celsius for silicon — CPU and GPU dies.
    ///
    /// Deliberately not alarmed by warmth. These parts boost in bursts and pass 60 °C opening a browser;
    /// a scale that called that "elevated" painted an idle machine orange and said nothing when it was
    /// genuinely working. Colour starts where headroom starts running out.
    /// </summary>
    public static readonly Thresholds Temperature = new(75, 85, 95);

    /// <summary>
    /// Degrees Celsius for drives and memory modules, which run far cooler than a die and are in trouble
    /// much sooner. 70 °C is an ordinary afternoon for a CPU and a reason to look at an NVMe.
    /// </summary>
    public static readonly Thresholds DriveTemperature = new(55, 65, 75);

    public string Role(double value) =>
        value >= Critical ? "Critical" :
        value >= Alert ? "Alert" :
        value >= Elevated ? "Elevated" :
        "Normal";

    /// <summary>
    /// Maps a reading onto 0–1, in steps rather than linearly, so that the SAME number means the same
    /// thing whatever the unit: ⅓ is "elevated", ⅔ is "alert", 1 is "critical".
    ///
    /// That is what makes readings comparable at all. Taking the larger of a load in percent and a
    /// temperature in degrees is meaningless on the raw values — 3200 rpm would win every time — and
    /// meaningful once both have been expressed as "how far into trouble is this".
    /// </summary>
    public double Level(double value)
    {
        if (double.IsNaN(value) || value <= 0) return 0;
        if (value >= Critical) return 1;

        const double third = 1d / 3d;
        const double twoThirds = 2d / 3d;

        if (value >= Alert) return Between(value, Alert, Critical, twoThirds, 1);
        if (value >= Elevated) return Between(value, Elevated, Alert, third, twoThirds);
        return Between(value, 0, Elevated, 0, third);
    }

    private static double Between(double value, double from, double to, double low, double high) =>
        to <= from ? high : low + (value - from) / (to - from) * (high - low);

    /// <summary>
    /// Reads either a preset name ("Temperature") or three numbers ("50,70,85"). Names are preferred in
    /// new code — a call site that spells the numbers out is a copy that can drift from this file.
    /// Returns null rather than throwing: this parses XAML written by hand.
    /// </summary>
    public static Thresholds? Parse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;

        if (string.Equals(text, nameof(Percent), StringComparison.OrdinalIgnoreCase)) return Percent;
        if (string.Equals(text, nameof(Temperature), StringComparison.OrdinalIgnoreCase)) return Temperature;
        if (string.Equals(text, nameof(DriveTemperature), StringComparison.OrdinalIgnoreCase)) return DriveTemperature;

        var parts = text.Split(',');
        if (parts.Length != 3) return null;

        var steps = new double[3];
        for (var i = 0; i < 3; i++)
            if (!double.TryParse(parts[i], NumberStyles.Any, CultureInfo.InvariantCulture, out steps[i]))
                return null;

        return new Thresholds(steps[0], steps[1], steps[2]);
    }
}
