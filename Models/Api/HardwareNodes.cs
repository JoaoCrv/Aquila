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

    public abstract class BaseHardwareNode : ObservableObject
    {
        public ObservableCollection<SensorNode> Temperatures { get; } = [];
        public ObservableCollection<SensorNode> Loads { get; } = [];
        public ObservableCollection<SensorNode> Clocks { get; } = [];
        public ObservableCollection<SensorNode> Powers { get; } = [];
        public ObservableCollection<SensorNode> Voltages { get; } = [];
        public ObservableCollection<SensorNode> Data { get; } = [];
        public ObservableCollection<SensorNode> Throughput { get; } = [];
        public ObservableCollection<SensorNode> Timings { get; } = []; 
        public ObservableCollection<SensorNode> Controls { get; } = [];
        public ObservableCollection<FanNode> Fans { get; } = [];
    }

    public partial class MotherboardNode : BaseHardwareNode
    {
        [ObservableProperty] private string _name = string.Empty;
    }

    public partial class CpuNode : BaseHardwareNode
    {
        [ObservableProperty] private string _name = string.Empty;
    }

    public partial class GpuNode : BaseHardwareNode
    {
        [ObservableProperty] private string _name = string.Empty;
        [ObservableProperty] private string _vendor = string.Empty; 
    }

    public partial class MemoryNode : BaseHardwareNode
    {
        [ObservableProperty] private string _name = "System Memory";
    }

    public partial class StorageNode : BaseHardwareNode
    {
        [ObservableProperty] private string _name = string.Empty;
    }

    public partial class NetworkNode : BaseHardwareNode
    {
        [ObservableProperty] private string _name = string.Empty;
    }
}
