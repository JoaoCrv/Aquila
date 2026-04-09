using Aquila.Models;
using LibreHardwareMonitor.Hardware;

namespace Aquila.Helpers
{
    public static partial class AquilaSnapshotBuilder
    {
        // Network

        private static DataSensor? NetworkUploadSpeed(ComputerData data) =>
            Find(data, HardwareType.Network, SensorType.Throughput, "Upload")
            ?? FindFirst(data, HardwareType.Network, SensorType.Throughput);

        private static DataSensor? NetworkDownloadSpeed(ComputerData data) =>
            Find(data, HardwareType.Network, SensorType.Throughput, "Download")
            ?? IndexedSensor(FirstHardware(data, HardwareType.Network), SensorType.Throughput, 1);

        private static DataSensor? NetworkDataUploaded(ComputerData data) =>
            Find(data, HardwareType.Network, SensorType.Data, "Data Uploaded")
            ?? FindFirst(data, HardwareType.Network, SensorType.Data);

        private static DataSensor? NetworkDataDownloaded(ComputerData data) =>
            Find(data, HardwareType.Network, SensorType.Data, "Data Downloaded")
            ?? IndexedSensor(FirstHardware(data, HardwareType.Network), SensorType.Data, 1);

        private static NetworkSnapshot BuildNetworkSnapshot(ComputerData data)
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
