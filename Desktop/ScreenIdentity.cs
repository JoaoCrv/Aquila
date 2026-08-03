using System.Runtime.InteropServices;
using static Aquila.Desktop.NativeMethods;

namespace Aquila.Desktop;

/// <summary>
/// A stable identity for a physical monitor, so a saved widget layout can find the same screen again.
///
/// Never use <c>Screen.DeviceName</c> ("\\.\DISPLAY1") or the position in <c>Screen.AllScreens</c>: both
/// are documented as unstable — they shift when monitors are unplugged, replugged or rearranged, and the
/// order does not match the numbering Windows shows the user in Display Settings.
///
/// The key comes from <c>EnumDisplayDevices</c>'s monitor DeviceID, e.g.
/// <c>MONITOR\DEL41A8\{4d36e96e-...}\0002</c> — it embeds the EDID manufacturer + product code plus the
/// connection instance, which is what makes it stable and what tells two identical monitors apart.
/// Deliberately no WMI (<c>WmiMonitorID</c>): it would only add a friendly name and the EDID serial, at
/// the cost of a System.Management dependency. If a screen picker later wants real monitor names, that's
/// the moment to weigh that cost.
///
/// Resolution is deliberately NOT part of the key: it changes far more often than the monitor does, and
/// baking it in would drop the layout on every resolution change — the exact case #24 wants to survive.
/// </summary>
internal static class ScreenIdentity
{
    public static string KeyFor(System.Windows.Forms.Screen screen)
    {
        var device = new DISPLAY_DEVICE { cb = Marshal.SizeOf<DISPLAY_DEVICE>() };

        // Index 0 = the monitor attached to this display adapter output.
        if (EnumDisplayDevices(screen.DeviceName, 0, ref device, 0) && !string.IsNullOrWhiteSpace(device.DeviceID))
            return device.DeviceID;

        // No monitor info (virtual/remote display, driver quirk). DeviceName is a poor key, but a poor key
        // beats no key — worst case the layout falls back to the primary screen, it never crashes.
        return screen.DeviceName;
    }
}
