namespace Aquila.Models;

public class AppSettings
{
    /// <summary>Schema version of this file, so a one-time fix-up can tell "already applied" from
    /// "the user has since changed their mind". Without it a migration keyed on the state it repairs
    /// re-applies itself on every launch, and overwrites the very choice it was meant to preserve.</summary>
    public int    SettingsVersion   { get; set; } = 0;

    /// <summary>"Light", "Dark", or "System" to follow Windows. Brightness only — kept separate from
    /// <see cref="ThemeStyle"/> so "System" keeps meaning exactly one thing, and so the two never have to
    /// be enumerated as a combinatorial list.</summary>
    public string Theme             { get; set; } = "System";

    /// <summary>"Aquila" for our own look, "Fluent" for the untouched WPF-UI base — for anyone who would
    /// rather the app matched the rest of Windows, including its accent colour.</summary>
    public string ThemeStyle        { get; set; } = "Aquila";

    /// <summary>Id of the active colour profile. Independent of the theme: one dresses the data, the
    /// other the window, and the user is expected to mix them freely.</summary>
    /// <summary>The preset the app's own surfaces are drawn in — the dashboard's cards, the pages, the
    /// pressure pills. Replaces ColorProfileId, which named the same thing in the format presets have
    /// since replaced.</summary>
    public string DashboardPresetId { get; set; } = "ember";

    /// <summary>The preset a widget wears when it names none, and what everything falls back to when the
    /// one it names is gone. Stored rather than compiled in, so "make this my default" is a thing a user
    /// can say.</summary>
    public string DefaultPresetId   { get; set; } = "ember";

    public int    PollingIntervalMs { get; set; } = 1000;
    public bool   MinimizeToTray   { get; set; } = false;
    public bool   StartMinimized   { get; set; } = false;
    public double WindowLeft       { get; set; } = double.NaN;
    public double WindowTop        { get; set; } = double.NaN;
    public double WindowWidth      { get; set; } = 1600;
    public double WindowHeight     { get; set; } = 900;
    public bool   WindowMaximized  { get; set; } = false;
    public bool   DashboardMode         { get; set; } = false;
    public bool   EnableVerboseLogging  { get; set; } = false;

    /// <summary>
    /// Limits the user has changed, by family name — "Temperature" to "75,85,95".
    ///
    /// Only what was changed is stored, so a family absent here still follows the built-in preset and keeps
    /// following it if that preset is ever revised. Written as text rather than as an object so the file
    /// stays legible and hand-editable, which is the same reason widgets.json stores enum names.
    /// </summary>
    public Dictionary<string, string> Thresholds { get; set; } = [];

    public bool ShowCpuCard          { get; set; } = true;
    public bool ShowMemoryCard       { get; set; } = true;
    public bool ShowNetworkCard      { get; set; } = true;
    public bool ShowTemperaturesCard { get; set; } = true;
    public bool ShowPowerCard        { get; set; } = true;
    public bool ShowFansCard         { get; set; } = true;
    public bool ShowGpuCard          { get; set; } = true;
    public bool ShowStorageCard      { get; set; } = true;

    public double DashboardWindowLeft   { get; set; } = double.NaN;
    public double DashboardWindowTop    { get; set; } = double.NaN;
    public double DashboardWindowWidth  { get; set; } = 900;
    public double DashboardWindowHeight { get; set; } = 600;

    // Desktop widgets (#24). Off by default while the feature is built out. The widget layout itself
    // lives in widgets.json, not here — it's a document, not a preference.
    public bool DesktopSurfaceEnabled { get; set; } = false;
}
