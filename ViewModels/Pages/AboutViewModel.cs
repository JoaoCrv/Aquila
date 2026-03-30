using System.Reflection;

namespace Aquila.ViewModels.Pages
{
    public partial class AboutViewModel : ObservableObject
    {
        [ObservableProperty]
        private string _appVersion = string.Empty;

        public AboutViewModel()
        {
            _appVersion = GetAssemblyVersion();
        }

        private static string GetAssemblyVersion() =>
            Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? string.Empty;
    }
}
