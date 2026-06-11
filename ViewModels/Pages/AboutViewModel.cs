using System.Diagnostics;
using System.Reflection;
using CommunityToolkit.Mvvm.Input;

namespace Aquila.ViewModels.Pages
{
    public partial class AboutViewModel : ObservableObject
    {
        // External links. Sponsor is GitHub Sponsors (Stripe + tiers); PayPal is the alternative for
        // one-off donations without a GitHub account. Contact is a web form (no email exposed).
        private const string SponsorUrl     = "https://github.com/sponsors/JoaoCrv";
        private const string PayPalUrl      = "https://paypal.me/joaocrv";
        private const string ContactFormUrl = "https://tally.so/r/gDzXEP";
        private const string RepoUrl        = "https://github.com/JoaoCrv/Aquila";

        [ObservableProperty]
        private string _appVersion = string.Empty;

        [RelayCommand]
        private static void OpenSponsor() => OpenUrl(SponsorUrl);

        [RelayCommand]
        private static void OpenPayPal() => OpenUrl(PayPalUrl);

        [RelayCommand]
        private static void OpenContact() => OpenUrl(ContactFormUrl);

        [RelayCommand]
        private static void OpenRepository() => OpenUrl(RepoUrl);

        private static void OpenUrl(string url)
        {
            try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); }
            catch { /* no browser available — ignore */ }
        }

        public string ProjectSummary =>
            "Aquila is a free and open-source Windows hardware monitoring app built for clean secondary-screen dashboards.";

        public string Maintainer => "@JoaoCrv";

        public string RepositoryUrl => "https://github.com/JoaoCrv/Aquila";

        public string LicenseName => "Mozilla Public License 2.0 (MPL-2.0)";

        public string LicenseSummary =>
            "Aquila is distributed under MPL-2.0, a file-level copyleft license that keeps improvements open while remaining friendly to contributors and forks.";

        public string PrivacySummary =>
            "Aquila does not show ads, does not include telemetry, and does not collect personal data. The only intended internet communication is the optional update flow through Velopack.";

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
