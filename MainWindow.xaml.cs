using Aquila.Services.Utilities;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace Aquila
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            versionTextBlock.Text = AppInfo.GetApplicationVersion();
            HardwareMonitorService _hardwareMonitor = new HardwareMonitorService();
            _hardwareMonitor.StartMonitoring();
            txtHardwareInfo.Text = _hardwareMonitor.listSensors();
        }

        private void txtHardwareInfo_Scroll(object sender, System.Windows.Controls.Primitives.ScrollEventArgs e)
        {

        }
    }
}