using System.Globalization;
using System.Windows.Data;

namespace Aquila.Helpers
{
    /// <summary>
    /// Converts (Value, Minimum, Maximum, ActualWidth) into the pixel width
    /// of the filled portion of a rounded ProgressBar template.
    /// </summary>
    public class ProgressBarWidthConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length < 4
                || values[0] is not double value
                || values[1] is not double min
                || values[2] is not double max
                || values[3] is not double totalWidth)
                return 0d;

            if (max <= min || totalWidth <= 0)
                return 0d;

            double ratio = Math.Clamp((value - min) / (max - min), 0, 1);
            return Math.Max(0d, ratio * totalWidth);
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}
