namespace Aquila.Models
{
    public sealed record MetricValue
    {
        public double? Value { get; init; }
        public double? Min { get; init; }
        public double? Max { get; init; }
        public string Unit { get; init; } = string.Empty;
        public string? SourceName { get; init; }
        public string? SourceIdentifier { get; init; }

        public static MetricValue FromSensor(SensorReading? sensor) => sensor is null
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
}