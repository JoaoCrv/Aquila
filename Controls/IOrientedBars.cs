using Aquila.Models;

namespace Aquila.Controls;

/// <summary>
/// A piece drawn as a row of bars that can be turned on its side.
///
/// Its own interface for the usual reason: the meter is a bar too and cannot be turned — its label and its
/// number sit beside it, in a layout that only reads one way — so a piece either carries this or visibly
/// does not, and no switch over kinds decides it.
/// </summary>
public interface IOrientedBars
{
    BarDirection Direction { get; set; }
}
