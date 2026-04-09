using CommunityToolkit.Mvvm.ComponentModel;

namespace Aquila.Models.Api
{
    public partial class SensorNode : ObservableObject
    {
        [ObservableProperty]
        private string _name;

        [ObservableProperty]
        private float? _value;
        
        [ObservableProperty]
        private float? _min;

        [ObservableProperty]
        private float? _max;

        public SensorNode(string defaultName = "")
        {
            _name = defaultName;
        }
    }

    public partial class FanNode : SensorNode
    {
        [ObservableProperty]
        private float? _controlPercent;

        [ObservableProperty]
        private float? _maxRpm;

        public FanNode(string defaultName = "") : base(defaultName) { }
    }
}
