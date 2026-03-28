using System;
using System.Globalization;
using System.Windows.Data;

namespace Aquila.Helpers
{
    /// <summary>
    /// Converts ScrollViewer.ActualWidth to the width of a single dashboard column,
    /// adapting between 3-column, 2-column, and 1-column layouts at breakpoints.
    /// Each column carries Margin.Right=8, so the formula ensures N columns exactly
    /// fill the available row:  N*(W+8) = available.
    /// Breakpoints (available = containerWidth - 36 padding):
    ///   >= 900px  → 3 cols : W = (available - 24) / 3
    ///   >= 580px  → 2 cols : W = (available - 16) / 2
    ///    else     → 1 col  : W =  available - 8
    /// </summary>
    [ValueConversion(typeof(double), typeof(double))]
    public class ColumnWidthConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is not double containerWidth || containerWidth <= 0)
                return 300.0;

            double available = containerWidth - 36; // 18+18 ScrollViewer left+right padding
            const double gap = 8.0;

            if (available >= 900) return Math.Max(0, (available - 3 * gap) / 3);
            if (available >= 580) return Math.Max(0, (available - 2 * gap) / 2);
            return Math.Max(0, available - gap);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => System.Windows.DependencyProperty.UnsetValue;
    }
}
