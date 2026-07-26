using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using static Aquila.Desktop.NativeMethods;

namespace Aquila.Desktop;

/// <summary>
/// The validated desktop-anchoring model (Rainmeter's "on desktop"), reimplemented from reading their
/// public GPL source (System.cpp / Skin.cpp) — the Win32 call sequence isn't copyrightable, only their
/// expression of it is; nothing here is copied code. A normal top-level layered window is kept at the
/// bottom of the z-order; no SetParent, no WorkerW/Progman embedding (both were proven not to render
/// transparent WPF content on current Windows builds — see issue #24 for the full investigation).
///
/// Win+D does NOT minimize such a window — the shell RAISES the desktop (the icon host) above it, so it
/// gets covered, not minimized. Everything that treats this as a minimize (restoring on SIZE_MINIMIZED,
/// forcing WindowState back to Normal) is therefore a no-op; Rainmeter itself contains no minimize
/// handling at all. What actually works is an active counter-dance:
///  1. Detect the raised desktop by STATE, not by guessing from an event: is the sentinel window now
///     BELOW the icon host in the z-order? (Rainmeter's CheckDesktopState.) The sentinel must be a window
///     that never moves — using the surface itself makes the answer flip the moment the counter-dance
///     raises it, oscillating at the poll interval and flickering constantly.
///  2. Poll that state on a 250 ms timer (Rainmeter's TIMER_SHOWDESKTOP), plus an EVENT_SYSTEM_FOREGROUND
///     hook (out-of-process, no DLL injection) for a fast reaction when show-desktop starts. The event
///     alone is not enough: it catches entering show-desktop, the timer is what notices it ending.
///  3. While raised, sit just behind the first genuine WS_EX_TOPMOST window (above the raised desktop,
///     below real topmost windows); when it descends, drop back to HWND_BOTTOM.
///  4. At rest, keep the window at the bottom by rewriting external z-order changes back to HWND_BOTTOM in
///     WM_WINDOWPOSCHANGING (gated on !_desktopRaised so it doesn't fight the counter-dance). Our own
///     moves carry SWP_NOSENDCHANGING, so they never re-enter this hook. Rainmeter instead FREEZES the
///     z-order (SWP_NOZORDER) — worth revisiting, but force-to-bottom is what's verified working here.
///
/// A hidden owner window + DWMWA_EXCLUDED_FROM_PEEK are also applied, mirroring Rainmeter. They were
/// added while chasing the (mistaken) minimize theory and kept because the owner doubles as the sentinel.
/// </summary>
internal sealed class ZOrderBottomAnchor : IDesktopAnchor
{
    private IntPtr _hwnd;
    private IntPtr _ownerHwnd;
    private HwndSource? _source;
    private IntPtr _iconHost;
    private bool _desktopRaised;
    private bool _disposed;

    private WinEventDelegate? _foregroundCallback; // kept rooted so the GC can't collect it mid-callback
    private IntPtr _foregroundHook;
    private DispatcherTimer? _stateTimer;

    public void Attach(Window window)
    {
        _source = PresentationSource.FromVisual(window) as HwndSource;
        _hwnd = _source?.Handle ?? IntPtr.Zero;
        if (_hwnd == IntPtr.Zero) return;

        var exStyle = GetWindowLongPtr(_hwnd, GwlExStyle).ToInt64();
        SetWindowLongPtr(_hwnd, GwlExStyle, new IntPtr(exStyle | WsExToolWindow | WsExNoActivate));

        // Hidden, disabled, bottom-pinned helper window. Its important job is being the SENTINEL for
        // IsDesktopRaised (see below) — a fixed reference point in the z-order. It is also set as the
        // surface's owner, mirroring Rainmeter's per-skin host window (keeps it out of the taskbar/Alt-Tab
        // and works around a Win11 per-monitor-V2 DPI quirk they documented).
        _ownerHwnd = CreateOwnerWindow();
        if (_ownerHwnd != IntPtr.Zero)
        {
            SetWindowPos(_ownerHwnd, HwndBottom, 0, 0, 0, 0, OwnMoveFlags);
            SetWindowLongPtr(_hwnd, GwlpHwndParent, _ownerHwnd);
        }

        ExcludeFromPeek(_hwnd); // Rainmeter's IgnoreAeroPeek

        _iconHost = FindIconHost();
        _source!.AddHook(WndProc);

        // Fast path: react to the shell window coming foreground (the Win+D signature). Rainmeter retries
        // here because the z-order takes a few ms to settle — without that, the state check runs too early
        // and reports "not raised".
        _foregroundCallback = OnForegroundChanged;
        _foregroundHook = SetWinEventHook(EventSystemForeground, EventSystemForeground,
            IntPtr.Zero, _foregroundCallback, 0, 0, WinEventOutOfContext);

        // Steady path: poll the real state. Rainmeter does exactly this (TIMER_SHOWDESKTOP, 250 ms) —
        // the foreground event only catches ENTERING show-desktop; the timer is what notices it ending.
        _stateTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
        _stateTimer.Tick += (_, _) => UpdateDesktopState();
        _stateTimer.Start();

        SendToBottom();
    }

