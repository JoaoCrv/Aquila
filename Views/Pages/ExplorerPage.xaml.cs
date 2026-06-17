using Aquila.ViewModels.Pages;
using System.Threading.Tasks;
using System.Windows.Controls;
using Wpf.Ui.Abstractions.Controls;

namespace Aquila.Views.Pages
{
    public partial class ExplorerPage : Page, INavigableView<ExplorerViewModel>, INavigationAware
    {
        public ExplorerViewModel ViewModel { get; }

        public ExplorerPage(ExplorerViewModel viewModel)
        {
            ViewModel = viewModel;
            DataContext = this;
            InitializeComponent();
        }

        // Rebuild the list when shown, so sensors are populated (the VM may be constructed before
        // the first poll tick).
        public Task OnNavigatedToAsync()
        {
            ViewModel.Refresh();
            return Task.CompletedTask;
        }

        public Task OnNavigatedFromAsync() => Task.CompletedTask;
    }
}
