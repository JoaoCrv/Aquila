
using System.Windows;
using Aquila.ViewModels;

namespace Aquila
{
    public partial class MainWindow : Window
    {
        
        public MainWindow()
        {
            InitializeComponent();

            DataContext = new MainWindowViewModel();

        }
    }
}