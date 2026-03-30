using System.Globalization;
using System.Windows.Data;

namespace Aquila.Helpers
{
    /// <summary>
    /// Converts a throughput value in B/s (as reported by LibreHardwareMonitor)
    /// to a human-readable string: B/s, KB/s or MB/s.
    /// </summary>
    [ValueConversion(typeof(float), typeof(string))]
    public class ThroughputConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is not float bytes)
                return "--";

            if (bytes >= 1_048_576f)
                return $"{bytes / 1_048_576f:F1} MB/s";

            if (bytes >= 1024f)
                return $"{bytes / 1024f:F1} KB/s";

            return $"{bytes:F0} B/s";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}
