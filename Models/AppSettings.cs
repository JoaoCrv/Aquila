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

    /// <summary>The preset the app's own surfaces are drawn in — the dashboard's cards, the pages, the
    /// pressure pills. Independent of the theme: one dresses the data, the other the window, and the user
    /// is expected to mix them freely. Replaces ColorProfileId, which named the same thing in the format
    /// presets have since replaced.</summary>
    public string DashboardPresetId { get; set; } = "ember";

    /// <summary>The preset a widget wears when it names none, and what everything falls back to when the
    /// one it names is gone. Stored rather than compiled in, so "make this my default" is a thing a user
    /// can say.</summary>
    public string DefaultPresetId   { get; set; } = "ember";

    public int    PollingIntervalMs { get; set; } = 1000;

    /// <summary>How long a reading takes to travel to its new value. Zero is no animation — the duration
    /// IS the switch, following the charting library's own convention, so there is no second field beside
    /// it that could say otherwise.
    ///
    /// 600 ms — Smooth — and not zero. With a one-second poll the reading has arrived with 400 ms left to
    /// show it at rest; Relaxed's 900 ms leaves a tenth of the second, so the slower one is a choice and not
    /// the default.</summary>
    public int    AnimationSpeedMs  { get; set; } = 600;

    /// <summary>The most frames a second anything moving may draw — dials, bars, the clock's rings. 15 to 60.
    ///
    /// Thirty and not sixty. This is a monitor: it draws for hours in the corner of a screen, and every frame
    /// it spends is taken from the machine it is measuring. Half the frames are not visible in a 300 ms sweep
    /// and are plainly visible in a task manager. Beside the speed, and a setting rather than a constant,
    /// because what a frame is worth depends on the machine paying for it.</summary>
    public int    MaxFrameRate      { get; set; } = 30;

    /// <summary>Whether a chart's line slides steadily left between readings, rather than standing still until the next
    /// one moves it on a step. Off by default: the one movement that never stops, so it is drawn at the frame rate for
    /// as long as a chart is on screen. Needs motion on — with motion off nothing moves.</summary>
    public bool   SlideCharts       { get; set; } = false;

    /// <summary>Whether WPF draws with the graphics card instead of the processor. Taken once, at start.
    ///
    /// Off by default (#50, measured 2026-10-22). Drawing with the card costs ~80 MB before a single window shows
    /// anything (an empty WPF window: 90 MB against 12), and more per window: in the tray with the widgets 340 MB
    /// against 138, with the main window open 460 against 190. Software costs processor in proportion to the frames
    /// drawn: at rest about a point of one core at 30 fps and five at 60 (the clock's rings), with the main window
    /// maximized eight at 60. Nothing Aquila draws looked any different.</summary>
    public bool   DrawWithGraphicsCard { get; set; } = false;
    public bool   MinimizeToTray   { get; set; } = false;
    public bool   StartMinimized   { get; set; } = false;
    public double WindowLeft       { get; set; } = double.NaN;
    public double WindowTop        { get; set; } = double.NaN;
    public double WindowWidth      { get; set; } = 1600;
    public double WindowHeight     { get; set; } = 900;
    public bool   WindowMaximized  { get; set; } = false;
    public bool   DashboardMode         { get; set; } = false;
    public bool   EnableVerboseLogging  { get; set; } = false;

    /// <summary>Whether Aquila has offered to install PawnIO. Offered once: "Not now" is an answer, and Settings
    /// keeps the button. See PawnIoService.</summary>
    public bool   PawnIoOffered         { get; set; } = false;

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

    /// <summary>Whether the edit bar's first tip — click a widget, drag it, pull a corner — has been seen: closed,
    /// or made unnecessary by a first selection. Shown once, ever; after that the gestures are known.</summary>
    public bool EditTipSeen { get; set; } = false;

    /// <summary>Where the editor panel was dragged to — the screen's key and its position in DIPs — or, with the
    /// position NaN, nowhere: docked on the primary screen. Kept across sessions, since a panel placed by hand never
    /// jumps on its own; a double-click on its header docks it again.</summary>
    public string? EditorPanelScreen { get; set; }
    public double EditorPanelLeft { get; set; } = double.NaN;
    public double EditorPanelTop { get; set; } = double.NaN;
}
