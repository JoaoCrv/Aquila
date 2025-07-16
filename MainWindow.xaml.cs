using Aquila.Services.Utilities;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace Aquila
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private DispatcherTimer _timer;
        private HardwareMonitorService _monitor;

        public MainWindow()
        {
            InitializeComponent();
            _monitor = new HardwareMonitorService();
            _monitor.StartMonitoring();

            _timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _timer.Tick += UpdateHardwareInfo;
            _timer.Start();
        }

        private void UpdateHardwareInfo(object? sender, EventArgs e)
        {
            var readings = _monitor.GetUpdatedSensorReadings();
            txtHardwareInfo.Text = string.Join("\n", readings.Select(r => r.ToString()));
        }
    }
}