using Aquila.ViewModels.Pages;
using Wpf.Ui.Abstractions.Controls;

namespace Aquila.Views.Pages
{
    public partial class WidgetsPage : INavigableView<WidgetsViewModel>
    {
        public WidgetsViewModel ViewModel { get; }

        public WidgetsPage(WidgetsViewModel viewModel)
        {
            ViewModel = viewModel;
            DataContext = this;
            InitializeComponent();
        }
    }
}
