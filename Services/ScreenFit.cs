using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;

namespace MpcHcVideoEditor.Services;

/// <summary>
/// Where a window is allowed to be: the usable area of the monitor it is on,
/// in the units WPF positions windows in.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="SystemParameters.WorkArea"/> answers for the primary monitor and
/// nothing else, which is wrong twice over on a second screen — the overlay
/// would be placed on the primary monitor whatever it was following, and a
/// window dragged to another screen would be measured against the wrong one.
/// So the monitor is asked for by window handle, or by the point of interest.
/// </para>
/// <para>
/// Everything here returns device-independent pixels. Win32 deals in physical
/// ones, and the two are the same number only at 100% scaling: a 1080p screen
/// at 150% gives a window 1280x720 to work in, which is where fixed window
/// heights written against a 1080p screen stop fitting.
/// </para>
/// </remarks>
public static class ScreenFit
{
    private const uint MONITOR_DEFAULTTONEAREST = 2;

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint flags);

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromPoint(POINT point, uint flags);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo info);

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT { public int X, Y; }

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT { public int Left, Top, Right, Bottom; }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct MonitorInfo
    {
        public int Size;
        public RECT Monitor;
        public RECT Work;
        public uint Flags;
    }

    /// <summary>
    /// The usable area of the monitor this window is on, in device-independent
    /// pixels. Falls back to the primary monitor's work area before the window
    /// has a handle.
    /// </summary>
    public static Rect WorkArea(Window window)
    {
        var handle = new System.Windows.Interop.WindowInteropHelper(window).Handle;

        if (handle == IntPtr.Zero)
            return SystemParameters.WorkArea;

        return Convert(MonitorFromWindow(handle, MONITOR_DEFAULTTONEAREST), Scale(window))
               ?? SystemParameters.WorkArea;
    }

    /// <summary>
    /// The usable area of whichever monitor holds the given physical point —
    /// for placing a window beside something that is not a window of ours.
    /// </summary>
    /// <param name="physicalX">Screen X in physical pixels.</param>
    /// <param name="physicalY">Screen Y in physical pixels.</param>
    /// <param name="forWindow">The window about to be placed, for its scale.</param>
    public static Rect WorkAreaAt(int physicalX, int physicalY, Window forWindow)
    {
        var point = new POINT { X = physicalX, Y = physicalY };

        return Convert(MonitorFromPoint(point, MONITOR_DEFAULTTONEAREST), Scale(forWindow))
               ?? SystemParameters.WorkArea;
    }

    /// <summary>
    /// The usable area of the monitor holding some other application's window
    /// — for putting one of ours beside it.
    /// </summary>
    /// <param name="anchor">
    /// A window handle to follow. <see cref="IntPtr.Zero"/>, or a handle that
    /// has since gone, falls back to the primary monitor.
    /// </param>
    /// <param name="forWindow">The window about to be placed, for its scale.</param>
    public static Rect WorkAreaBeside(IntPtr anchor, Window forWindow)
    {
        if (anchor == IntPtr.Zero) return SystemParameters.WorkArea;

        return Convert(MonitorFromWindow(anchor, MONITOR_DEFAULTTONEAREST), Scale(forWindow))
               ?? SystemParameters.WorkArea;
    }

    private static Rect? Convert(IntPtr monitor, double scale)
    {
        if (monitor == IntPtr.Zero || scale <= 0) return null;

        var info = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
        if (!GetMonitorInfo(monitor, ref info)) return null;

        return new Rect(info.Work.Left / scale,
                        info.Work.Top / scale,
                        (info.Work.Right - info.Work.Left) / scale,
                        (info.Work.Bottom - info.Work.Top) / scale);
    }

    /// <summary>
    /// How many physical pixels one device-independent pixel is worth for this
    /// window. 1.0 at 100%, 1.5 at 150%.
    /// </summary>
    private static double Scale(Window window)
    {
        try
        {
            var dpi = VisualTreeHelper.GetDpi(window);
            return dpi.DpiScaleX > 0 ? dpi.DpiScaleX : 1.0;
        }
        catch
        {
            // GetDpi throws for a visual that is not in a tree yet, and a
            // window being sized before it is shown is exactly that.
            return 1.0;
        }
    }

    /// <summary>
    /// Stops a window growing past the screen, without fixing its size.
    /// </summary>
    /// <remarks>
    /// For the <c>SizeToContent</c> dialogs, which size themselves to whatever
    /// they contain and will happily come out taller than the monitor. Pair it
    /// with a <c>ScrollViewer</c> — a cap on its own would cut the content off
    /// rather than putting it out of reach, which is no better.
    /// </remarks>
    /// <param name="margin">
    /// How much of the work area to leave alone, so a capped window still looks
    /// like a window rather than something wedged against the edges.
    /// </param>
    public static void Cap(Window window, double margin = 24)
    {
        var work = WorkArea(window);

        window.MaxWidth = Math.Max(320, work.Width - margin);
        window.MaxHeight = Math.Max(240, work.Height - margin);
    }

    /// <summary>
    /// Brings a window's size and position inside the usable area: no larger
    /// than the screen, and not hanging off any edge of it.
    /// </summary>
    /// <remarks>
    /// Needed for a size that came from somewhere other than this screen — the
    /// figures in the XAML, written against the monitor the program was built
    /// on, and a size restored from the last session, which may have been on a
    /// different display altogether.
    ///
    /// The minimums win. A window whose own MinHeight is taller than the screen
    /// cannot be made to fit, and WPF would override a smaller Height anyway;
    /// shrinking towards it and leaving it there keeps the top-left corner on
    /// screen, which is the part that matters for dragging it somewhere better.
    /// </remarks>
    public static void Fit(Window window)
    {
        var work = WorkArea(window);

        var width = Math.Max(window.MinWidth, Math.Min(window.Width, work.Width));
        var height = Math.Max(window.MinHeight, Math.Min(window.Height, work.Height));

        if (!double.IsNaN(width)) window.Width = width;
        if (!double.IsNaN(height)) window.Height = height;

        // Only move it if it is actually outside. A window the user placed is
        // left where they put it.
        if (!double.IsNaN(window.Left))
            window.Left = Math.Max(work.Left, Math.Min(window.Left, work.Right - width));

        if (!double.IsNaN(window.Top))
            window.Top = Math.Max(work.Top, Math.Min(window.Top, work.Bottom - height));
    }

    /// <summary>
    /// Whether a stored position still lands on a monitor that exists.
    /// </summary>
    /// <remarks>
    /// A window remembered on a second screen that has since been unplugged
    /// would otherwise be restored onto empty coordinates — present, focused,
    /// and nowhere the user can see. Tested against the whole virtual desktop
    /// rather than one monitor, so a window spanning two screens still counts,
    /// and generously: any overlap at all is enough.
    /// </remarks>
    public static bool IsOnScreen(double left, double top, double width, double height)
    {
        var virtualScreen = new Rect(SystemParameters.VirtualScreenLeft,
                                     SystemParameters.VirtualScreenTop,
                                     SystemParameters.VirtualScreenWidth,
                                     SystemParameters.VirtualScreenHeight);

        var window = new Rect(left, top, Math.Max(1, width), Math.Max(1, height));
        window.Intersect(virtualScreen);

        // Enough of it to grab: a sliver poking onto the screen is not a window
        // anyone can use.
        return window is { Width: >= 80, Height: >= 40 };
    }
}
