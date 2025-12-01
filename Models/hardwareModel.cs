using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Aquila.Models
{
    /// <summary>
    ///     This class implements InotifyPropertyChanged.
    ///     Any class that needs to notify the UI of property changes should inherit from this class.
    ///     This is the motor for data binding in WPF applications.
    /// </summary>
    public abstract class ObservableObject : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    /// <summary>
    ///  This represents a unicque sensor (ex: "CPU Temperature", "Gpu Power") 
    ///  

    public class SensorModel: ObservableObject
    {
        private float _value;
        private string? _unit;
        private string Name { get; set; }
        private string Identifier { get; set; } //unique ID for the sensor, ex: "/amdcpu/0/temperature/0"

        //when the value of the sensor changes, he calls onPropertyChanged("value")
        //this makes that any control binded to this property to update its value automatically

        public float Value
        {
            get=> _value;
            set
            {
              
                    _value = value;
                    OnPropertyChanged(nameof(Value));
                
            }
        }

        public string? Unit
        {
            get => _unit;
            set
            {
                _unit = value;
                OnPropertyChanged(nameof(Unit));
            }
        }


    }

    public class HardwareModel : ObservableObject
    {
        public string Name { get; set; }
        public Dictionary<string, SensorModel> Sensors { get;  } = new ();
    }
}
