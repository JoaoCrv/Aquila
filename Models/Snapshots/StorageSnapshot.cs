namespace Aquila.Models
{
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
}