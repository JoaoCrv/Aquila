namespace Aquila.Models;

/// <summary>
/// How heavy text is drawn.
///
/// Five steps rather than the full numeric range: a preset is a design, and the useful distinctions are
/// "quieter than normal", "normal", and three degrees of emphasis. A slider from 100 to 900 would offer
/// two hundred choices, most of which no installed face can actually draw.
///
/// Named here rather than reusing System.Windows.FontWeights so the same value can cross to SkiaSharp,
/// which draws the gauge's centre number and knows nothing about WPF.
/// </summary>
public enum TextWeight
{
    Light,
    Regular,
    Medium,
    SemiBold,
    Bold,
}
