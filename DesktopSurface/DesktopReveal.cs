namespace Aquila.DesktopSurface;

/// <summary>
/// Clears the screen down to the wallpaper, and puts it back.
///
/// Edit mode exists so widgets can be judged where they actually live — over the user's own wallpaper, at
/// their real size. Minimizing only our own window left that judgement to be made through whatever browser
/// happened to be open behind it.
///
/// This is the shell's own "Show the desktop", the one on the taskbar's context menu, reached through the
/// documented <c>Shell.Application</c> automation object rather than by posting undocumented command ids at
/// <c>Shell_TrayWnd</c>. Both are best-effort: the shell can refuse, and a failure here must never stop the
/// user from editing, so everything is swallowed.
/// </summary>
internal static class DesktopReveal
{
    /// <summary>Minimizes every window that has a taskbar button. Call it BEFORE showing the edit-mode
    /// toolbar and the properties panel — those are how the user gets back out, and sweeping them away
    /// along with everything else would strand them.</summary>
    public static void MinimizeAll() => Invoke("MinimizeAll");

    /// <summary>Undoes <see cref="MinimizeAll"/>, restoring only what it minimized. Paired with it on the
    /// way out of edit mode: someone who wanted the desktop for twenty seconds should not have to rebuild
    /// their whole workspace afterwards.</summary>
    public static void RestoreAll() => Invoke("UndoMinimizeALL");

    private static void Invoke(string method)
    {
        try
        {
            var type = Type.GetTypeFromProgID("Shell.Application");
            if (type is null) return;

            var shell = Activator.CreateInstance(type);

            // Late-bound on purpose. Binding to the IShellDispatch interfaces would mean carrying an interop
            // assembly for two calls, and the method names have been stable since Windows 95 — including
            // UndoMinimizeALL's shouting, which is how the shell really spells it.
            type.InvokeMember(method, System.Reflection.BindingFlags.InvokeMethod, null, shell, null);
        }
        catch
        {
            // The shell is not obliged to cooperate — a locked-down desktop, Explorer restarting, COM
            // interop disabled in the runtime. Not being able to tidy the screen is not a reason to refuse
            // to edit widgets. The wrapper is short-lived and collected normally; releasing it by hand in a
            // finally would only add a second way to throw, on the very runtimes this catch exists for.
        }
    }
}
