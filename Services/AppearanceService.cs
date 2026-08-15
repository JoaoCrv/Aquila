using System.Windows.Media;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using Wpf.Ui.Appearance;

namespace Aquila.Services;

/// <summary>
/// Owns what the application looks like: how bright the window is, and — through
/// <see cref="ColorProfileService"/> — which colour profile the data is painted with.
///
/// Two ideas, kept apart on purpose. The THEME is Aquila's own look, ours, one per brightness. The
/// PROFILE is what the data wears, and users are expected to mix them: the new theme with the Sky
/// profile is a combination we want to work, and eventually different profiles on different surfaces at
/// once. A profile can therefore never be allowed to dress the window.
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
    ColorProfileService profiles,
    ILogger<AppearanceService> logger)
{
    private const string DarkOverlay = "pack://application:,,,/Themes/Aquila.Dark.xaml";
    private const string LightOverlay = "pack://application:,,,/Themes/Aquila.Light.xaml";

    private ResourceDictionary? _overlay;
    private bool _watching;

    /// <summary>Which half of a profile's palette is in use.</summary>
    public bool IsDark { get; private set; } = true;

    /// <summary>Raised after the theme or profile has been applied, for anything that has to redraw
    /// itself rather than rely on a DynamicResource.</summary>
    public event Action? Changed;

    public void Initialize()
    {
        profiles.Load();

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

    /// <summary>Applies the theme and the profile together. One entry point, so the order — which
    /// matters — lives in one place.</summary>
    public void Apply()
    {
        IsDark = settings.Current.Theme switch
        {
            "Light" => false,
            "Dark" => true,
            _ => !IsSystemLight(),
        };

        var app = Application.Current;
        var applicationTheme = IsDark ? ApplicationTheme.Dark : ApplicationTheme.Light;

        // First, because it rebuilds WPF-UI's merged dictionaries — anything applied before this point
        // would be thrown away. Note it does NOT clear the accent: see the Fluent branch below.
        ApplicationThemeManager.Apply(applicationTheme);

        if (_overlay is not null)
        {
            app.Resources.MergedDictionaries.Remove(_overlay);
            _overlay = null;
        }

        // "Fluent" means the WPF-UI base and nothing else — no overlay, and the user's own Windows accent
        // colour. That is the whole point of offering it.
        if (settings.Current.ThemeStyle == "Fluent")
        {
            // Restoring the accent has to be explicit. ApplicationAccentColorManager writes its brushes
            // into the TOP level of Application.Resources, where they shadow the merged theme dictionary
            // — so once an Aquila accent has been applied, reapplying the base theme can never undo it,
            // and the ember orange would survive in the navigation and every accented control.
            ApplicationAccentColorManager.ApplySystemAccent();
        }
        else
        {
            try
            {
                _overlay = new ResourceDictionary
                {
                    Source = new Uri(IsDark ? DarkOverlay : LightOverlay, UriKind.Absolute),
                };
                app.Resources.MergedDictionaries.Add(_overlay);

                if (_overlay["Aquila.Theme.Accent"] is Color accent)
                    ApplicationAccentColorManager.Apply(accent, applicationTheme, false, false);
            }
            catch (Exception ex)
            {
                // The Fluent base is already applied underneath, so a failure here leaves the window plain
                // rather than broken.
                logger.LogWarning(ex, "The Aquila theme could not be loaded; falling back to the plain base");
                _overlay = null;
            }
        }

        ApplyProfile();
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

    /// <summary>Re-publishes the active profile without touching the theme — for when only the profile
    /// changed.</summary>
    public void ApplyProfile()
    {
        profiles.Apply(settings.Current.ColorProfileId, IsDark);
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
