namespace Aquila.Models
{
    public sealed record CpuSnapshot
    {
        public string? Name { get; init; }
        public string? Summary { get; init; }
        public MetricValue Temperature { get; init; } = new();
        public MetricValue Load { get; init; } = new();
        public MetricValue EffectiveClock { get; init; } = new();
        public MetricValue Power { get; init; } = new();
        public MetricValue FanRpm { get; init; } = new();
        public MetricValue Fan2Rpm { get; init; } = new();
        public IReadOnlyList<CpuCoreSnapshot> Cores { get; init; } = [];
    }

    public sealed record CpuCoreSnapshot
    {
        public string Label { get; init; } = string.Empty;
        public MetricValue Load { get; init; } = new();
    }
}