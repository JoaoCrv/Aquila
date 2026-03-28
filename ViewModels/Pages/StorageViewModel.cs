using Aquila.Helpers;
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
        }

        private void OnDataUpdated()
        {
            var lhmDrives = SensorLocator.AllStorageDrives(_monitor.ComputerData).ToList();
            var fixedDrives = DriveInfo.GetDrives()
                .Where(d => d.IsReady && d.DriveType == DriveType.Fixed)
                .OrderBy(d => d.Name)
                .ToList();

            if (Drives.Count != fixedDrives.Count)
            {
                var items = new List<StoragePageDriveItem>();
                for (int i = 0; i < fixedDrives.Count; i++)
                {
                    var lhm = i < lhmDrives.Count ? lhmDrives[i] : null;
                    items.Add(new StoragePageDriveItem(fixedDrives[i], lhm));
                }
                Drives = items;
            }
            else
            {
                for (int i = 0; i < Drives.Count; i++)
                {
                    var lhm = i < lhmDrives.Count ? lhmDrives[i] : null;
                    Drives[i].Refresh(fixedDrives[i], lhm);
                }
            }
        }

        public void Dispose() => _monitor.DataUpdated -= OnDataUpdated;

    }

    public partial class StoragePageDriveItem : ObservableObject
    {
        [ObservableProperty] private string _driveLetter  = "";
        [ObservableProperty] private string _label        = "";
        [ObservableProperty] private string _fileSystem   = "";
        [ObservableProperty] private string _totalCapacity = "";
        [ObservableProperty] private string _usedCapacity  = "";
        [ObservableProperty] private string _freeCapacity  = "";
        [ObservableProperty] private double _usedPercent;

        [ObservableProperty] private string?     _lhmName;
        [ObservableProperty] private string?     _driveTypeTag;
        [ObservableProperty] private DataSensor? _tempSensor;
        [ObservableProperty] private DataSensor? _readSensor;
        [ObservableProperty] private DataSensor? _writeSensor;

        public StoragePageDriveItem(DriveInfo drive, DataHardware? lhm) => Refresh(drive, lhm);

        public void Refresh(DriveInfo drive, DataHardware? lhm)
        {
            DriveLetter = drive.Name.TrimEnd('\\', '/');
            Label       = string.IsNullOrWhiteSpace(drive.VolumeLabel)
                ? drive.Name.TrimEnd('\\', '/')
                : drive.VolumeLabel;
            FileSystem  = drive.DriveFormat;

            long total = drive.TotalSize;
            long free  = drive.AvailableFreeSpace;
            long used  = total - free;

            TotalCapacity = FormatBytes(total);
            UsedCapacity  = FormatBytes(used);
            FreeCapacity  = FormatBytes(free);
            UsedPercent   = total > 0 ? (double)used / total * 100.0 : 0;

            LhmName      = lhm?.Name;
            DriveTypeTag = DetectType(lhm);
            TempSensor   = lhm is null ? null : SensorLocator.StorageTemperatureFor(lhm);
            ReadSensor   = lhm is null ? null : SensorLocator.StorageReadRateFor(lhm);
            WriteSensor  = lhm is null ? null : SensorLocator.StorageWriteRateFor(lhm);
        }

        private static string FormatBytes(long bytes)
        {
            if (bytes >= 1_000_000_000_000L) return $"{bytes / 1_000_000_000_000.0:F1} TB";
            if (bytes >= 1_000_000_000L)     return $"{bytes / 1_000_000_000.0:F0} GB";
            return $"{bytes / 1_000_000.0:F0} MB";
        }

        private static string? DetectType(DataHardware? lhm)
        {
            if (lhm is null) return null;
            if (lhm.Identifier.Contains("/nvme/", StringComparison.OrdinalIgnoreCase)) return "NVMe";
            if (lhm.Identifier.Contains("/ata/",  StringComparison.OrdinalIgnoreCase)) return "SATA";
            return null;
        }
    }
}
