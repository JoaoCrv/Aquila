using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using Aquila.Models;
using Aquila.Services;

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
    /// Thresholds come from the ConverterParameter, either as a preset name — <c>ConverterParameter=Temperature</c>
    /// or <c>ConverterParameter=Percent</c> — or spelled out as "elevated,alert,critical" in the value's
    /// own units (<c>ConverterParameter='50,70,85'</c>). Prefer the names: numbers written at a call site
    /// are a copy of <see cref="Thresholds"/> that can drift from it. Below the first step, Normal.
    /// </summary>
    [ValueConversion(typeof(float), typeof(Brush))]
    public sealed class IntensityBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // A missing reading is not a cold one: leave it at Normal rather than implying "all good".
            if (value is null) return VitalMonitor.Brush("Normal");

            double reading = value switch
            {
                float f => f,
                double d => d,
                int i => i,
                _ => double.NaN,
            };

            if (double.IsNaN(reading)) return VitalMonitor.Brush("Normal");

            // Everything below is the monitor's decision, not this converter's: it is handed what is being
            // read and how much, and never works out a colour itself.
            //
            // The parameter names a reading KIND — "Cpu.Temperature" — so a limit the user changes reaches
            // this binding. The pressure ribbon is the one exception: it draws a value that has already been
            // normalised, and its steps ARE that normalisation, so it is not a kind anyone can configure.
            var name = parameter as string;

            if (string.Equals(name, nameof(Thresholds.Pressure), StringComparison.OrdinalIgnoreCase))
                return VitalMonitor.Brush(Thresholds.Pressure.Role(reading));

            // A malformed parameter leaves the element at Normal rather than throwing — a typo in markup
            // should not take the dashboard down.
            if (MetricKey.Parse(name) is not { } key) return VitalMonitor.Brush("Normal");

            return VitalMonitor.Current?.BrushFor(reading, key)
                ?? VitalMonitor.Brush(Thresholds.Preset(key)?.Role(reading) ?? "Normal");
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => Binding.DoNothing;
    }
}
