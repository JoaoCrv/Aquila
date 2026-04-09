namespace Aquila.Models
{
    public sealed record TemperatureSnapshot
    {
        public string Label { get; init; } = string.Empty;
        public MetricValue Value { get; init; } = new();
    }

    public sealed record FanSnapshot
    {
        public string Name { get; init; } = string.Empty;
        public MetricValue Speed { get; init; } = new();
    }
}