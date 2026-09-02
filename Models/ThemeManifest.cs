using System.Text.Json.Serialization;

namespace Aquila.Models;

/// <summary>
/// What a theme is, as its folder declares it.
///
/// A theme is values, not rules — the rules live once in <c>Themes/Controls.xaml</c> and are shared by
/// every theme, Fluent included. What a folder brings is a token file per side and this, which says what
/// to call the thing and who made it.
///
/// It exists even for a theme with nothing else to say: the name has to come from somewhere, and a folder
/// holding two XAML files has nowhere to put one. Before this, "Aquila" was written into the code.
/// </summary>
public sealed class ThemeManifest
{
    /// <summary>Folder name by convention, and what <c>AppSettings.ThemeStyle</c> stores.</summary>
    [JsonIgnore]
    public string Id { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;
    public string? Author { get; set; }
    public string? Description { get; set; }

    /// <summary>
    /// A preset that goes with this theme, by id. The single link between the two axes, and it runs one
    /// way only: choosing a theme may OFFER to dress the widgets to match, while a preset never suggests
    /// a theme — a theme is chosen once, in settings, and a preset is chosen per widget, so the reverse
    /// would ask "change the whole app?" every time somebody dressed a gauge.
    ///
    /// Never imposed, and silently ignored when the preset is not installed.
    /// </summary>
    public string? SuggestedPreset { get; set; }

    /// <summary>
    /// Which sides the theme ships.
    ///
    /// Declared here rather than discovered from which files exist. Both were on the table: reading the
    /// compiled resource table means a theme cannot lie about itself, but it costs the most intricate code
    /// in the catalogue, and the lie is already handled — a side that will not load is caught where the
    /// overlay is merged, which logs it and leaves the app plain rather than broken. One place to look
    /// beat one place to be wrong.
    /// </summary>
    public bool Dark { get; set; } = true;

    public bool Light { get; set; }

    /// <summary>A theme with one side pins the app to it: a dark-only theme is an honest answer, where a
    /// light side written out of obligation is worse than none.</summary>
    [JsonIgnore]
    public bool HasBothSides => Dark && Light;
}
