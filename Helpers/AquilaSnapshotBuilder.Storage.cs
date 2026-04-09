using Aquila.Models;
using LibreHardwareMonitor.Hardware;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Aquila.Helpers
{
    public static partial class AquilaSnapshotBuilder
    {
        // Storage

        private static IEnumerable<DataHardware> AllStorageDrives(ComputerData data) =>
            data.HardwareList.Where(hardware => hardware.HardwareType == HardwareType.Storage);

        private static DataSensor? StorageTemperatureFor(DataHardware drive) =>
            FirstSensor(drive, SensorType.Temperature);

        private static DataSensor? StorageReadRateFor(DataHardware drive) =>
            FindSensor(drive, SensorType.Throughput, "Read");

        private static DataSensor? StorageWriteRateFor(DataHardware drive) =>
            FindSensor(drive, SensorType.Throughput, "Write");

        private static DataSensor? StorageUsedSpaceFor(DataHardware drive) =>
            FindSensor(drive, SensorType.Load, "Used Space");

        private static DataSensor? StorageDataReadFor(DataHardware drive) =>
            FindSensor(drive, SensorType.Data, "Data Read");

        private static DataSensor? StorageDataWrittenFor(DataHardware drive) =>
            FindSensor(drive, SensorType.Data, "Data Written");

        private static IReadOnlyList<StorageDeviceSnapshot> BuildStorageSnapshots(ComputerData data) =>
            AllStorageDrives(data)
                .Select(drive => new StorageDeviceSnapshot
                {
                    Identifier = drive.Identifier,
                    Name = drive.Name,
                    TypeTag = DetectStorageType(drive.Identifier),
                    Temperature = MetricValue.FromSensor(StorageTemperatureFor(drive)),
                    UsedSpace = MetricValue.FromSensor(StorageUsedSpaceFor(drive)),
                    ReadRate = MetricValue.FromSensor(StorageReadRateFor(drive)),
                    WriteRate = MetricValue.FromSensor(StorageWriteRateFor(drive)),
                    DataRead = MetricValue.FromSensor(StorageDataReadFor(drive)),
                    DataWritten = MetricValue.FromSensor(StorageDataWrittenFor(drive))
                })
                .ToList();

        private static string? DetectStorageType(string? identifier)
        {
            if (string.IsNullOrWhiteSpace(identifier))
                return null;

            if (identifier.Contains("/nvme/", StringComparison.OrdinalIgnoreCase))
                return "NVMe";

            if (identifier.Contains("/ata/", StringComparison.OrdinalIgnoreCase))
                return "SATA";

            return null;
        }

    }
}
