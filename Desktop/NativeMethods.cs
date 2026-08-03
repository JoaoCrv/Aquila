using System.Runtime.InteropServices;
using System.Text;

namespace Aquila.Desktop;

/// <summary>
/// The entire Win32 P/Invoke surface for the desktop-surface feature, deliberately quarantined in one
/// internal file inside the domain-agnostic <see cref="Aquila.Desktop"/> namespace. Nothing here (or
/// anywhere else in this folder) references the Aquila domain (AquilaState/SensorNode/widgets), so the
/// whole folder stays a candidate for extraction into a standalone library — and so a future
/// "place inside Progman" anchor (an alternative <see cref="IDesktopAnchor"/>) can be added without
/// touching the rest of the app.
/// </summary>
internal static class NativeMethods
{
    public static readonly IntPtr HwndBottom = new(1);
    public static readonly IntPtr HwndTop = IntPtr.Zero;

    public const int GwlStyle = -16;
    public const int GwlExStyle = -20;
    public const int GwlpHwndParent = -8;

    public const long WsExToolWindow = 0x00000080;
    public const long WsExNoActivate = 0x08000000;
    public const long WsExTransparent = 0x00000020;
    public const long WsExTopmost = 0x00000008;

    public const uint WsPopup = 0x80000000;
    public const uint WsDisabled = 0x08000000;

    public const uint DwmwaExcludedFromPeek = 12;

    public const uint SwpNoMove = 0x0002;
    public const uint SwpNoSize = 0x0001;
    public const uint SwpNoActivate = 0x0010;
    public const uint SwpNoZOrder = 0x0004;
    public const uint SwpNoOwnerZOrder = 0x0200;
    public const uint SwpNoSendChanging = 0x0400;

    /// <summary>Flags for our OWN z-order moves: SWP_NOSENDCHANGING makes them skip the
    /// WM_WINDOWPOSCHANGING hook ("only I get to move me"), so we never fight our own guard.</summary>
    public const uint OwnMoveFlags = SwpNoMove | SwpNoSize | SwpNoOwnerZOrder | SwpNoActivate | SwpNoSendChanging;

    public const int WmWindowPosChanging = 0x0046;
    public const uint GwHwndPrev = 3;
    public const uint GwHwndNext = 2;

    public const uint EventSystemForeground = 0x0003;
    public const uint WinEventOutOfContext = 0x0000;

    [StructLayout(LayoutKind.Sequential)]
    public struct WINDOWPOS
    {
        public IntPtr hwnd, hwndInsertAfter;
        public int x, y, cx, cy;
        public uint flags;
    }

    public delegate void WinEventDelegate(IntPtr hWinEventHook, uint eventType, IntPtr hwnd, int idObject,
        int idChild, uint dwEventThread, uint dwmsEventTime);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint uFlags);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern IntPtr GetWindow(IntPtr hWnd, uint uCmd);

    [DllImport("user32.dll")]
    public static extern IntPtr GetShellWindow();

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    public static extern IntPtr FindWindowEx(IntPtr hWndParent, IntPtr hWndChildAfter, string? lpszClass, string? lpszWindow);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    public static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
    public static extern IntPtr GetModuleHandle(string lpModuleName);

    [DllImport("kernel32.dll", CharSet = CharSet.Ansi, ExactSpelling = true)]
    public static extern IntPtr GetProcAddress(IntPtr hModule, string procName);

    [DllImport("user32.dll")]
    public static extern IntPtr SetWinEventHook(uint eventMin, uint eventMax, IntPtr hmodWinEventProc,
        WinEventDelegate lpfnWinEventProc, uint idProcess, uint idThread, uint dwFlags);

    [DllImport("user32.dll")]
    public static extern bool UnhookWinEvent(IntPtr hWinEventHook);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    public static extern IntPtr CreateWindowEx(uint dwExStyle, string lpClassName, string? lpWindowName,
        uint dwStyle, int x, int y, int nWidth, int nHeight, IntPtr hWndParent, IntPtr hMenu, IntPtr hInstance, IntPtr lpParam);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool DestroyWindow(IntPtr hWnd);

    [DllImport("dwmapi.dll")]
    public static extern int DwmSetWindowAttribute(IntPtr hwnd, uint attr, [In] ref int attrValue, int attrSize);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct DISPLAY_DEVICE
    {
        public int cb;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string DeviceName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string DeviceString;
        public uint StateFlags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string DeviceID;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string DeviceKey;
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    public static extern bool EnumDisplayDevices(string? lpDevice, uint iDevNum, ref DISPLAY_DEVICE lpDisplayDevice, uint dwFlags);

    public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    /// <summary>
    /// Creates the hidden owner window Rainmeter uses for each on-desktop skin: a disabled, invisible
    /// WS_POPUP tool window. Owning the surface with it keeps the surface off the taskbar/Alt-Tab AND
    /// works around a documented Win11 per-monitor-V2 DPI bug where an ownerless tool window stops
    /// receiving WM_DPICHANGED (Rainmeter's own comment). Uses the predefined "Static" class so no class
    /// registration is needed.
    /// </summary>
    public static IntPtr CreateOwnerWindow() =>
        CreateWindowEx((uint)WsExToolWindow, "Static", null, WsPopup | WsDisabled,
            0, 0, 0, 0, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);

    /// <summary>Excludes the window from Aero Peek / show-desktop preview (DWMWA_EXCLUDED_FROM_PEEK) —
    /// what Rainmeter's IgnoreAeroPeek does.</summary>
    public static void ExcludeFromPeek(IntPtr hWnd)
    {
        var value = 1;
        DwmSetWindowAttribute(hWnd, DwmwaExcludedFromPeek, ref value, sizeof(int));
    }

    public static string ClassNameOf(IntPtr hWnd)
    {
        var sb = new StringBuilder(256);
        GetClassName(hWnd, sb, sb.Capacity);
        return sb.ToString();
    }

    /// <summary>
    /// The window that hosts the desktop icons — Win+D raises this above bottom-most windows, so we key
    /// the "show desktop" counter-dance off it. 24H2+ moved it: detected via a capability check
    /// (GetCurrentMonitorTopologyId only exists from build 26100.2454+, more robust than a version
    /// number), where Progman itself is the host; pre-24H2 it's the WorkerW that contains
    /// SHELLDLL_DefView. Falls back to the shell window if nothing better is found (caller should
    /// tolerate a zero/unexpected result rather than assume).
    /// </summary>
    public static IntPtr FindIconHost()
    {
        var user32 = GetModuleHandle("user32.dll");
        var is24H2Plus = GetProcAddress(user32, "GetCurrentMonitorTopologyId") != IntPtr.Zero;
        var shellWindow = GetShellWindow();

        if (is24H2Plus)
        {
            if (FindWindowEx(shellWindow, IntPtr.Zero, "SHELLDLL_DefView", null) != IntPtr.Zero)
                return shellWindow;
        }
        else
        {
            var found = IntPtr.Zero;
            EnumWindows((hwnd, _) =>
            {
                if (ClassNameOf(hwnd) == "WorkerW" &&
                    FindWindowEx(hwnd, IntPtr.Zero, "SHELLDLL_DefView", null) != IntPtr.Zero)
                {
                    found = hwnd;
                    return false;
                }
                return true;
            }, IntPtr.Zero);
            if (found != IntPtr.Zero) return found;
        }

        return shellWindow;
    }
}
