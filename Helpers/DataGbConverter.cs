using System.Globalization;
using System.Windows.Data;

namespace Aquila.Helpers
{
    /// <summary>
    /// Converts a data value in GB (as reported by LibreHardwareMonitor SensorType.Data)
    /// to a human-readable string: GB or TB.
    /// </summary>
    [ValueConversion(typeof(float), typeof(string))]
    public class DataGbConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is not float gb) return "--";
            if (gb >= 1000f) return $"{gb / 1000f:F1} TB";
            return $"{gb:F0} GB";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}
