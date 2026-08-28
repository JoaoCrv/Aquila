using System.Threading.Tasks;
using Aquila.ViewModels.Pages;
using Wpf.Ui.Abstractions.Controls;

namespace Aquila.Views.Pages
{
    public partial class WidgetsPage : INavigableView<WidgetsViewModel>, INavigationAware
    {
        public WidgetsViewModel ViewModel { get; }

        public WidgetsPage(WidgetsViewModel viewModel)
        {
            ViewModel = viewModel;
            DataContext = this;
            InitializeComponent();
        }

        // Rebuilt when shown, so the sensors are populated: the view model may be constructed before the
        // first poll tick, and a list built then would call every reading missing.
        public Task OnNavigatedToAsync()
        {
            ViewModel.RefreshWidgets();
            return Task.CompletedTask;
        }

        public Task OnNavigatedFromAsync() => Task.CompletedTask;
    }
}
