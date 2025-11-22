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

            //versionTextBlock.Text = Aquila.Services.Utilities.AppInfo.GetApplicationVersion();
        }

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