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
    public sealed class AccentBrushProvider : ObservableObject
    {
        public static AccentBrushProvider Instance { get; } = new();

        public AccentBrushProvider()
        {
            ApplicationThemeManager.Changed += OnThemeChanged;
            Refresh();
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

            CpuAccent        = Resolve("CpuAccentBrush" + suffix);
            GpuAccent        = Resolve("GpuAccentBrush" + suffix);
            RamAccent        = Resolve("RamAccentBrush" + suffix);
            TempAccent       = Resolve("TempAccentBrush" + suffix);
            GpuTempAccent    = Resolve("GpuTempAccentBrush" + suffix);
            PowerAccent      = Resolve("PowerAccentBrush" + suffix);
            GaugeBackground  = Resolve("GaugeBackgroundBrush" + suffix);
        }

        private static Brush Resolve(string key)
        {
            return Application.Current?.TryFindResource(key) as Brush ?? Brushes.Gray;
        }
    }
}
