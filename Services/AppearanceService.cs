using System.Windows.Media;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

namespace Aquila.Services;

/// <summary>
/// Owns what the application looks like: how bright the window is, which theme dresses it, and — through
/// <see cref="PresetService"/> — which preset the app's own surfaces paint their data with.
///
/// Two axes, kept apart on purpose. The THEME is the application: backgrounds, lettering, spacing, its own
/// semantic colours. The PRESET is what the data wears, and the two are meant to be mixed — widgets to
/// match a wallpaper over the stock shell is the case the separation exists for. A preset therefore never
/// dresses the window, and a theme never decides what a reading looks like.
///
/// The two theme settings are kept orthogonal — STYLE (Aquila or plain Fluent) and BRIGHTNESS (Light,
/// Dark, or follow Windows) — rather than enumerated as one list. Two controls beat four entries plus an
/// ambiguous "System", and adding a third style later costs one entry instead of three.
///
/// Named for both axes rather than for the theme, which also avoids colliding with WPF-UI's own
/// <c>Wpf.Ui.ThemeService</c>.
/// </summary>
public sealed class AppearanceService(
    SettingsService settings,
    PresetService presets,
    ThemeCatalog themes,
    ILogger<AppearanceService> logger)
{

    private ResourceDictionary? _overlay;
    private bool _watching;

    /// <summary>Which half of a profile's palette is in use.</summary>
    public bool IsDark { get; private set; } = true;

    /// <summary>Raised after the theme or profile has been applied, for anything that has to redraw
    /// itself rather than rely on a DynamicResource.</summary>
    public event Action? Changed;

    public void Initialize()
    {
        themes.Load();

        // Loaded here, beside the themes, and not by the caller: this service PUBLISHES a preset, so it
        // has to be the thing that makes sure there is one. When the caller owned the ordering, Publish
        // ran twelve lines before Load and painted everything in the fallback's blue while the settings
        // page — built later — correctly showed the chosen preset. The list said one thing and the screen
        // another.
        presets.Load();

        if (!_watching)
        {
            // Watching the OS switch ourselves, rather than through WPF-UI's SystemThemeWatcher, is
            // deliberate: that helper applies plain Fluent light or dark directly, which would strip the
            // Aquila overlay off the window every time Windows changed.
            SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
            _watching = true;
        }

        Apply();
    }

    public void Shutdown()
    {
        if (!_watching) return;
        SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
        _watching = false;
    }

    /// <summary>
    /// Applies the theme and the profile. Five steps, and the ORDER is the whole trick — see step 4.
    /// </summary>
    public void Apply()
    {
        IsDark = settings.Current.Theme switch
        {
            "Light" => false,
            "Dark" => true,
            _ => !IsSystemLight(),
        };

        var theme = IsDark ? ApplicationTheme.Dark : ApplicationTheme.Light;

        ApplicationThemeManager.Apply(theme);   // 1. WPF-UI's base look
        SwapOverlay();                          // 2. our theme file on top, or none under Fluent
        ApplyAccent();                          // 3. our accent, or the system's

        // 4. Again — and this is the part that is not obvious. Step 1 swaps WPF-UI's dictionaries, and
        //    every control re-reads its brushes at that instant, while the accent is still the previous
        //    theme's. Step 3 then writes the new one correctly, but nothing looks again, so the rail kept
        //    the colour it had already taken. Startup escaped it only because no window existed yet.
        //    updateAccent: false, or this pass would overwrite what it is here to publish.
        ApplicationThemeManager.Apply(theme, WindowBackdropType.Mica, updateAccent: false);

        ApplyProfile();                         // 5. the surface preset's colours, for the data
    }

    /// <summary>Puts our theme file over WPF-UI's, or takes it off. Fluent gets nothing of ours.</summary>
    private void SwapOverlay()
    {
        var merged = Application.Current.Resources.MergedDictionaries;

        if (_overlay is not null)
        {
            merged.Remove(_overlay);
            _overlay = null;
        }

        // Null for Fluent, and for a theme that is no longer installed — a layout naming a theme that
        // has gone should leave the app plain rather than refuse to draw.
        if (themes.Overlay(settings.Current.ThemeStyle, IsDark) is not { } source) return;

        try
        {
            _overlay = new ResourceDictionary { Source = new Uri(source, UriKind.Absolute) };
            merged.Add(_overlay);
        }
        catch (Exception ex)
        {
            // The Fluent base is already applied underneath, so a failure here leaves the window plain
            // rather than broken.
            logger.LogWarning(ex, "The Aquila theme could not be loaded; falling back to the plain base");
            _overlay = null;
        }
    }

    /// <summary>
    /// The accent, stated rather than derived.
    ///
    /// No overlay means Fluent — or a theme file that failed to load — and both want the user's own
    /// Windows accent, so one condition covers both. Otherwise the same colour is given four times, on
    /// purpose: the overload that takes a theme instead treats the colour as a base and works the rest
    /// out, lightening for dark and darkening for light. That is why one accent came out pale amber in
    /// dark and deep orange in light, and why changing it barely moved anything — the derivation was
    /// overruling the theme file.
    /// </summary>
    private void ApplyAccent()
    {
        if (_overlay?["Aquila.Theme.Accent"] is Color accent)
            ApplicationAccentColorManager.Apply(accent, accent, accent, accent);
        else
            ApplicationAccentColorManager.ApplySystemAccent();
    }

    /// <summary>
    /// Flips between light and dark and pins the result.
    ///
    /// Deliberately leaves "System" behind rather than cycling back into it: someone reaching for the
    /// toggle wants this brightness now, and a three-way cycle would make the next click's outcome
    /// depend on what Windows happens to be set to. Following the system again is a deliberate choice,
    /// and it stays in Settings where deliberate choices live.
    /// </summary>
    public void ToggleBrightness()
    {
        settings.Current.Theme = IsDark ? "Light" : "Dark";
        settings.Save();
        Apply();
    }

    /// <summary>Re-publishes the surface preset without touching the theme — for when only the preset
    /// changed.</summary>
    public void ApplyProfile()
    {
        presets.Publish(settings.Current.DashboardPresetId);
        Changed?.Invoke();
    }

    private void OnUserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
    {
        if (e.Category != UserPreferenceCategory.General) return;
        if (settings.Current.Theme != "System") return;

        // Raised off the UI thread, and more than once for a single toggle — Apply is idempotent, so the
        // repeats cost nothing but a redraw.
        Application.Current?.Dispatcher.BeginInvoke(Apply);
    }

    /// <summary>Reads the setting the user actually toggles in Windows. Preferred over mapping WPF-UI's
    /// richer SystemTheme enum, where entries like Glow or Sunrise have no unambiguous light/dark
    /// answer.</summary>
    private static bool IsSystemLight()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return key?.GetValue("AppsUseLightTheme") is int value && value != 0;
        }
        catch
        {
            return false;
        }
    }
}
