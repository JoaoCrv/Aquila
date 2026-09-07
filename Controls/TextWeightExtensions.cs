using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Aquila.Models;

namespace Aquila.Controls;

/// <summary>Turns the preset's five steps into what each drawing world understands. Two mappings in one
/// file, so a step added later cannot be taught to one of them and forgotten in the other.</summary>
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

    public static SkiaSharp.SKFontStyleWeight ToSkiaWeight(this TextWeight weight) => weight switch
    {
        TextWeight.Light => SkiaSharp.SKFontStyleWeight.Light,
        TextWeight.Medium => SkiaSharp.SKFontStyleWeight.Medium,
        TextWeight.SemiBold => SkiaSharp.SKFontStyleWeight.SemiBold,
        TextWeight.Bold => SkiaSharp.SKFontStyleWeight.Bold,
        _ => SkiaSharp.SKFontStyleWeight.Normal,
    };
}
