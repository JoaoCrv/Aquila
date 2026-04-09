using System.Globalization;
using System.Windows.Data;

namespace Aquila.Helpers;

/// <summary>
/// Returns the current sensor maximum as a ProgressBar maximum.
/// Falls back to <see cref="Fallback"/> when Max is zero or negative
/// (sensor not yet polled) to avoid a Maximum=0 invalid state.
/// </summary>
[ValueConversion(typeof(float), typeof(double))]
public sealed class FanMaxConverter : IValueConverter
{
    public double Fallback { get; set; } = 3000.0;

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is float f && f > 0f) return (double)f;
        if (value is double d && d > 0.0) return d;
        return Fallback;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
