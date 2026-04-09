using CommunityToolkit.Mvvm.ComponentModel;

namespace Aquila.Models.Api
{
    public partial class AquilaState : ObservableObject
    {
        public HardwareNodes Hardware { get; } = new();
    }
}
