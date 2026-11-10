using System.Windows;
using System.Windows.Threading;
using Aquila.ViewModels.Pages;
using Wpf.Ui.Abstractions.Controls;

namespace Aquila.Views.Pages
{
    public partial class SettingsPage : INavigableView<SettingsViewModel>
    {
        public SettingsViewModel ViewModel { get; }

        public SettingsPage(SettingsViewModel viewModel)
        {
            ViewModel = viewModel;
            DataContext = this;

            InitializeComponent();

            // Opened for its updates — from an update's notification — the page shows them: the section open and
            // in view, the Install button one click away. Asked on Loaded for a page just navigated to, and by the
            // event for one already showing.
            Loaded += (_, _) =>
            {
                ViewModel.UpdatesRequested += ShowUpdates;
                ShowUpdates();
            };
            Unloaded += (_, _) => ViewModel.UpdatesRequested -= ShowUpdates;
        }

        private void ShowUpdates()
        {
            if (!ViewModel.UpdatesWanted) return;
            ViewModel.UpdatesWanted = false;

            UpdatesCard.IsExpanded = true;

            // After the expander has laid its content out, or the page scrolls to where the section was.
            Dispatcher.BeginInvoke(DispatcherPriority.Loaded, () => UpdatesCard.BringIntoView());
        }
    }
}
