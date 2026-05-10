using System.Collections;

namespace Aquila.Models;

public class SensorGroup : IEnumerable<SensorNode>
{
    private readonly Dictionary<string, SensorNode> _sensors = new();

    public SensorNode GetOrCreate(string name)
    {
        if (!_sensors.TryGetValue(name, out var node))
        {
            node = new SensorNode { Name = name };
            _sensors[name] = node;
        }
        return node;
    }

    public SensorNode? this[string name]
        => _sensors.TryGetValue(name, out var node) ? node : null;

    public int Count => _sensors.Count;

    public IEnumerator<SensorNode> GetEnumerator() => _sensors.Values.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => _sensors.Values.GetEnumerator();
}