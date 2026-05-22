using Aquila.Models;
using Microsoft.Extensions.Logging;
using System.IO;
using System.Text.Json;

namespace Aquila.Services;

public class SettingsService(ILogger<SettingsService> logger)
{
    private static readonly JsonSerializerOptions _json = new() { WriteIndented = true };
    private readonly ILogger<SettingsService> _logger = logger;

    public AppSettings Current { get; private set; } = new();

    public void Load()
    {
        try
        {
            if (!File.Exists(AquilaPaths.Settings)) { Save(); return; }
            Current = JsonSerializer.Deserialize<AppSettings>(
                File.ReadAllText(AquilaPaths.Settings)) ?? new();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load settings, using defaults");
            Current = new();
        }
    }

    public event Action? Changed;

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(AquilaPaths.Root);
            File.WriteAllText(AquilaPaths.Settings,
                JsonSerializer.Serialize(Current, _json));
            Changed?.Invoke();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to save settings");
        }
    }
}
