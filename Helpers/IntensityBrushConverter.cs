using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace Aquila.Helpers
{
    /// <summary>
    /// Maps a reading to the colour scheme's intensity roles — Normal, Elevated, Alert, Critical.
    ///
    /// This is the other half of the scheme model: the scheme says what "alert" LOOKS like, this says
    /// WHEN a value is alerting. They are deliberately separate — thresholds belong to the metric, not the
    /// palette, so restyling can't silently change meaning and a shared scheme can't impose someone else's
    /// limits.
    ///
    /// Thresholds come from the ConverterParameter as "elevated,alert,critical", in the value's own units:
    ///   <c>Converter={StaticResource Intensity}, ConverterParameter='50,70,85'</c> for temperatures in °C,
    ///   or '60,80,92' for a percentage. Below the first threshold the value reads as Normal.
    /// </summary>
    [ValueConversion(typeof(float), typeof(Brush))]
    public sealed class IntensityBrushConverter : IValueConverter
    {
        /// <summary>Percentage-shaped defaults, for the common case of a 0–100 load.</summary>
        private static readonly double[] _defaults = [60, 80, 92];

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // A missing reading is not a cold one: leave it at Normal rather than implying "all good".
            if (value is null) return Resolve("Normal");

            double reading = value switch
            {
                float f => f,
                double d => d,
                int i => i,
                _ => double.NaN,
            };

            if (double.IsNaN(reading)) return Resolve("Normal");

            var steps = Parse(parameter as string) ?? _defaults;

            var role = reading >= steps[2] ? "Critical"
                     : reading >= steps[1] ? "Alert"
                     : reading >= steps[0] ? "Elevated"
                     :                       "Normal";

            return Resolve(role);
        }

        /// <summary>Parses "elevated,alert,critical". A malformed parameter falls back to the defaults
        /// rather than throwing — a typo in XAML should not take the dashboard down.</summary>
        private static double[]? Parse(string? parameter)
        {
            if (string.IsNullOrWhiteSpace(parameter)) return null;

            var parts = parameter.Split(',');
            if (parts.Length != 3) return null;

            var steps = new double[3];
            for (int i = 0; i < 3; i++)
                if (!double.TryParse(parts[i], NumberStyles.Any, CultureInfo.InvariantCulture, out steps[i]))
                    return null;

            return steps;
        }

        private static Brush Resolve(string role) =>
            Application.Current?.TryFindResource($"Aquila.Scheme.{role}") as Brush ?? Brushes.Gray;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => Binding.DoNothing;
    }
}
