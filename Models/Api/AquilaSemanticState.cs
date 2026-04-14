using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;

namespace Aquila.Models.Api
{
    public enum SemanticResolutionState
    {
        Missing,
        Matched,
        Ambiguous,
        Unsupported,
    }

    public partial class AquilaSemanticState : ObservableObject
    {
        public MotherboardSemanticNode Motherboard { get; } = new();
        public CpuSemanticNode Cpu { get; } = new();
        public MemorySemanticNode Memory { get; } = new();
        public NetworkSemanticNode Network { get; } = new();
        public ObservableCollection<GpuSemanticNode> Gpus { get; } = [];
        public ObservableCollection<StorageSemanticNode> Storage { get; } = [];
    }

    public partial class MotherboardSemanticNode : ObservableObject
    {
        [ObservableProperty] private string _name = string.Empty;
        public TemperatureSemanticGroup Temperature { get; } = new();
        public FanSemanticGroup Fan { get; } = new();
        public ControlSemanticGroup Control { get; } = new();
        public ObservableCollection<SensorNode> Temperatures { get; } = [];
        public ObservableCollection<FanNode> Fans { get; } = [];
    }

    public partial class CpuSemanticNode : ObservableObject
    {
        [ObservableProperty] private string _name = string.Empty;
        public LoadSemanticGroup Load { get; } = new();
        public TemperatureSemanticGroup Temperature { get; } = new();
        public ClockSemanticGroup Clock { get; } = new();
        public PowerSemanticGroup Power { get; } = new();
        public ObservableCollection<SensorNode> Cores { get; } = [];
    }

    public partial class MemorySemanticNode : ObservableObject
    {
        [ObservableProperty] private string _name = string.Empty;
        public LoadSemanticGroup Load { get; } = new();
        public DataSemanticGroup Data { get; } = new();
        public PowerSemanticGroup Power { get; } = new();
        [ObservableProperty] private SensorNode? _virtualLoad;
        [ObservableProperty] private SensorNode? _virtualUsed;
        [ObservableProperty] private SensorNode? _virtualAvailable;
        public ObservableCollection<MemoryDimmSemanticNode> Dimms { get; } = [];
    }

    public partial class MemoryDimmSemanticNode : ObservableObject
    {
        [ObservableProperty] private int _slot;
        [ObservableProperty] private string _name = string.Empty;
        [ObservableProperty] private SensorNode? _capacity;
        [ObservableProperty] private SensorNode? _temperature;
        [ObservableProperty] private SensorNode? _warningTemperature;
        [ObservableProperty] private SensorNode? _criticalTemperature;
    }

    public partial class NetworkSemanticNode : ObservableObject
    {
        [ObservableProperty] private string _primaryAdapterName = string.Empty;
        public ThroughputSemanticGroup Throughput { get; } = new();
        public DataSemanticGroup Data { get; } = new();
        public ResolutionNode PrimaryAdapterResolution { get; } = new();
    }

    public partial class GpuSemanticNode : ObservableObject
    {
        [ObservableProperty] private string _name = string.Empty;
        public LoadSemanticGroup Load { get; } = new();
        public TemperatureSemanticGroup Temperature { get; } = new();
        public ClockSemanticGroup Clock { get; } = new();
        public PowerSemanticGroup Power { get; } = new();
        public DataSemanticGroup Data { get; } = new();
        public FanSemanticGroup Fan { get; } = new();
    }

    public partial class StorageSemanticNode : ObservableObject
    {
        [ObservableProperty] private string _name = string.Empty;
        public TemperatureSemanticGroup Temperature { get; } = new();
        public ThroughputSemanticGroup Throughput { get; } = new();
        public LoadSemanticGroup Load { get; } = new();
        public DataSemanticGroup Data { get; } = new();
    }

    public partial class LoadSemanticGroup : ObservableObject
    {
        [ObservableProperty] private SensorNode? _total;
        [ObservableProperty] private SensorNode? _system;
        public SensorResolutionNode TotalResolution { get; } = new();
        public SensorResolutionNode SystemResolution { get; } = new();
    }

    public partial class TemperatureSemanticGroup : ObservableObject
    {
        [ObservableProperty] private SensorNode? _package;
        [ObservableProperty] private SensorNode? _system;
        [ObservableProperty] private SensorNode? _core;
        [ObservableProperty] private SensorNode? _hotspot;
        public SensorResolutionNode PackageResolution { get; } = new();
        public SensorResolutionNode SystemResolution { get; } = new();
        public SensorResolutionNode CoreResolution { get; } = new();
        public SensorResolutionNode HotspotResolution { get; } = new();
    }

    public partial class ClockSemanticGroup : ObservableObject
    {
        [ObservableProperty] private SensorNode? _effective;
        [ObservableProperty] private SensorNode? _core;
    }

    public partial class PowerSemanticGroup : ObservableObject
    {
        [ObservableProperty] private SensorNode? _package;
        [ObservableProperty] private SensorNode? _total;
        public SensorResolutionNode PackageResolution { get; } = new();
    }

    public partial class FanSemanticGroup : ObservableObject
    {
        [ObservableProperty] private SensorNode? _cpu;
        [ObservableProperty] private SensorNode? _primary;
        [ObservableProperty] private SensorNode? _secondary;
        public SensorResolutionNode CpuResolution { get; } = new();
        public SensorResolutionNode PrimaryResolution { get; } = new();
        public SensorResolutionNode SecondaryResolution { get; } = new();
    }

    public partial class ControlSemanticGroup : ObservableObject
    {
        [ObservableProperty] private SensorNode? _cpu;
        public SensorResolutionNode CpuResolution { get; } = new();
    }

    public partial class SensorResolutionNode : ObservableObject
    {
        [ObservableProperty] private SemanticResolutionState _state = SemanticResolutionState.Missing;
        [ObservableProperty] private int _candidateCount;
        [ObservableProperty] private string _reason = string.Empty;
    }

    public partial class ThroughputSemanticGroup : ObservableObject
    {
        [ObservableProperty] private SensorNode? _download;
        [ObservableProperty] private SensorNode? _upload;
        [ObservableProperty] private SensorNode? _read;
        [ObservableProperty] private SensorNode? _write;
        public SensorResolutionNode DownloadResolution { get; } = new();
        public SensorResolutionNode UploadResolution { get; } = new();
        public SensorResolutionNode ReadResolution { get; } = new();
        public SensorResolutionNode WriteResolution { get; } = new();
    }

    public partial class DataSemanticGroup : ObservableObject
    {
        [ObservableProperty] private SensorNode? _used;
        [ObservableProperty] private SensorNode? _available;
        [ObservableProperty] private SensorNode? _total;
        [ObservableProperty] private SensorNode? _cache;
        [ObservableProperty] private SensorNode? _pageReads;
        [ObservableProperty] private SensorNode? _pageWrites;
        [ObservableProperty] private SensorNode? _downloaded;
        [ObservableProperty] private SensorNode? _uploaded;
        [ObservableProperty] private SensorNode? _read;
        [ObservableProperty] private SensorNode? _written;
        public SensorResolutionNode DownloadedResolution { get; } = new();
        public SensorResolutionNode UploadedResolution { get; } = new();
        public SensorResolutionNode ReadResolution { get; } = new();
        public SensorResolutionNode WrittenResolution { get; } = new();
    }

    public partial class ResolutionNode : ObservableObject
    {
        [ObservableProperty] private SemanticResolutionState _state = SemanticResolutionState.Missing;
        [ObservableProperty] private int _candidateCount;
        [ObservableProperty] private string _reason = string.Empty;
    }
}