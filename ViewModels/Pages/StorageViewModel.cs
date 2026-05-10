using Aquila.Models;
using Aquila.Models.Nodes;
using Aquila.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;

namespace Aquila.ViewModels.Pages;

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
        var storages = _aquila.State.Hardware.Storages;
        var fixedDrives = DriveInfo.GetDrives()
            .Where(d => d.IsReady && d.DriveType == DriveType.Fixed)
            .OrderBy(d => d.Name)
            .ToList();

        if (Drives.Count != fixedDrives.Count)
        {
            Drives = fixedDrives
                .Select((drive, i) => new StoragePageDriveItem(
                    drive, i < storages.Count ? storages[i] : null))
                .ToList();
            return;
        }

        for (int i = 0; i < Drives.Count; i++)
            Drives[i].Refresh(fixedDrives[i], i < storages.Count ? storages[i] : null);
    }

    public void Dispose() => _aquila.DataUpdated -= OnDataUpdated;
}

public partial class StoragePageDriveItem : ObservableObject
{
    [ObservableProperty] private string  _driveLetter   = "";
    [ObservableProperty] private string  _displayName   = "";
    [ObservableProperty] private string? _driveTypeTag;
    [ObservableProperty] private string  _fileSystem     = "";
    [ObservableProperty] private string  _totalCapacity  = "";
    [ObservableProperty] private string  _usedCapacity   = "";
    [ObservableProperty] private string  _freeCapacity   = "";
    [ObservableProperty] private double  _usedPercent;
    [ObservableProperty] private string? _hardwareName;
    [ObservableProperty] private SensorNode? _temperature;
    [ObservableProperty] private SensorNode? _readRate;
    [ObservableProperty] private SensorNode? _writeRate;
    [ObservableProperty] private SensorNode? _dataRead;
    [ObservableProperty] private SensorNode? _dataWritten;
    [ObservableProperty] private SensorNode? _life;
    [ObservableProperty] private SensorNode? _powerOnHours;
    [ObservableProperty] private SensorNode? _availableSpare;

    public ObservableCollection<double> ReadHistory  { get; } = new(Enumerable.Repeat(0.0, 60));
    public ObservableCollection<double> WriteHistory { get; } = new(Enumerable.Repeat(0.0, 60));

    public StoragePageDriveItem(DriveInfo drive, StorageNode? storage) => Refresh(drive, storage);

    public void Refresh(DriveInfo drive, StorageNode? storage)
    {
        DriveLetter = drive.Name.TrimEnd('\\', '/');
        DisplayName = storage?.Name
            ?? (!string.IsNullOrWhiteSpace(drive.VolumeLabel) ? drive.VolumeLabel : DriveLetter);
        FileSystem  = drive.DriveFormat;
        DriveTypeTag = storage?.Name is string n
            ? n.Contains("NVMe", StringComparison.OrdinalIgnoreCase) ? "NVMe"
            : n.Contains("SSD",  StringComparison.OrdinalIgnoreCase) ? "SSD"
            : n.Contains("HDD",  StringComparison.OrdinalIgnoreCase) ? "HDD"
            : null
            : null;

        long total = drive.TotalSize;
        long free  = drive.AvailableFreeSpace;
        long used  = total - free;
        TotalCapacity = FormatBytes(total);
        UsedCapacity  = FormatBytes(used);
        FreeCapacity  = FormatBytes(free);
        UsedPercent   = total > 0 ? (double)used / total * 100.0 : 0;

        HardwareName   = storage?.Name;
        Temperature    = storage?.Temperature.Primary;
        ReadRate       = storage?.Throughput.ReadRate;
        WriteRate      = storage?.Throughput.WriteRate;
        DataRead       = storage?.Data.Read;
        DataWritten    = storage?.Data.Written;
        Life           = storage?.Level.Life;
        PowerOnHours   = storage?.Factor.PowerOnHours;
        AvailableSpare = storage?.Level.AvailableSpare;

        ReadHistory.RemoveAt(0);
        ReadHistory.Add(storage?.Throughput.ReadRate.Value ?? 0);
        WriteHistory.RemoveAt(0);
        WriteHistory.Add(storage?.Throughput.WriteRate.Value ?? 0);
    }

    private static string FormatBytes(long bytes)
    {
        if (bytes >= 1_000_000_000_000L) return $"{bytes / 1_000_000_000_000.0:F1} TB";
        if (bytes >= 1_000_000_000L)     return $"{bytes / 1_000_000_000.0:F0} GB";
        return $"{bytes / 1_000_000.0:F0} MB";
    }
}
