using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using CommunityToolkit.Mvvm.ComponentModel;
using LibreHardwareMonitor.Hardware;

namespace Aquila.Models
{
        /// <summary>
    ///  This represents a unicque sensor (ex: "CPU Temperature", "Gpu Power") 
    ///  

    public class SensorModel: ObservableObject
    {
        private float _value;
        private string? _unit;
        public string Name { get; set; } = string.Empty;
        public string Identifier { get; set; } = string.Empty;//unique ID for the sensor, ex: "/amdcpu/0/temperature/0"
        public SensorType SensorType { get; set; }

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

    public class HardwareModel
    {
        public string Name { get; set; } = string.Empty;
        public Dictionary<string, SensorModel> Sensors { get; } = [];
    }
}
