using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Aquila.Models.Nodes;

public class SensorGroup
{
    private readonly Dictionary<string, SensorNode> _sensors = new();

    public SensorNode GetOrCreate(string name)
    {
        if (!_sensors.TryGetValue(name, out var node))
        {
            node = new SensorNode();
            _sensors[name] = node;
        }
        return node;
    }

    public IReadOnlyDictionary<string, SensorNode> All => _sensors;
}
