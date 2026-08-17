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
            if (!File.Exists(AquilaPaths.Settings))
            {
                // A fresh install has nothing to migrate — stamp it current so it never tries.
                Current.SettingsVersion = CurrentVersion;
                Save();
                return;
            }

            Current = JsonSerializer.Deserialize<AppSettings>(
                File.ReadAllText(AquilaPaths.Settings)) ?? new();

            Migrate();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load settings, using defaults");
            Current = new();
        }
    }

    private const int CurrentVersion = 1;

    /// <summary>
    /// One-time fix-ups, guarded by <see cref="AppSettings.SettingsVersion"/> so each runs exactly once.
    /// The guard is the whole point: a fix-up keyed on the state it repairs cannot tell that state from
    /// a later choice by the user, and would keep undoing it.
    /// </summary>
    private void Migrate()
    {
        if (Current.SettingsVersion >= CurrentVersion) return;

        // v1 — starting hidden used to be implied by dashboard mode rather than stored. Startup now
        // reads StartMinimized alone, so write down what an appliance install was already relying on.
        if (Current.SettingsVersion < 1 && Current.DashboardMode)
            Current.StartMinimized = true;

        Current.SettingsVersion = CurrentVersion;
        Save();
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