    /// <summary>
    /// Ground-truth test for "show desktop", mirroring Rainmeter's CheckDesktopState: ask whether the
    /// SENTINEL window sits BELOW the icon host in the z-order. Normally the icon host (Progman) is the
    /// bottom-most window of all, so a HWND_BOTTOM window still lands above it — finding the sentinel
    /// below it means the shell raised the desktop over everything.
    ///
    /// The sentinel MUST be a window that never moves (we keep it pinned at HWND_BOTTOM), never the
    /// surface itself: testing the surface makes the state flip as soon as the counter-dance raises it,
    /// which oscillates at the poll interval (climb → "not raised" → bottom → "raised" → climb…) and shows
    /// up as constant flickering. Rainmeter dedicates a hidden window to this for exactly that reason; we
    /// reuse the hidden owner window we already create.
    /// </summary>
    private bool IsDesktopRaised()
    {
        if (_iconHost == IntPtr.Zero || _ownerHwnd == IntPtr.Zero) return false;

        for (var h = GetWindow(_iconHost, GwHwndNext); h != IntPtr.Zero; h = GetWindow(h, GwHwndNext))
            if (h == _ownerHwnd) return true;

        return false;
    }

    private void UpdateDesktopState()
    {
        if (_disposed) return;

        // Keep the sentinel pinned at the bottom — it's only a reference point, so it must not drift.
        if (_ownerHwnd != IntPtr.Zero)
            SetWindowPos(_ownerHwnd, HwndBottom, 0, 0, 0, 0, OwnMoveFlags);

        var raised = IsDesktopRaised();
        if (raised == _desktopRaised) return;

        _desktopRaised = raised;
        if (raised) ClimbAboveDesktop(); else SendToBottom();
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        // (3) Keep it at the bottom: rewrite external z-order changes back to HWND_BOTTOM (force, not
        // freeze — freeze makes the WPF layered window vanish). Only at rest: while climbed above a raised
        // desktop we let changes pass so the counter-dance holds. Our own moves carry SWP_NOSENDCHANGING
        // and never reach this hook.
        if (msg == WmWindowPosChanging && !_desktopRaised)
        {
            var pos = Marshal.PtrToStructure<WINDOWPOS>(lParam);
            pos.hwndInsertAfter = HwndBottom;
            pos.flags &= ~SwpNoZOrder;
            Marshal.StructureToPtr(pos, lParam, false);
        }

        return IntPtr.Zero;
    }

    private void OnForegroundChanged(IntPtr hWinEventHook, uint eventType, IntPtr hwnd, int idObject,
        int idChild, uint dwEventThread, uint dwmsEventTime)
    {
        if (_disposed || _hwnd == IntPtr.Zero) return;

        // Only the icon host coming foreground is worth the fast path; everything else is left to the
        // 250 ms poll. Retry briefly (as Rainmeter does) — the z-order needs a few ms to settle, so an
        // immediate check would still see the pre-Win+D order.
        if (hwnd != _iconHost) return;

        for (var i = 0; i < 5 && !IsDesktopRaised(); i++)
            Thread.Sleep(2);

        UpdateDesktopState();
    }

    /// <summary>Walk up the z-order from the icon host to the first genuine WS_EX_TOPMOST window and
    /// insert ourselves just behind it — above the raised desktop, below real topmost windows.</summary>
    private void ClimbAboveDesktop()
    {
        var candidate = _iconHost;
        var insertAfter = HwndTop;

        while (true)
        {
            var prev = GetWindow(candidate, GwHwndPrev);
            if (prev == IntPtr.Zero) break;

            if ((GetWindowLongPtr(prev, GwlExStyle).ToInt64() & WsExTopmost) != 0)
            {
                insertAfter = prev;
                break;
            }

            candidate = prev;
        }

        SetWindowPos(_hwnd, insertAfter, 0, 0, 0, 0, OwnMoveFlags);
    }

    private void SendToBottom() => SetWindowPos(_hwnd, HwndBottom, 0, 0, 0, 0, OwnMoveFlags);

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _stateTimer?.Stop();
        _stateTimer = null;

        if (_foregroundHook != IntPtr.Zero)
        {
            UnhookWinEvent(_foregroundHook);
            _foregroundHook = IntPtr.Zero;
        }
        _foregroundCallback = null;
        _source?.RemoveHook(WndProc);
        _source = null;

        if (_ownerHwnd != IntPtr.Zero)
        {
            DestroyWindow(_ownerHwnd);
            _ownerHwnd = IntPtr.Zero;
        }
    }
}
