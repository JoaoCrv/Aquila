using Aquila.Extensions;
using Aquila.ViewModels.Pages;
using System.Windows;
using System.Windows.Controls;

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
