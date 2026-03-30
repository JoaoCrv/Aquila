using Aquila.ViewModels.Pages;
using System.Threading.Tasks;
using System.Windows.Threading;
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

            // ActualWidth of DashboardScroll is 0 during the first layout pass (the
            // NavigationView frame hasn't measured yet). Queue a second layout pass
            // at Render priority so the WrapPanel columns get the correct width
            // without waiting for the user to resize the window.
            Loaded += (_, _) => Dispatcher.BeginInvoke(DispatcherPriority.Render, new Action(UpdateLayout));
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
