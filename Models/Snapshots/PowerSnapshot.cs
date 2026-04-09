namespace Aquila.Models
{
    public sealed record PowerSnapshot
    {
        public MetricValue Cpu { get; init; } = new();
        public MetricValue Memory { get; init; } = new();
        public MetricValue Gpu { get; init; } = new();
        public MetricValue Total { get; init; } = new();
    }
}