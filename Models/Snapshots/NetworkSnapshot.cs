namespace Aquila.Models
{
    public sealed record NetworkSnapshot
    {
        public string? Name { get; init; }
        public MetricValue UploadSpeed { get; init; } = new();
        public MetricValue DownloadSpeed { get; init; } = new();
        public MetricValue DataUploaded { get; init; } = new();
        public MetricValue DataDownloaded { get; init; } = new();
    }
}