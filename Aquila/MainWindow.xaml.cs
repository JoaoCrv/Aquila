using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Threading;
using Aquila.Services.Utilities;

namespace Aquila
{
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        private readonly DispatcherTimer _timer;
        private readonly HardwareMonitorService _monitor;

        public ObservableCollection<SensorInfo> Sensores { get; set; } = [];

        public MainWindow()
        {
            InitializeComponent();
            DataContext = this;

            _monitor = new HardwareMonitorService();
            _monitor.StartMonitoring();

            _timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _timer.Tick += UpdateHardwareInfo;
            _timer.Start();
            // Replace this line:
            // _aquila = Aquila.Services.Utilities.AppInfo();

            // With this line:
            versionTextBlock.Text = Aquila.Services.Utilities.AppInfo.GetApplicationVersion();
        }

        public void ShowLoadingBar() => loadingBar.Visibility = Visibility.Visible;
        public void HideLoadingBar() => loadingBar.Visibility = Visibility.Collapsed;

        private void UpdateHardwareInfo(object? sender, EventArgs e)
        {
            var novos = _monitor.GetUpdatedSensorReadings();

            Sensores.Clear();
            foreach (var sensor in novos)
                Sensores.Add(sensor);
        }

        // Suporte a INotifyPropertyChanged (para futura expansão)
        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string propertyName) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}