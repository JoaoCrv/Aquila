using System.Collections;

namespace Aquila.Models;

public class SensorCollection : IEnumerable<SensorNode>
{
    private readonly List<SensorNode> _sensors = new();

    public SensorNode GetOrCreate(int index)
    {
        while (_sensors.Count <= index)
            _sensors.Add(new SensorNode());
        return _sensors[index];
    }

    public SensorNode? this[int index]
        => index < _sensors.Count ? _sensors[index] : null;

    public int Count => _sensors.Count;

    public IEnumerator<SensorNode> GetEnumerator() => _sensors.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => _sensors.GetEnumerator();
}