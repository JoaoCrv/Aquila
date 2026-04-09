using System;

namespace Aquila.Models
{
    /// <summary>
    /// Semantic hardware snapshot exposed to the UI.
    /// It contains ready-to-consume domain values instead of raw sensor lookups.
    /// </summary>
    public sealed record AquilaSnapshot
    {
        public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.Now;
        public CpuSnapshot Cpu { get; init; } = new();
        public GpuCollectionSnapshot Gpu { get; init; } = new();
        public MemorySnapshot Memory { get; init; } = new();
        public PowerSnapshot Power { get; init; } = new();
        public NetworkSnapshot Network { get; init; } = new();
        public IReadOnlyList<StorageDeviceSnapshot> Storage { get; init; } = [];
        public IReadOnlyList<TemperatureSnapshot> Temperatures { get; init; } = [];
        public IReadOnlyList<FanSnapshot> Fans { get; init; } = [];
    }

    public sealed record MetricValue
    {
        public double? Value { get; init; }
        public double? Min { get; init; }
        public double? Max { get; init; }
        public string Unit { get; init; } = string.Empty;
        public string? SourceName { get; init; }
        public string? SourceIdentifier { get; init; }

        public static MetricValue FromSensor(DataSensor? sensor) => sensor is null
            ? new MetricValue()
            : new MetricValue
            {
                Value = sensor.Value,
                Min = sensor.Min,
                Max = sensor.Max,
                Unit = sensor.Unit ?? string.Empty,
                SourceName = sensor.Name,
                SourceIdentifier = sensor.Identifier
            };

        public static MetricValue FromValue(double? value, string unit, string? sourceName = null) => new()
        {
            Value = value,
            Unit = unit,
            SourceName = sourceName
        };
    }

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

    public sealed record PowerSnapshot
    {
        public MetricValue Cpu { get; init; } = new();
        public MetricValue Memory { get; init; } = new();
        public MetricValue Gpu { get; init; } = new();
        public MetricValue Total { get; init; } = new();
    }

    public sealed record NetworkSnapshot
    {
        public string? Name { get; init; }
        public MetricValue UploadSpeed { get; init; } = new();
        public MetricValue DownloadSpeed { get; init; } = new();
        public MetricValue DataUploaded { get; init; } = new();
        public MetricValue DataDownloaded { get; init; } = new();
    }

    public sealed record StorageDeviceSnapshot
    {
        public string? Identifier { get; init; }
        public string? Name { get; init; }
        public string? TypeTag { get; init; }
        public MetricValue Temperature { get; init; } = new();
        public MetricValue UsedSpace { get; init; } = new();
        public MetricValue ReadRate { get; init; } = new();
        public MetricValue WriteRate { get; init; } = new();
        public MetricValue DataRead { get; init; } = new();
        public MetricValue DataWritten { get; init; } = new();
    }

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
