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
    /// Degrees Celsius for drives, which run far cooler than a die and are in trouble much sooner. 70 °C is
    /// an ordinary afternoon for a CPU and a reason to look at an NVMe, most of which throttle by 80.
    /// </summary>
    public static readonly Thresholds DriveTemperature = new(55, 65, 75);

    /// <summary>
    /// Degrees Celsius for memory modules. Split from drives because they are not the same part with the
    /// same tolerance: a DIMM under load sits where an NVMe would already be throttling, and DDR5 in
    /// particular runs warm by design. Judged against how these parts behave, not against a spec sheet —
    /// which is exactly why they are editable.
    /// </summary>
    public static readonly Thresholds MemoryTemperature = new(60, 70, 80);

    /// <summary>
    /// For a value that has already been through <see cref="Level"/> and scaled to 0–100. The steps are
    /// the thirds that mapping produces, so colouring a normalised reading needs no second opinion about
    /// what counts as alert — the sensor's own scale already decided that.
    /// </summary>
    public static readonly Thresholds Pressure = new(100d / 3, 200d / 3, 100);

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
    /// The families a reading can be judged on, by the names call sites use. The set the settings form
    /// draws a row for, and the set a ConverterParameter may name.
    /// </summary>
    public static IReadOnlyList<string> Families { get; } =
        [nameof(Percent), nameof(Temperature), nameof(DriveTemperature), nameof(MemoryTemperature), nameof(Pressure)];

    /// <summary>
    /// The families worth putting in front of a person, and the reason the list is not simply
    /// <see cref="Families"/>.
    ///
    /// <see cref="Pressure"/> is deliberately absent. It is not a judgement about a reading — it colours a
    /// number that has ALREADY been through <see cref="Level"/>, and its steps ARE the thirds that mapping
    /// produces. Letting it be edited would let the ribbon disagree with the very mapping that produced the
    /// number it is drawing.
    /// </summary>
    public static IReadOnlyList<string> Configurable { get; } =
        [nameof(Percent), nameof(Temperature), nameof(DriveTemperature), nameof(MemoryTemperature)];

    /// <summary>What each family is called in front of a person, and what it covers.</summary>
    public static (string Name, string Detail) Describe(string family) => family switch
    {
        nameof(Temperature) => ("Processor temperature", "CPU and GPU dies, which run hot by design"),
        nameof(DriveTemperature) => ("Drive temperature", "SSDs and hard drives, which throttle far sooner than a die"),
        nameof(MemoryTemperature) => ("Memory temperature", "DIMMs, which sit warmer than a drive but cooler than a die"),
        _ => ("Load and usage", "Anything measured 0–100: CPU, GPU, memory, disk space"),
    };

    /// <summary>The built-in limits for a family, ignoring anything the user has changed. What a Reset
    /// goes back to, and the fallback when a name is not one of ours.</summary>
    public static Thresholds Preset(string? family) =>
        string.Equals(family, nameof(Temperature), StringComparison.OrdinalIgnoreCase) ? Temperature :
        string.Equals(family, nameof(DriveTemperature), StringComparison.OrdinalIgnoreCase) ? DriveTemperature :
        string.Equals(family, nameof(MemoryTemperature), StringComparison.OrdinalIgnoreCase) ? MemoryTemperature :
        string.Equals(family, nameof(Pressure), StringComparison.OrdinalIgnoreCase) ? Pressure :
        Percent;

    /// <summary>
    /// Reads either a preset name ("Temperature") or three numbers ("50,70,85"). Names are preferred in
    /// new code — a call site that spells the numbers out is a copy that can drift from this file, and
    /// only a name can follow a limit the user has since changed.
    /// Returns null rather than throwing: this parses XAML written by hand.
    /// </summary>
    public static Thresholds? Parse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;

        if (string.Equals(text, nameof(Percent), StringComparison.OrdinalIgnoreCase)) return Percent;
        if (string.Equals(text, nameof(Temperature), StringComparison.OrdinalIgnoreCase)) return Temperature;
        if (string.Equals(text, nameof(DriveTemperature), StringComparison.OrdinalIgnoreCase)) return DriveTemperature;
        if (string.Equals(text, nameof(MemoryTemperature), StringComparison.OrdinalIgnoreCase)) return MemoryTemperature;
        if (string.Equals(text, nameof(Pressure), StringComparison.OrdinalIgnoreCase)) return Pressure;

        var parts = text.Split(',');
        if (parts.Length != 3) return null;

        var steps = new double[3];
        for (var i = 0; i < 3; i++)
            if (!double.TryParse(parts[i], NumberStyles.Any, CultureInfo.InvariantCulture, out steps[i]))
                return null;

        return new Thresholds(steps[0], steps[1], steps[2]);
    }
}
