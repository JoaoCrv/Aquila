namespace Aquila.Models;

/// <summary>What kind of part a reading came from. The outer level of <see cref="HardwareNode"/>.</summary>
public enum HardwareKind
{
    Cpu,
    Gpu,
    Memory,
    Motherboard,
    Network,
    Storage,

    /// <summary>Readings that belong to the machine rather than to a part — total power.</summary>
    System,
}

/// <summary>What a reading measures. The inner level of each hardware node.</summary>
public enum MetricKind
{
    Load,
    Temperature,
    Power,
    Clock,
    Data,
    Throughput,
    Voltage,

    /// <summary>A fan's speed, in RPM. No shared scale — 1200 rpm is loud on one fan and idle on another.</summary>
    Fan,

    /// <summary>How hard a fan is being driven, 0-100. A real percentage from the board's PWM control, and
    /// a different measurement from <see cref="Fan"/> — one is how fast it turns, the other how much of its
    /// capacity is being asked for.</summary>
    Duty,

    Level,
}

/// <summary>
/// What a reading IS: the part it came from and what it measures.
///
/// The two together are what decides whether a number is ordinary. Neither is enough on its own — 62 °C is
/// unremarkable on a die and worth looking at on an NVMe, and "83" means nothing at all without knowing it
/// is a percentage rather than a temperature.
///
/// This taxonomy already existed, as the shape of <see cref="HardwareNode"/>: the outer property is the
/// part, the inner group is the metric. What it never did was survive to the leaf — a
/// <see cref="SensorNode"/> knows its value, unit and name, and nothing about where it came from. This
/// carries that knowledge out of the tree and into the places that need to judge it.
/// </summary>
/// <remarks>
/// Written as "Cpu.Temperature" wherever it has to be text: settings keys, widget definitions, and the
/// ConverterParameter the dashboard's XAML passes. A name rather than the numbers, so a limit the user
/// changes reaches every one of them.
/// </remarks>
public readonly record struct MetricKey(HardwareKind Hardware, MetricKind Metric)
{
    public override string ToString() => $"{Hardware}.{Metric}";

    /// <summary>Reads "Cpu.Temperature". Returns null rather than throwing: this parses settings written by
    /// hand and XAML written by hand, and a typo should cost one binding, not the window.</summary>
    public static MetricKey? Parse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;

        var dot = text.IndexOf('.');
        if (dot <= 0 || dot == text.Length - 1) return null;

        return Enum.TryParse<HardwareKind>(text[..dot], ignoreCase: true, out var hardware)
            && Enum.TryParse<MetricKind>(text[(dot + 1)..], ignoreCase: true, out var metric)
                ? new MetricKey(hardware, metric)
                : null;
    }
}
