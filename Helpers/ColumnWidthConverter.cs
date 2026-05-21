using System;
using System.Globalization;
using System.Windows.Data;

namespace Aquila.Helpers
{
    /// <summary>
    /// Converts the dashboard content panel ActualWidth to the width of a single
    /// column. Each column carries Margin.Right=8, so N*(W+8) = available.
    /// Breakpoints:
    ///   >= 1200px -> 4 cols : W = (available - 32) / 4
    ///   >=  900px -> 3 cols : W = (available - 24) / 3
    ///   >=  480px -> 2 cols : W = (available - 16) / 2
    ///    else     -> 1 col  : W =  available - 8
    /// </summary>
    [ValueConversion(typeof(double), typeof(double))]
    public class ColumnWidthConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is not double available || available <= 0)
                return 300.0;

            const double gap = 8.0;

            if (available >= 1200) return Math.Max(0, (available - 4 * gap) / 4);
            if (available >= 900)  return Math.Max(0, (available - 3 * gap) / 3);
            if (available >= 480)  return Math.Max(0, (available - 2 * gap) / 2);
            return Math.Max(0, available - gap);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => System.Windows.DependencyProperty.UnsetValue;
    }
}
