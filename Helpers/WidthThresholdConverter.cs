using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Aquila.Helpers
{
    /// <summary>
    /// Returns Visible when the bound width is >= the threshold supplied via ConverterParameter,
    /// Collapsed otherwise. Used for adaptive card content based on column width.
    /// </summary>
    [ValueConversion(typeof(double), typeof(Visibility))]
    public class WidthThresholdConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is not double w) return Visibility.Visible;
            double threshold = parameter is string s
                && double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out double t)
                ? t : 300;
            return w >= threshold ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => DependencyProperty.UnsetValue;
    }
}
