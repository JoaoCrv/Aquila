using System;

namespace Aquila.Models
{
    public sealed record GpuCollectionSnapshot
    {
        public GpuSnapshot? Primary { get; init; }
    }

    public sealed record GpuSnapshot
    {
        public string? Identifier { get; init; }
        public string? Name { get; init; }
        public MetricValue Temperature { get; init; } = new();
        public MetricValue Load { get; init; } = new();
        public MetricValue Clock { get; init; } = new();
        public MetricValue Power { get; init; } = new();
        public MetricValue FanRpm { get; init; } = new();
        public MetricValue Fan2Rpm { get; init; } = new();
        public MetricValue VramUsed { get; init; } = new();
        public MetricValue VramTotal { get; init; } = new();

        public double VramPercent => (VramTotal.Value ?? 0) > 0
            ? Math.Clamp(((VramUsed.Value ?? 0) / (VramTotal.Value ?? 1)) * 100.0, 0.0, 100.0)
            : 0.0;
    }
}