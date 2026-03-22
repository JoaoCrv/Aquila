using Aquila.Extensions;
using Aquila.ViewModels.Pages;
using System.Windows;
using System.Windows.Controls;
using Wpf.Ui.Abstractions.Controls;

namespace Aquila.Views.Pages
{
    /// <summary>
    /// Interaction logic for ExplorerPage.xaml
    /// </summary>
    public partial class ExplorerPage : Page, INavigableView<ExplorerViewModel>, INavigationAware
    {
        public ExplorerViewModel ViewModel { get; }

        public ExplorerPage(ExplorerViewModel viewModel)
        {
            ViewModel = viewModel;
            DataContext = this;
            InitializeComponent();
        }

        public Task OnNavigatedToAsync()
        {
            return ViewModel.InitializeAsync();
        }

        public Task OnNavigatedFromAsync() => Task.CompletedTask;
    }
}
