using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace MpcHcVideoEditor.Views;

/// <summary>
/// A small, borderless, always-on-top overlay used for transient
/// confirmations ("Timestamp set", "Clip saved"). It never takes focus and
/// never swallows a click, so it can appear on top of MPC-HC — including
/// fullscreen playback — without interrupting what the user is doing.
/// </summary>
/// <remarks>
/// Visibility and lifetime are driven entirely by
/// <see cref="Services.ToastService"/>; this class only owns the window
/// styles that make it non-intrusive.
/// </remarks>
public partial class ToastWindow : Window
{
    private const int GWL_EXSTYLE = -20;
    private const int WS_EX_TRANSPARENT = 0x00000020;  // clicks fall through
    private const int WS_EX_NOACTIVATE = 0x08000000;   // never take focus
    private const int WS_EX_TOOLWINDOW = 0x00000080;   // keep out of Alt+Tab

    public ToastWindow()
    {
        InitializeComponent();
    }

    public void SetContent(string icon, string title, string? detail)
    {
        IconText.Text = icon;
        TitleText.Text = title;
        DetailText.Text = detail ?? string.Empty;
        DetailText.Visibility = string.IsNullOrWhiteSpace(detail)
            ? Visibility.Collapsed
            : Visibility.Visible;
    }

    /// <summary>
    /// Applies the extended window styles once the HWND exists. WPF's
    /// <c>IsHitTestVisible</c> only governs WPF-level hit testing, so the
    /// OS-level WS_EX_TRANSPARENT here is what actually lets clicks reach
    /// the window underneath.
    /// </summary>
    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        var hwnd = new WindowInteropHelper(this).Handle;
        if (hwnd == IntPtr.Zero) return;

        var exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
        SetWindowLong(hwnd, GWL_EXSTYLE,
            exStyle | WS_EX_TRANSPARENT | WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW);
    }

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtr", SetLastError = true)]
    private static extern IntPtr GetWindowLongPtr64(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr", SetLastError = true)]
    private static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    [DllImport("user32.dll", EntryPoint = "GetWindowLong", SetLastError = true)]
    private static extern int GetWindowLong32(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "SetWindowLong", SetLastError = true)]
    private static extern int SetWindowLong32(IntPtr hWnd, int nIndex, int dwNewLong);

    // 32-bit Windows has no *Ptr variants, so pick per process bitness.
    private static int GetWindowLong(IntPtr hWnd, int nIndex)
        => IntPtr.Size == 8 ? (int)GetWindowLongPtr64(hWnd, nIndex) : GetWindowLong32(hWnd, nIndex);

    private static void SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong)
    {
        if (IntPtr.Size == 8) SetWindowLongPtr64(hWnd, nIndex, new IntPtr(dwNewLong));
        else SetWindowLong32(hWnd, nIndex, dwNewLong);
    }
}
