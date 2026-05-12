using System.IO;

namespace Aquila.Services;

public static class AquilaPaths
{
    public static string Root     => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Aquila");

    public static string Settings => Path.Combine(Root, "settings.json");
    public static string Logs     => Path.Combine(Root, "logs");
    public static string Themes   => Path.Combine(Root, "themes");
}
