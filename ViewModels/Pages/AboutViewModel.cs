using System.Diagnostics;
using System.IO;
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

        [RelayCommand]
        private static void OpenLicence() => OpenShipped("LICENSE.txt", $"{RepoUrl}/blob/master/LICENSE");

        [RelayCommand]
        private static void OpenThirdPartyNotices() =>
            OpenShipped("THIRD-PARTY-NOTICES.txt", $"{RepoUrl}/blob/master/THIRD-PARTY-NOTICES.txt");

        /// <summary>Opens a file that ships beside the executable — the licences travel with the binaries, as MIT
        /// and Apache-2.0 ask — or, should it be missing, the same file in the repository.</summary>
        private static void OpenShipped(string fileName, string fallbackUrl)
        {
            var path = Path.Combine(AppContext.BaseDirectory, fileName);
            OpenUrl(File.Exists(path) ? path : fallbackUrl);
        }

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
            "Built on open-source packages, among them LibreHardwareMonitor, WPF-UI and Velopack. Every one of them is listed, with its licence and copyright, under Third-party licences below.";

        public AboutViewModel()
        {
            AppVersion = $"Version {GetAssemblyVersion()}";
        }

        private static string GetAssemblyVersion() =>
            Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? string.Empty;
    }
}
