using Aquila.Models;
using System.IO;
using System.Text.Json;

namespace Aquila.Services;

public class SettingsService
{
    private static readonly JsonSerializerOptions _json = new() { WriteIndented = true };

    public AppSettings Current { get; private set; } = new();

    public void Load()
    {
        try
        {
            if (!File.Exists(AquilaPaths.Settings)) { Save(); return; }
            Current = JsonSerializer.Deserialize<AppSettings>(
                File.ReadAllText(AquilaPaths.Settings)) ?? new();
        }
        catch { Current = new(); }
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(AquilaPaths.Root);
            File.WriteAllText(AquilaPaths.Settings,
                JsonSerializer.Serialize(Current, _json));
        }
        catch { }
    }
}
