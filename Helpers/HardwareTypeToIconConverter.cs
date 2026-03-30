using LibreHardwareMonitor.Hardware;
using System.Globalization;
using System.Windows.Data;
using Wpf.Ui.Controls;

namespace Aquila.Helpers
{
    /// <summary>
    /// Converts a LibreHardwareMonitor HardwareType to a WPF-UI SymbolRegular icon.
    /// </summary>
    public class HardwareTypeToIconConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is HardwareType type)
            {
                return type switch
                {
                    HardwareType.Cpu => SymbolRegular.Molecule24,
                    HardwareType.GpuAmd or
                    HardwareType.GpuNvidia or
                    HardwareType.GpuIntel => SymbolRegular.DrawImage24,
                    HardwareType.Memory => SymbolRegular.Ram20,
                    HardwareType.Motherboard => SymbolRegular.Grid28,
                    HardwareType.Storage => SymbolRegular.HardDrive24,
                    HardwareType.Network => SymbolRegular.NetworkCheck24,
                    HardwareType.Battery => SymbolRegular.Battery924,
                    HardwareType.Cooler => SymbolRegular.WeatherSnowflake24,
                    _ => SymbolRegular.Desktop24,
                };
            }
            return SymbolRegular.Desktop24;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            throw new NotImplementedException();
    }
}
