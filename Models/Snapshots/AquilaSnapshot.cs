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
}
