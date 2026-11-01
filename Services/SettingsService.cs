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

            var text = File.ReadAllText(AquilaPaths.Settings);
            Current = JsonSerializer.Deserialize<AppSettings>(text) ?? new();

            Migrate(text);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load settings, using defaults");
            Current = new();
        }
    }

    private const int CurrentVersion = 3;

    /// <summary>
    /// One-time fix-ups, guarded by <see cref="AppSettings.SettingsVersion"/> so each runs exactly once.
    /// The guard is the whole point: a fix-up keyed on the state it repairs cannot tell that state from
    /// a later choice by the user, and would keep undoing it.
    /// </summary>
    private void Migrate(string text)
    {
        if (Current.SettingsVersion >= CurrentVersion) return;

        // v1 — starting hidden used to be implied by dashboard mode rather than stored. Startup now
        // reads StartMinimized alone, so write down what an appliance install was already relying on.
        if (Current.SettingsVersion < 1 && Current.DashboardMode)
            Current.StartMinimized = true;

        // v2 — colour profiles became presets. The ids are the same on both sides (ember, classic, sky,
        // signal), so the choice carries straight over — but it has to be read from the raw JSON, because
        // AppSettings no longer has a ColorProfileId to deserialize into.
        if (Current.SettingsVersion < 2)
        {
            try
            {
                using var document = JsonDocument.Parse(text);
                if (document.RootElement.TryGetProperty("ColorProfileId", out var chosen) &&
                    chosen.GetString() is { Length: > 0 } id)
                    Current.DashboardPresetId = id;
            }
            catch (Exception ex)
            {
                // Not worth failing a launch over: the default preset is a fine place to land.
                _logger.LogWarning(ex, "The old colour profile could not be carried over");
            }
        }

        // v3 — the motion steps were spread out (2026-11-01): Subtle 150 → 300, Smooth 300 → 600, Relaxed
        // 400 → 900. The file stores the step's milliseconds, not its name, so a Smooth chosen in 2.4.0 would
        // come up as the new Subtle. Carried over step by step; a number no step had — written by hand — is
        // left as it is.
        if (Current.SettingsVersion < 3)
            Current.AnimationSpeedMs = Current.AnimationSpeedMs switch
            {
                150 => 300,
                300 => 600,
                400 => 900,
                var other => other,
            };

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
