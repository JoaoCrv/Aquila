using System;
using System.Globalization;
using System.Windows.Data;

namespace Aquila.Helpers
{
    /// <summary>
    /// Converts the dashboard content panel ActualWidth to the width of a single
    /// column, adapting between a 2-column and 1-column layout.
    /// Each column carries Margin.Right=8, so N*(W+8) = available.
    /// Breakpoints:
    ///   >= 600px  -> 2 cols : W = (available - 16) / 2
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

            if (available >= 600) return Math.Max(0, (available - 2 * gap) / 2);
            return Math.Max(0, available - gap);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => System.Windows.DependencyProperty.UnsetValue;
    }
}
