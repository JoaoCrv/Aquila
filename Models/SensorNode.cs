using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Aquila.Models;

public class SensorNode : INotifyPropertyChanged
{
    private float? _value;
    private float? _min;
    private float? _max;
    private string? _unit;
    private string? _name;
    private string? _identifier;

    public float? Value
    {
        get => _value;
        set { if (_value != value) { _value = value; OnPropertyChanged(); } }
    }

    public float? Min
    {
        get => _min;
        set { if (_min != value) { _min = value; OnPropertyChanged(); } }
    }

    public float? Max
    {
        get => _max;
        set { if (_max != value) { _max = value; OnPropertyChanged(); } }
    }

    public string? Unit
    {
        get => _unit;
        set { if (_unit != value) { _unit = value; OnPropertyChanged(); } }
    }

    public string? Name
    {
        get => _name;
        set { if (_name != value) { _name = value; OnPropertyChanged(); } }
    }

    public string? Identifier
    {
        get => _identifier;
        set { if (_identifier != value) { _identifier = value; OnPropertyChanged(); } }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
