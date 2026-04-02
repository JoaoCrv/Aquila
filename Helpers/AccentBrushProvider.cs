using System.Windows;
using System.Windows.Media;
using Wpf.Ui.Appearance;

namespace Aquila.Helpers
{
    /// <summary>
    /// Singleton that exposes theme-aware accent brushes.
    /// Automatically switches between Dark/Light variants when the WPF-UI theme changes.
    /// <para>Usage in XAML: <c>Foreground="{Binding CpuAccent, Source={StaticResource AccentBrushes}}"</c></para>
    /// </summary>
    public sealed class AccentBrushProvider : ObservableObject, IDisposable
    {
        public AccentBrushProvider()
        {
            ApplicationThemeManager.Changed += OnThemeChanged;
            Refresh();
        }

        public void Dispose()
        {
            ApplicationThemeManager.Changed -= OnThemeChanged;
        }

        // ?? Public brush properties ??????????????????????????????????????

        private Brush _cpuAccent = Brushes.Gray;
        public Brush CpuAccent { get => _cpuAccent; private set => SetProperty(ref _cpuAccent, value); }

        private Brush _gpuAccent = Brushes.Gray;
        public Brush GpuAccent { get => _gpuAccent; private set => SetProperty(ref _gpuAccent, value); }

        private Brush _ramAccent = Brushes.Gray;
        public Brush RamAccent { get => _ramAccent; private set => SetProperty(ref _ramAccent, value); }

        private Brush _tempAccent = Brushes.Gray;
        public Brush TempAccent { get => _tempAccent; private set => SetProperty(ref _tempAccent, value); }

        private Brush _gpuTempAccent = Brushes.Gray;
        public Brush GpuTempAccent { get => _gpuTempAccent; private set => SetProperty(ref _gpuTempAccent, value); }

        private Brush _powerAccent = Brushes.Gray;
        public Brush PowerAccent { get => _powerAccent; private set => SetProperty(ref _powerAccent, value); }

        private Brush _gaugeBackground = Brushes.Transparent;
        public Brush GaugeBackground { get => _gaugeBackground; private set => SetProperty(ref _gaugeBackground, value); }

        // ?? Internal ?????????????????????????????????????????????????????

        private void OnThemeChanged(ApplicationTheme currentTheme, Color _)
        {
            Application.Current?.Dispatcher.Invoke(Refresh);
        }

        private void Refresh()
        {
            var suffix = ApplicationThemeManager.GetAppTheme() == ApplicationTheme.Light
                ? ".Light"
                : ".Dark";

            CpuAccent = Resolve("Aquila.Cpu" + suffix);
            GpuAccent = Resolve("Aquila.Gpu" + suffix);
            RamAccent = Resolve("Aquila.Ram" + suffix);
            TempAccent = Resolve("Aquila.Temp" + suffix);
            GpuTempAccent = Resolve("Aquila.GpuTemp" + suffix);
            PowerAccent = Resolve("Aquila.Power" + suffix);
            GaugeBackground = Resolve("Aquila.Gauge.Background" + suffix);

            // If CpuAccent fell back to Gray the source resources are not yet
            // available (too early in App.xaml parsing). Skip publishing so we
            // don't overwrite the XAML layer-3 defaults with gray.
            if (ReferenceEquals(CpuAccent, Brushes.Gray)) return;

            var res = Application.Current?.Resources;
            if (res == null) return;

            res["Aquila.Cpu"] = CpuAccent;
            res["Aquila.Gpu"] = GpuAccent;
            res["Aquila.Ram"] = RamAccent;
            res["Aquila.Temp"] = TempAccent;
            res["Aquila.GpuTemp"] = GpuTempAccent;
            res["Aquila.Power"] = PowerAccent;
            res["Aquila.Gauge.Background"] = GaugeBackground;
            res["Aquila.Critical"] = Resolve("Aquila.Critical" + suffix);

            // Chart series
            res["Aquila.Chart.Cpu"] = Resolve("Aquila.Chart.Cpu" + suffix);
            res["Aquila.Chart.Ram"] = Resolve("Aquila.Chart.Ram" + suffix);
            res["Aquila.Chart.Gpu"] = Resolve("Aquila.Chart.Gpu" + suffix);
            res["Aquila.Chart.NetDown"] = Resolve("Aquila.Chart.NetDown" + suffix);
            res["Aquila.Chart.NetUp"] = Resolve("Aquila.Chart.NetUp" + suffix);
        }

        private static Brush Resolve(string key)
        {
            return Application.Current?.TryFindResource(key) as Brush ?? Brushes.Gray;
        }
    }
}
