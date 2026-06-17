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
    public partial class LhmExplorerPage : Page, INavigableView<LhmExplorerViewModel>//, INavigationAware
    {
        public LhmExplorerViewModel ViewModel { get; }

        public LhmExplorerPage(LhmExplorerViewModel viewModel)
        {
            ViewModel = viewModel;
            DataContext = this;
            InitializeComponent();
        }

        //public Task OnNavigatedToAsync()
        //{
        //    return ViewModel.InitializeAsync();
        //}

        public Task OnNavigatedFromAsync() => Task.CompletedTask;
    }
}
