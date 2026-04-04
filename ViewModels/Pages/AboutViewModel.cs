using System.Reflection;

namespace Aquila.ViewModels.Pages
{
    public partial class AboutViewModel : ObservableObject
    {
        [ObservableProperty]
        private string _appVersion = string.Empty;

        public string ProjectSummary =>
            "Aquila is a free and open-source Windows hardware monitoring app built for clean secondary-screen dashboards.";

        public string Maintainer => "@JoaoCrv";

        public string RepositoryUrl => "https://github.com/JoaoCrv/Aquila";

        public string LicenseName => "Mozilla Public License 2.0 (MPL-2.0)";

        public string LicenseSummary =>
            "Aquila is distributed under MPL-2.0, a file-level copyleft license that keeps improvements open while remaining friendly to contributors and forks.";

        public string PrivacySummary =>
            "Aquila does not show ads, does not include telemetry, and does not collect personal data. The only intended internet communication is the optional update flow through Velopack.";

        public string SupportSummary =>
            "The app is free to use. Optional donations may support continued development, but there are no paid features or advertising.";

        public string DependenciesSummary =>
            "Built with open-source projects including LibreHardwareMonitor, WPF-UI, and Velopack. See the README for the full dependency list and links.";

        public AboutViewModel()
        {
            AppVersion = $"Version {GetAssemblyVersion()}";
        }

        private static string GetAssemblyVersion() =>
            Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? string.Empty;
    }
}
