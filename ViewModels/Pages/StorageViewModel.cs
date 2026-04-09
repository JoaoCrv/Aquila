using Aquila.Models.Api;
using Aquila.Services;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Aquila.ViewModels.Pages
{
    public partial class StorageViewModel : ObservableObject, IDisposable
    {
        private readonly AquilaService _aquila;

        [ObservableProperty]
        private List<StoragePageDriveItem> _drives = [];

        public StorageViewModel(AquilaService aquila)
        {
            _aquila = aquila;
            _aquila.DataUpdated += OnDataUpdated;
            OnDataUpdated();
        }

        private void OnDataUpdated()
        {
            var storageSnapshots = _aquila.State.Hardware.Drives.ToList();
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

        public void Dispose() => _aquila.DataUpdated -= OnDataUpdated;
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

        [ObservableProperty] private SensorNode? _temperature;
        [ObservableProperty] private SensorNode? _readRate;
        [ObservableProperty] private SensorNode? _writeRate;
        [ObservableProperty] private SensorNode? _dataRead;
        [ObservableProperty] private SensorNode? _dataWritten;

        public string DisplayName => HardwareName ?? Label;

        public StoragePageDriveItem(DriveInfo drive, StorageNode? snapshot) => Refresh(drive, snapshot);

        public void Refresh(DriveInfo drive, StorageNode? snapshot)
        {
            DriveLetter = string.IsNullOrEmpty(drive.Name) ? "" : drive.Name.TrimEnd('\\', '/');
            Label = string.IsNullOrWhiteSpace(drive.VolumeLabel)
                ? DriveLetter
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
            Temperature = snapshot?.Temperature;
            ReadRate = snapshot?.ReadRate;
            WriteRate = snapshot?.WriteRate;
            DataRead = snapshot?.DataRead;
            DataWritten = snapshot?.DataWritten;
        }

        private static string FormatBytes(long bytes)
        {
            if (bytes >= 1_000_000_000_000L) return $"{bytes / 1_000_000_000_000.0:F1} TB";
            if (bytes >= 1_000_000_000L) return $"{bytes / 1_000_000_000.0:F0} GB";
            return $"{bytes / 1_000_000.0:F0} MB";
        }
    }
}
