using Aquila.Extensions;
using Aquila.ViewModels.Pages;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace Aquila.Views.Pages
{
    /// <summary>
    /// Interaction logic for ExplorerPage.xaml
    /// </summary>
    public partial class ExplorerPage : Page
    {

        public ExplorerViewModel ViewModel { get; }
        public ExplorerPage(ExplorerViewModel viewModel)
        {
            ViewModel = viewModel;
            DataContext = this;

            InitializeComponent();
            this.Loaded += OnExplorerPageLoaded;
        }

        private void OnExplorerPageLoaded(object sender, RoutedEventArgs e)
        {
            this.Loaded -= OnExplorerPageLoaded;
            ViewModel.InitializeAsync().SafeFireAndForget(ex => 
            {
                // Handle exceptions here, e.g., log them
                Console.WriteLine($"Error initializing ExplorerPage: {ex.Message}");
            });
        }
    }
}
