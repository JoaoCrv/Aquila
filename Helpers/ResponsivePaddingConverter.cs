using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Aquila.Helpers
{
    /// <summary>
    /// Returns a responsive <see cref="Thickness"/> that shrinks at smaller widths.
    /// ConverterParameter selects the profile:
    ///   "nav"     - NavigationView side padding  (42 | 24 | 12)
    ///   (default) - Page content padding          (18 | 10 | 6 horizontal)
    /// </summary>
    [ValueConversion(typeof(double), typeof(Thickness))]
    public class ResponsivePaddingConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is not double w || w <= 0)
                return new Thickness(0);

            return (parameter as string) switch
            {
                "nav" => w switch
                {
                    >= 1000 => new Thickness(42, 0, 42, 0),
                    >= 700  => new Thickness(24, 0, 24, 0),
                    _       => new Thickness(12, 0, 12, 0),
                },
                _ => w switch
                {
                    >= 900 => new Thickness(18, 6, 18, 18),
                    >= 600 => new Thickness(10, 4, 10, 10),
                    _      => new Thickness(6, 4, 6, 6),
                },
            };
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => DependencyProperty.UnsetValue;
    }
}
