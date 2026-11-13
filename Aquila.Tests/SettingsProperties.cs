using Aquila.Models;
using Aquila.Services;
using Xunit;

namespace Aquila.Tests;

/// <summary>
/// The settings file (<see cref="SettingsService.Write"/> and <see cref="SettingsService.Read"/>). A fresh install's
/// settings hold NaN for every window's place — none has one yet — and the default JSON options refused to write it,
/// so on a clean machine no setting was ever saved until a window had been moved (2026-11-13, a clean VM).
/// </summary>
public sealed class SettingsProperties
{
    [Fact]
    public void A_fresh_install_s_settings_are_written_and_read_back()
    {
        var fresh = new AppSettings();

        var written = SettingsService.Write(fresh);
        var again = SettingsService.Read(written);

        Assert.True(double.IsNaN(again.WindowLeft) && double.IsNaN(again.EditorPanelTop));
        Assert.Equal(written, SettingsService.Write(again));
    }

    /// <summary>A file written before — numbers where the windows were — reads as it always did.</summary>
    [Fact]
    public void A_settings_file_with_numbers_reads_as_before()
    {
        var settings = SettingsService.Read("""{ "WindowLeft": 120, "WindowTop": -8.5, "PollingIntervalMs": 2000 }""");

        Assert.Equal((120d, -8.5d, 2000), (settings.WindowLeft, settings.WindowTop, settings.PollingIntervalMs));
    }
}
