using Aquila.Models;
using Aquila.Services;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Aquila.ViewModels.Pages
{
    public partial class StorageViewModel : ObservableObject, IDisposable
    {
        private readonly HardwareMonitorService _monitor;

        [ObservableProperty]
        private List<StoragePageDriveItem> _drives = [];

        public StorageViewModel(HardwareMonitorService monitor)
        {
            _monitor = monitor;
            _monitor.DataUpdated += OnDataUpdated;
            OnDataUpdated();
        }

        private void OnDataUpdated()
        {
            var storageSnapshots = _monitor.CurrentSnapshot.Storage.ToList();
            var fixedDrives = DriveInfo.GetDrives()
                .Where(drive => drive.IsReady && drive.DriveType == DriveType.Fixed)
                .OrderBy(drive => drive.Name)
                .ToList();

            if (Drives.Count != fixedDrives.Count)
            {
                Drives = fixedDrives
                    .Select((drive, index) => new StoragePageDriveItem(
                        drive,
                        index < storageSnapshots.Count ? storageSnapshots[index] : null))
                    .ToList();
                return;
            }

            for (int i = 0; i < Drives.Count; i++)
            {
                var snapshot = i < storageSnapshots.Count ? storageSnapshots[i] : null;
                Drives[i].Refresh(fixedDrives[i], snapshot);
            }
        }

        public void Dispose() => _monitor.DataUpdated -= OnDataUpdated;
    }

    public partial class StoragePageDriveItem : ObservableObject
    {
        [ObservableProperty] private string _driveLetter = "";

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(DisplayName))]
        private string _label = "";

        [ObservableProperty] private string _fileSystem = "";
        [ObservableProperty] private string _totalCapacity = "";
        [ObservableProperty] private string _usedCapacity = "";
        [ObservableProperty] private string _freeCapacity = "";
        [ObservableProperty] private double _usedPercent;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(DisplayName))]
        private string? _hardwareName;

        [ObservableProperty] private string? _driveTypeTag;
        [ObservableProperty] private MetricValue _temperature = new();
        [ObservableProperty] private MetricValue _readRate = new();
        [ObservableProperty] private MetricValue _writeRate = new();
        [ObservableProperty] private MetricValue _dataRead = new();
        [ObservableProperty] private MetricValue _dataWritten = new();

        public string DisplayName => HardwareName ?? Label;

        public StoragePageDriveItem(DriveInfo drive, StorageDeviceSnapshot? snapshot) => Refresh(drive, snapshot);

        public void Refresh(DriveInfo drive, StorageDeviceSnapshot? snapshot)
        {
            DriveLetter = drive.Name.TrimEnd('\\', '/');
            Label = string.IsNullOrWhiteSpace(drive.VolumeLabel)
                ? drive.Name.TrimEnd('\\', '/')
                : drive.VolumeLabel;
            FileSystem = drive.DriveFormat;

            long total = drive.TotalSize;
            long free = drive.AvailableFreeSpace;
            long used = total - free;

            TotalCapacity = FormatBytes(total);
            UsedCapacity = FormatBytes(used);
            FreeCapacity = FormatBytes(free);
            UsedPercent = total > 0 ? (double)used / total * 100.0 : 0;

            HardwareName = snapshot?.Name;
            DriveTypeTag = snapshot?.TypeTag;
            Temperature = snapshot?.Temperature ?? new MetricValue();
            ReadRate = snapshot?.ReadRate ?? new MetricValue();
            WriteRate = snapshot?.WriteRate ?? new MetricValue();
            DataRead = snapshot?.DataRead ?? new MetricValue();
            DataWritten = snapshot?.DataWritten ?? new MetricValue();
        }

        private static string FormatBytes(long bytes)
        {
            if (bytes >= 1_000_000_000_000L) return $"{bytes / 1_000_000_000_000.0:F1} TB";
            if (bytes >= 1_000_000_000L) return $"{bytes / 1_000_000_000.0:F0} GB";
            return $"{bytes / 1_000_000.0:F0} MB";
        }

    }
}
