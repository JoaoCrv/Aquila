using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace Aquila.Helpers
{
    /// <summary>
    /// Maps a temperature value (°C) to a theme-aware brush:
    ///   &lt; 50 °C  → Aquila.Ram  (green — normal)
    ///   50–79 °C → Aquila.Temp (orange — warm)
    ///   ≥  80 °C → Aquila.Critical (red — hot)
    /// </summary>
    [ValueConversion(typeof(float), typeof(Brush))]
    public sealed class ThermalBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            float temp = value switch
            {
                float f  => f,
                double d => (float)d,
                _        => 0f
            };

            string key = temp >= 80f ? "Aquila.Critical"
                       : temp >= 50f ? "Aquila.Temp"
                       :               "Aquila.Ram";

            return Application.Current?.TryFindResource(key) as Brush ?? Brushes.Gray;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => Binding.DoNothing;
    }
}
