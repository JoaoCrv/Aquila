using Aquila.Models;

namespace Aquila.Controls;

/// <summary>
/// A piece that holds a live <see cref="SensorNode"/> and subscribes to it, and therefore has to be told
/// when it is being thrown away.
///
/// It exists so unhooking is structural rather than remembered. Detaching used to be a switch over widget
/// kinds, which is the kind of list that fails quietly: a new kind simply would not match, keep its
/// subscription after the widget was removed, and go on updating an element nobody can see. Implementing
/// this is now the price of holding a sensor.
///
/// Pieces that take values rather than a node — SparklineChart, StatBox — need none of this: their
/// bindings are released with the element.
/// </summary>
public interface ISensorPiece
{
    SensorNode? Sensor { get; set; }
}
