namespace Aquila.Models
{
    public sealed record MemorySnapshot
    {
        public MetricValue LoadPercent { get; init; } = new();
        public MetricValue UsedGb { get; init; } = new();
        public MetricValue AvailableGb { get; init; } = new();
        public MetricValue Power { get; init; } = new();
        public MetricValue VirtualUsedGb { get; init; } = new();
        public MetricValue VirtualAvailableGb { get; init; } = new();
        public MetricValue PageReadsPerSec { get; init; } = new();
        public MetricValue PageWritesPerSec { get; init; } = new();
        public MetricValue CacheBytes { get; init; } = new();

        public double CacheGb => (CacheBytes.Value ?? 0d) / 1_073_741_824d;
        public double TotalVisibleGb => (UsedGb.Value ?? 0d) + (AvailableGb.Value ?? 0d);
    }
}