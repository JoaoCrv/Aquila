using Aquila.ViewModels.Pages;
using System.Threading.Tasks;
using Wpf.Ui.Abstractions.Controls;

namespace Aquila.Views.Pages
{
    public partial class DashboardPage : INavigableView<DashboardViewModel>, INavigationAware
    {
        public DashboardViewModel ViewModel { get; }

        public DashboardPage(DashboardViewModel viewModel)
        {
            ViewModel = viewModel;
            DataContext = this;

            InitializeComponent();
        }

        public Task OnNavigatedToAsync()
        {
            ViewModel.Resume();
            return Task.CompletedTask;
        }

        public Task OnNavigatedFromAsync()
        {
            ViewModel.Suspend();
            return Task.CompletedTask;
        }
    }
}
