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
            if (values.Length < 4)
                return 0d;

            var value      = ToDouble(values[0]);
            var min        = ToDouble(values[1]);
            var max        = ToDouble(values[2]);
            var totalWidth = ToDouble(values[3]);

            if (max <= min || totalWidth <= 0)
                return 0d;

            var ratio = Math.Clamp((value - min) / (max - min), 0, 1);
            return Math.Max(0d, ratio * totalWidth);
        }

        private static double ToDouble(object v) => v switch
        {
            double d => d,
            float  f => f,
            int    i => i,
            string s when double.TryParse(s, System.Globalization.NumberStyles.Any,
                              System.Globalization.CultureInfo.InvariantCulture, out var p) => p,
            _        => 0d
        };

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}
