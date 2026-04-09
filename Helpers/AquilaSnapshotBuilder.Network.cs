using Aquila.Models;
using LibreHardwareMonitor.Hardware;

namespace Aquila.Helpers
{
    public static partial class AquilaSnapshotBuilder
    {
        // Network

        private static SensorReading? NetworkUploadSpeed(HardwareState data) =>
            Find(data, HardwareType.Network, SensorType.Throughput, "Upload")
            ?? FindFirst(data, HardwareType.Network, SensorType.Throughput);

        private static SensorReading? NetworkDownloadSpeed(HardwareState data) =>
            Find(data, HardwareType.Network, SensorType.Throughput, "Download")
            ?? IndexedSensor(FirstHardware(data, HardwareType.Network), SensorType.Throughput, 1);

        private static SensorReading? NetworkDataUploaded(HardwareState data) =>
            Find(data, HardwareType.Network, SensorType.Data, "Data Uploaded")
            ?? FindFirst(data, HardwareType.Network, SensorType.Data);

        private static SensorReading? NetworkDataDownloaded(HardwareState data) =>
            Find(data, HardwareType.Network, SensorType.Data, "Data Downloaded")
            ?? IndexedSensor(FirstHardware(data, HardwareType.Network), SensorType.Data, 1);

        private static NetworkSnapshot BuildNetworkSnapshot(HardwareState data)
        {
            var network = FirstHardware(data, HardwareType.Network);

            return new NetworkSnapshot
            {
                Name = network?.Name,
                UploadSpeed = MetricValue.FromSensor(NetworkUploadSpeed(data)),
                DownloadSpeed = MetricValue.FromSensor(NetworkDownloadSpeed(data)),
                DataUploaded = MetricValue.FromSensor(NetworkDataUploaded(data)),
                DataDownloaded = MetricValue.FromSensor(NetworkDataDownloaded(data))
            };
        }
    }
}
