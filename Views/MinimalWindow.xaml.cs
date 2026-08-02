using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using System.Runtime.InteropServices;

namespace MpcHcVideoEditor.Views;

/// <summary>
/// Click-through overlay listing just the bookmarks, for when the video
/// covers the main window. Purely informational — it takes no input at all;
/// Ctrl+Escape (handled globally by the ViewModel) restores the full window.
/// </summary>
public partial class MinimalWindow : Window
{
    private const int GwlExStyle = -20;
    private const int WsExTransparent = 0x20;
    private const int WsExNoActivate = 0x08000000;
    private const int WsExToolWindow = 0x00000080;

    private static readonly IntPtr HwndTopmost = new(-1);
    private const uint SwpNoMove = 0x0002;
    private const uint SwpNoSize = 0x0001;
    private const uint SwpNoActivate = 0x0010;

    [DllImport("user32.dll")]
    private static extern int GetWindowLong(IntPtr hWnd, int index);

    [DllImport("user32.dll")]
    private static extern int SetWindowLong(IntPtr hWnd, int index, int newStyle);

    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr after,
                                            int x, int y, int cx, int cy, uint flags);

    /// <summary>
    /// Re-asserts top-most placement periodically.
    /// </summary>
    /// <remarks>
    /// <c>Topmost="True"</c> is not enough against a full-screened player.
    /// A player going full screen makes itself top-most too, and the most
    /// recent window to claim that band wins — so the overlay ended up
    /// underneath, even though it sat correctly above a merely maximised
    /// window. Re-claiming the band on a timer puts it back in front.
    /// </remarks>
    private readonly DispatcherTimer _topmostTimer = new()
    {
        Interval = TimeSpan.FromMilliseconds(700)
    };

    public MinimalWindow()
    {
        InitializeComponent();

        _topmostTimer.Tick += (_, _) => AssertTopmost();
        IsVisibleChanged += (_, _) =>
        {
            if (IsVisible) { AssertTopmost(); _topmostTimer.Start(); }
            else _topmostTimer.Stop();
        };

        // IsHitTestVisible stops WPF routing input, but the window would still
        // swallow clicks at the OS level and steal focus from the player.
        // WS_EX_TRANSPARENT passes clicks through to whatever is underneath;
        // WS_EX_NOACTIVATE keeps it from taking focus; WS_EX_TOOLWINDOW keeps
        // it out of Alt+Tab.
        SourceInitialized += (_, _) =>
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            var style = GetWindowLong(hwnd, GwlExStyle);
            SetWindowLong(hwnd, GwlExStyle,
                style | WsExTransparent | WsExNoActivate | WsExToolWindow);
        };
    }

    /// <summary>Re-claims the top-most band without taking focus.</summary>
    private void AssertTopmost()
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        if (hwnd == IntPtr.Zero) return;

        // Toggling the WPF property alone does not re-order against another
        // process that has since claimed top-most; SetWindowPos does.
        Topmost = true;
        SetWindowPos(hwnd, HwndTopmost, 0, 0, 0, 0, SwpNoMove | SwpNoSize | SwpNoActivate);
    }

    /// <summary>
    /// Parks the overlay in the top-right corner.
    /// </summary>
    /// <remarks>
    /// Uses the full screen bounds, not the work area: over a full-screened
    /// player there is no taskbar to avoid, and the work area would leave the
    /// overlay floating oddly inset.
    /// </remarks>
    public void PositionTopRight()
    {
        Left = SystemParameters.PrimaryScreenWidth - Width - 16;
        Top = 16;
    }
}
