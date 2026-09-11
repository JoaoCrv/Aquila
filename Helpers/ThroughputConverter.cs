using System.Globalization;
using System.Windows.Data;
using Aquila.Controls;

namespace Aquila.Helpers
{
    /// <summary>
    /// Writes a throughput value, reported in B/s by LibreHardwareMonitor, at whatever scale suits its
    /// magnitude.
    ///
    /// The rule itself is <see cref="SensorFormat"/>'s, and this only reaches it from XAML. It used to hold
    /// its own copy, which is how the dashboard came to scale a drive's throughput while the desktop
    /// widgets drew the raw bytes beside a unit that said megabytes.
    ///
    /// <c>ConverterParameter</c> selects which part is wanted: <c>value</c> for the number alone,
    /// <c>unit</c> for the scale alone, anything else for both. The split exists because a card header sets
    /// the number and its unit at different sizes — and because the scale is chosen from the magnitude, so
    /// no caller can work out the unit on its own.
    /// </summary>
    [ValueConversion(typeof(float), typeof(string))]
    public class ThroughputConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var part = parameter as string;

            if (value is not float bytes)
                return string.Equals(part, "unit", StringComparison.OrdinalIgnoreCase) ? string.Empty : "--";

            var (number, unit) = SensorFormat.Parts(bytes, SensorFormat.BytesPerSecond);

            if (string.Equals(part, "value", StringComparison.OrdinalIgnoreCase)) return number;
            if (string.Equals(part, "unit", StringComparison.OrdinalIgnoreCase)) return unit;
            return $"{number} {unit}";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}
