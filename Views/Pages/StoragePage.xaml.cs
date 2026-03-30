using Aquila.ViewModels.Pages;
using Wpf.Ui.Abstractions.Controls;

namespace Aquila.Views.Pages
{
    public partial class StoragePage : INavigableView<StorageViewModel>
    {
        public StorageViewModel ViewModel { get; }

        public StoragePage(StorageViewModel viewModel)
        {
            ViewModel = viewModel;
            DataContext = this;
            InitializeComponent();
        }
    }
}
