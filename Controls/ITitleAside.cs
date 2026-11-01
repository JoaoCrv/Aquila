using System.Windows;

namespace Aquila.Controls;

/// <summary>
/// A piece that writes something at the far end of its widget's title line — a list's total, beside the name of
/// what it is the total of.
///
/// The title is the tile's and not the piece's, so the piece hands over an element and the tile places it (see
/// <see cref="LabeledTile.Aside"/>). The element stays the piece's: the piece fills it, colours it and hides it, and
/// the tile gives it room only while it is visible — so a piece with nothing to say there leaves the title exactly as
/// it was.
/// </summary>
public interface ITitleAside
{
    FrameworkElement Aside { get; }
}
