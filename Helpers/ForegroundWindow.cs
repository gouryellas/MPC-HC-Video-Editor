using System.Runtime.InteropServices;

namespace MpcHcVideoEditor.Helpers;

/// <summary>
/// Who currently owns the foreground.
/// </summary>
/// <remarks>
/// <para>
/// Two questions are answered from here, and they are the same question.
/// </para>
/// <para>
/// Whether an activation request will be granted: Windows gives the foreground
/// only to a process that already has it, and a refusal is not a no-op — it
/// flashes the taskbar button, offering the user the attention the program was
/// denied. Asking first is the difference between a quiet restore and a
/// blinking one.
/// </para>
/// <para>
/// And whether the user has gone somewhere else: a window that raises itself
/// over the application someone just clicked is a window interrupting them,
/// however politely it declines the focus afterwards.
/// </para>
/// <para>
/// Named for the window rather than called <c>Foreground</c>, which would be
/// unusable from any code-behind: every WPF control has a Foreground brush, and
/// inside one the type name resolves to that property instead.
/// </para>
/// </remarks>
public static class ForegroundWindow
{
    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

    /// <summary>
    /// Whether the foreground window belongs to this program.
    /// </summary>
    /// <remarks>
    /// By process rather than by window, deliberately: the overlay and the main
    /// window are both this program, and moving between them is not leaving it.
    /// </remarks>
    public static bool OwnedByThisProcess()
    {
        var foreground = GetForegroundWindow();
        if (foreground == IntPtr.Zero) return false;

        GetWindowThreadProcessId(foreground, out var owner);
        return owner == (uint)Environment.ProcessId;
    }

    /// <summary>
    /// Whether some other application has deliberately been put in front.
    /// </summary>
    /// <remarks>
    /// Not simply the negation of <see cref="OwnedByThisProcess"/>. Focus can
    /// end up nowhere — a player that closed, a window that went away, the
    /// desktop itself — and that is not the user choosing something else. Those
    /// cases answer false, so a window restoring itself into the gap is filling
    /// a vacancy rather than talking over anyone.
    ///
    /// The shell window is the desktop. Clicking it is as close to clicking
    /// nothing as Windows offers, and treating it as a third application would
    /// make "put everything away" the one gesture this never recovers from.
    /// </remarks>
    public static bool OwnedByAnotherApp()
    {
        var foreground = GetForegroundWindow();
        if (foreground == IntPtr.Zero) return false;
        if (foreground == GetShellWindow() || foreground == GetDesktopWindow()) return false;

        GetWindowThreadProcessId(foreground, out var owner);
        return owner != (uint)Environment.ProcessId;
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetShellWindow();

    [DllImport("user32.dll")]
    private static extern IntPtr GetDesktopWindow();
}
