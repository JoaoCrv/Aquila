using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.CompilerServices;

namespace Aquila.Models;

public  class SensorNode : INotifyPropertyChanged
{
    private float? _value;
    private float? _min;
    private float? _max;
    private string? _unit;
    private string? _sensorName;
    private string? _identifier;

    public float? Value
        {
        get => _value;
        set
        {
            if (_value != value)
            {
                _value = value;
                OnPropertyChanged();
            }
        }
    }
    public float? Min
    {     get => _min;
        set
        {
            if (_min != value)
            {
                _min = value;
                OnPropertyChanged();
            }
        }
    }

    public float? Max
    {     get => _max;
        set
        {
            if (_max != value)
            {
                _max = value;
                OnPropertyChanged();
            }
        }
    }

    public string? Unit
    {
        get => _unit;
        set
        {
            if (_unit != value)
            {
                _unit = value;
                OnPropertyChanged();
            }
        }
    }

    public string? SensorName
    {
        get => _sensorName;
        set
        {
            if (_sensorName != value)
            {
                _sensorName = value;
                OnPropertyChanged();
            }
        }
    }

    public string? Identifier
    {
        get => _identifier;
        set { _identifier = value; OnPropertyChanged(); }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
