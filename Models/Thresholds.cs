using System.Globalization;

namespace Aquila.Models;

/// <summary>
/// When a reading stops being ordinary. Three steps, in the value's own units.
///
/// This is the policy half of the colour model: the preset says what "alert" looks like, this says when
/// a value is alerting. They are kept apart so restyling cannot silently change meaning, and so a shared
/// preset cannot impose someone else's limits.
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
    /// The built-in limits for a reading, or NULL when its kind has no shared scale to be judged on.
    ///
    /// Null is the honest answer for watts, clocks, bytes and volts: 85 W means one thing in a laptop and
    /// another in a workstation, and there is no number we could put here that would be true of both. A
    /// caller that gets null must not colour by state — it has nothing to colour against.
    ///
    /// This table IS the definition of "judgeable": the settings form draws a row for every entry, and
    /// nothing else keeps a second list that could fall out of step with it.
    /// </summary>
    public static Thresholds? Preset(MetricKey key) => (key.Hardware, key.Metric) switch
    {
        // Silicon that is designed to run hot and boosts in bursts.
        (HardwareKind.Cpu, MetricKind.Temperature) => Temperature,
        (HardwareKind.Gpu, MetricKind.Temperature) => Temperature,
        (HardwareKind.Motherboard, MetricKind.Temperature) => Temperature,

        // Drives throttle far sooner than a die does.
        (HardwareKind.Storage, MetricKind.Temperature) => DriveTemperature,

        // A DIMM under load sits where an NVMe would already be in trouble.
        (HardwareKind.Memory, MetricKind.Temperature) => MemoryTemperature,

        // Anything on a 0-100 scale. Listed rather than matched with a wildcard on Load: a wildcard also
        // claimed Network.Load and System.Load, which no sensor produces, and the settings form drew two
        // rows for readings that cannot exist. Fan duty is here because it IS a percentage; fan SPEED is
        // not, and has no entry — 1200 rpm judged against 60/80/92 would be critical on every fan.
        (HardwareKind.Cpu, MetricKind.Load) => Percent,
        (HardwareKind.Gpu, MetricKind.Load) => Percent,
        (HardwareKind.Memory, MetricKind.Load) => Percent,
        (HardwareKind.Storage, MetricKind.Load) => Percent,
        (HardwareKind.Motherboard, MetricKind.Duty) => Percent,
        (HardwareKind.Storage, MetricKind.Level) => Percent,

        _ => null,
    };

    /// <summary>Every reading kind that can be judged, and therefore every row the settings form draws.
    /// Derived from <see cref="Preset(MetricKey)"/> so the two can never disagree.</summary>
    public static IReadOnlyList<MetricKey> Configurable { get; } =
    [
        .. from hardware in Enum.GetValues<HardwareKind>()
           from metric in Enum.GetValues<MetricKind>()
           let key = new MetricKey(hardware, metric)
           where Preset(key) is not null
           select key
    ];

    /// <summary>What a reading kind is called in front of a person, and what it covers.</summary>
    public static (string Name, string Detail) Describe(MetricKey key) => (key.Hardware, key.Metric) switch
    {
        (HardwareKind.Cpu, MetricKind.Temperature) => ("CPU temperature", "The die, which is designed to run hot and boosts in bursts"),
        (HardwareKind.Gpu, MetricKind.Temperature) => ("GPU temperature", "The graphics die, warmer still under load"),
        (HardwareKind.Motherboard, MetricKind.Temperature) => ("Board temperature", "VRM, chipset and whatever else the board reports"),
        (HardwareKind.Storage, MetricKind.Temperature) => ("Drive temperature", "SSDs and hard drives, which throttle far sooner than a die"),
        (HardwareKind.Memory, MetricKind.Temperature) => ("Memory temperature", "DIMMs, warmer than a drive and cooler than a die"),

        (HardwareKind.Cpu, MetricKind.Load) => ("CPU load", "How much of the processor is in use"),
        (HardwareKind.Gpu, MetricKind.Load) => ("GPU load", "How much of the graphics card is in use"),
        (HardwareKind.Memory, MetricKind.Load) => ("Memory in use", "How full the machine's memory is"),
        (HardwareKind.Storage, MetricKind.Load) => ("Disk space used", "How full a drive is"),
        (HardwareKind.Storage, MetricKind.Level) => ("Drive life left", "Wear reported by the drive itself"),
        (HardwareKind.Motherboard, MetricKind.Duty) => ("Fan duty", "How much of a fan's capacity is being asked for — one at its limit cannot cool any harder"),

        // Unreachable through the settings form, which draws a row per entry in Preset — every one of which
        // is named above. Here so adding a preset without a description degrades rather than throws.
        _ => ($"{key.Hardware} {key.Metric}".ToLowerInvariant(), string.Empty),
    };

    /// <summary>
    /// Reads three numbers, "50,70,85", in the reading's own units. Only for values stored or written by
    /// hand — a call site that spells the numbers out holds a copy that cannot follow a limit the user
    /// changes, which is why every binding in the app names a <see cref="MetricKey"/> instead.
    /// Returns null rather than throwing.
    /// </summary>
    public static Thresholds? Parse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;

        var parts = text.Split(',');
        if (parts.Length != 3) return null;

        var steps = new double[3];
        for (var i = 0; i < 3; i++)
            if (!double.TryParse(parts[i], NumberStyles.Any, CultureInfo.InvariantCulture, out steps[i]))
                return null;

        return new Thresholds(steps[0], steps[1], steps[2]);
    }
}
