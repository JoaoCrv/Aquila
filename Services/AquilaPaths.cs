using System.IO;

namespace Aquila.Services;

public static class AquilaPaths
{
    public static string Root     => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Aquila");

    public static string Settings => Path.Combine(Root, "settings.json");
    public static string Widgets  => Path.Combine(Root, "widgets.json");
    public static string Logs     => Path.Combine(Root, "logs");

    /// <summary>Where the user's own colour profiles live, one JSON file each. Themes are not here on
    /// purpose: those ship inside the app, while profiles are meant to be copied, edited and shared.</summary>

    /// <summary>The user's own presets, one JSON file each. Same reasoning as profiles, which these
    /// replace: a preset is meant to be copied, edited and sent to someone else.</summary>
    public static string Presets => Path.Combine(Root, "presets");
}
