using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Aquila.Models
{
    public class SensorInfo
    {
        public required string HardwareName { get; set; }
        public required string SensorName { get; set; }
        public required string SensorType { get; set; }
        public required string Identifier { get; set; }
        public float Value { get; set; }

        public override string ToString()
        {
            return $"{HardwareName} - {SensorType} - {SensorName}: {Value}";
        }
    }
}
