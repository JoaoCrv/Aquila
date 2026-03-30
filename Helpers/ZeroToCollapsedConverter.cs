using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Aquila.Helpers
{
    [ValueConversion(typeof(object), typeof(Visibility))]
    public class ZeroToCollapsedConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            double v = value switch
            {
                float  f => f,
                int    i => i,
                double d => d,
                _        => -1.0
            };
            return v == 0.0 ? Visibility.Collapsed : Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}
