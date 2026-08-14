using System.Globalization;
using System.Windows.Data;

namespace Aquila.Helpers
{
    /// <summary>
    /// Converts a throughput value in B/s (as reported by LibreHardwareMonitor)
    /// to a human-readable string: B/s, KB/s or MB/s.
    ///
    /// <c>ConverterParameter</c> selects which part is wanted: <c>value</c> for the number alone,
    /// <c>unit</c> for the scale alone, anything else for both. The split exists because a card header
    /// sets the number and its unit at different sizes — and because the scale is chosen here, from the
    /// magnitude, so no caller can work out the unit on its own.
    /// </summary>
    [ValueConversion(typeof(float), typeof(string))]
    public class ThroughputConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var part = parameter as string;

            if (value is not float bytes)
                return string.Equals(part, "unit", StringComparison.OrdinalIgnoreCase) ? string.Empty : "--";

            var (number, unit) = bytes switch
            {
                >= 1_048_576f => ($"{bytes / 1_048_576f:F1}", "MB/s"),
                >= 1024f      => ($"{bytes / 1024f:F1}",      "KB/s"),
                _             => ($"{bytes:F0}",              "B/s"),
            };

            if (string.Equals(part, "value", StringComparison.OrdinalIgnoreCase)) return number;
            if (string.Equals(part, "unit", StringComparison.OrdinalIgnoreCase)) return unit;
            return $"{number} {unit}";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}
