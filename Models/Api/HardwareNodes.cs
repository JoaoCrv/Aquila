using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;

namespace Aquila.Models.Api
{
    public partial class HardwareNodes : ObservableObject
    {
        public MotherboardNode Motherboard { get; } = new();
        public CpuNode Cpu { get; } = new();
        public MemoryNode Memory { get; } = new();
        public ObservableCollection<GpuNode> Gpus { get; } = [];
        public ObservableCollection<StorageNode> Drives { get; } = [];
        public ObservableCollection<NetworkNode> NetworkAdapters { get; } = [];
    }

    public partial class MotherboardNode : ObservableObject
    {
        [ObservableProperty] private string _name = string.Empty;
        
        public SensorNode CpuTemperature { get; } = new("CPU");
        public SensorNode SystemTemperature { get; } = new("System");
        public SensorNode VrmTemperature { get; } = new("VRM");
        public SensorNode ChipsetTemperature { get; } = new("Chipset");

        public ObservableCollection<FanNode> Fans { get; } = [];
    }

    public partial class CpuNode : ObservableObject
    {
        [ObservableProperty] private string _name = string.Empty;
        
        public SensorNode Load { get; } = new("Core (Avg)");
        public SensorNode Temperature { get; } = new("Package");
        public SensorNode Power { get; } = new("Package Power");
        public SensorNode Clock { get; } = new("Core (Avg) Clock");
        
        public ObservableCollection<SensorNode> Cores { get; } = [];
    }

    public partial class GpuNode : ObservableObject
    {
        [ObservableProperty] private string _name = string.Empty;
        [ObservableProperty] private string _vendor = string.Empty; 
        
        public SensorNode Load { get; } = new("Core");
        public SensorNode Temperature { get; } = new("Core");
        public SensorNode HotSpotTemperature { get; } = new("Hot Spot");
        public SensorNode Power { get; } = new("Total Power");
        public SensorNode Clock { get; } = new("Core Clock");
        public SensorNode MemoryClock { get; } = new("Memory Clock");
        
        public SensorNode VramUsed { get; } = new("VRAM Used");
        public SensorNode VramTotal { get; } = new("VRAM Total");

        public ObservableCollection<FanNode> Fans { get; } = [];
    }

    public partial class MemoryNode : ObservableObject
    {
        [ObservableProperty] private string _name = "System Memory";

        public SensorNode UsedGb { get; } = new("Used");
        public SensorNode AvailableGb { get; } = new("Available");
        public SensorNode TotalGb { get; } = new("Total");
        public SensorNode Load { get; } = new("Load");
    }

    public partial class StorageNode : ObservableObject
    {
        [ObservableProperty] private string _name = string.Empty;
        
        public SensorNode Temperature { get; } = new("Temperature");
        public SensorNode UsedPercent { get; } = new("Used Space");
        public SensorNode ReadRate { get; } = new("Read Rate");
        public SensorNode WriteRate { get; } = new("Write Rate");
        
        public SensorNode DataRead { get; } = new("Data Read");
        public SensorNode DataWritten { get; } = new("Data Written");
    }

    public partial class NetworkNode : ObservableObject
    {
        [ObservableProperty] private string _name = string.Empty;
        
        public SensorNode UploadSpeed { get; } = new("Upload Speed");
        public SensorNode DownloadSpeed { get; } = new("Download Speed");
        public SensorNode DataUploaded { get; } = new("Data Uploaded");
        public SensorNode DataDownloaded { get; } = new("Data Downloaded");
    }
}
