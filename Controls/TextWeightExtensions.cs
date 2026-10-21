using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Aquila.Models;

namespace Aquila.Controls;

/// <summary>Turns the preset's five steps into what WPF understands. The one drawing world left that writes text:
/// the mapping to Skia went with the dial's painted number.</summary>
public static class TextWeightExtensions
{
    public static FontWeight ToFontWeight(this TextWeight weight) => weight switch
    {
        TextWeight.Light => FontWeights.Light,
        TextWeight.Medium => FontWeights.Medium,
        TextWeight.SemiBold => FontWeights.SemiBold,
        TextWeight.Bold => FontWeights.Bold,
        _ => FontWeights.Normal,
    };

    /// <summary>Puts a face and a weight on a piece of text. Cleared rather than defaulted when no family
    /// is named: an explicit family would override whatever the theme is using, and "none" means keep it.
    /// </summary>
    public static void Wear(this TextBlock text, string? family, TextWeight weight)
    {
        if (string.IsNullOrWhiteSpace(family)) text.ClearValue(TextBlock.FontFamilyProperty);
        else text.FontFamily = new FontFamily(family);

        text.FontWeight = weight.ToFontWeight();
    }
}
