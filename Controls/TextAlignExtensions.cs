using System.Windows;
using Aquila.Models;

namespace Aquila.Controls;

/// <summary>
/// Between our stored alignment and WPF's.
///
/// A model enum of our own rather than <see cref="TextAlignment"/> directly: the definition is serialised
/// to widgets.json by name, and a persisted format should not be able to grow a value because a UI
/// framework added one — WPF's has a Justify we would never offer.
/// </summary>
internal static class TextAlignExtensions
{
    public static TextAlignment ToTextAlignment(this TextAlign align) => align switch
    {
        TextAlign.Left => TextAlignment.Left,
        TextAlign.Right => TextAlignment.Right,
        _ => TextAlignment.Center,
    };

    public static TextAlign ToAlign(this TextAlignment alignment) => alignment switch
    {
        TextAlignment.Left => TextAlign.Left,
        TextAlignment.Right => TextAlign.Right,
        _ => TextAlign.Center,
    };
}
