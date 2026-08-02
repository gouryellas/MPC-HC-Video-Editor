using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using MpcHcVideoEditor.Views;

namespace MpcHcVideoEditor.Services;

/// <summary>
/// Shows brief, non-intrusive confirmations ("Timestamp set", "Clip saved")
/// as a floating overlay. Because timestamps are normally set by hotkey
/// while MPC-HC has focus — often fullscreen — the status bar in the main
/// window is not visible at that moment, so the feedback has to float above
/// whatever is on screen.
/// </summary>
/// <remarks>
/// A single <see cref="ToastWindow"/> is created lazily and reused; firing
/// the hotkey repeatedly re-uses that one window and restarts its timer
/// rather than stacking overlays. All public members must be called on the
/// UI thread (the hotkey hook already marshals there).
/// </remarks>
public sealed class ToastService : IDisposable
{
    private const int MONITOR_DEFAULTTOPRIMARY = 1;
    private const int MONITOR_DEFAULTTONEAREST = 2;

    private readonly DispatcherTimer _holdTimer;
    private readonly Func<IntPtr>? _anchorWindowProvider;
    private ToastWindow? _window;
    private bool _disposed;

    /// <summary>How long the toast stays at full opacity before fading.</summary>
    public TimeSpan HoldDuration { get; set; } = TimeSpan.FromSeconds(2.2);

    /// <summary>
    /// Set false to suppress all toasts (the status bar text still updates).
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <param name="anchorWindowProvider">Returns the window the toast should
    /// appear over — normally MPC-HC's. Used only to pick the monitor, so a
    /// return of <see cref="IntPtr.Zero"/> is fine and falls back to the
    /// primary display.</param>
    public ToastService(Func<IntPtr>? anchorWindowProvider = null)
    {
        _anchorWindowProvider = anchorWindowProvider;
        _holdTimer = new DispatcherTimer { Interval = HoldDuration };
        _holdTimer.Tick += (_, _) =>
        {
            _holdTimer.Stop();
            FadeOut();
        };
    }

    /// <summary>
    /// Displays a toast, replacing whatever is currently showing.
    /// </summary>
    /// <param name="title">Headline, e.g. "Timestamp 3 set".</param>
    /// <param name="detail">Optional second line, e.g. the time or filename.</param>
    /// <param name="icon">Leading glyph. Defaults to a map pin.</param>
    public void Show(string title, string? detail = null, string icon = "📍")
    {
        if (_disposed || !Enabled) return;

        // Never let a cosmetic notification take down the operation that
        // triggered it — a failure here is not worth surfacing to the user.
        try
        {
            var window = EnsureWindow();
            window.SetContent(icon, title, detail);

            // Force a layout pass so ActualWidth/ActualHeight reflect the new
            // text before we centre the window against the monitor.
            window.UpdateLayout();
            PositionOverAnchor(window);

            // Clear any in-flight fade, otherwise the animation keeps
            // ownership of Opacity and direct assignment is ignored.
            window.BeginAnimation(UIElement.OpacityProperty, null);
            window.Opacity = 1.0;

            _holdTimer.Stop();
            _holdTimer.Interval = HoldDuration;
            _holdTimer.Start();
        }
        catch
        {
            // ignored — toast is best-effort
        }
    }

    private ToastWindow EnsureWindow()
    {
        if (_window == null)
        {
            _window = new ToastWindow();
            _window.Closed += (_, _) => _window = null;
        }

        if (!_window.IsVisible)
            _window.Show();   // ShowActivated=false, so focus stays put

        return _window;
    }

    private void FadeOut()
    {
        if (_window == null) return;

        var fade = new DoubleAnimation
        {
            To = 0.0,
            Duration = TimeSpan.FromMilliseconds(450),
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn }
        };

        // Hide once transparent so no invisible layered window lingers over
        // the video; Show() brings it back for the next notification.
        fade.Completed += (_, _) =>
        {
            if (_window != null && Math.Abs(_window.Opacity) < 0.01)
                _window.Hide();
        };

        _window.BeginAnimation(UIElement.OpacityProperty, fade);
    }

    /// <summary>
    /// Centres the toast horizontally near the top of the monitor that holds
    /// the anchor window, so it sits over the video rather than over the
    /// player's on-screen controls at the bottom.
    /// </summary>
    private void PositionOverAnchor(ToastWindow window)
    {
        var anchor = IntPtr.Zero;
        try { anchor = _anchorWindowProvider?.Invoke() ?? IntPtr.Zero; }
        catch { /* provider failure just means "use the primary monitor" */ }

        var monitor = anchor != IntPtr.Zero
            ? MonitorFromWindow(anchor, MONITOR_DEFAULTTONEAREST)
            : MonitorFromWindow(IntPtr.Zero, MONITOR_DEFAULTTOPRIMARY);

        var info = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
        if (monitor == IntPtr.Zero || !GetMonitorInfo(monitor, ref info))
        {
            // Last resort: primary screen, in DIPs already.
            window.Left = (SystemParameters.PrimaryScreenWidth - window.ActualWidth) / 2;
            window.Top = SystemParameters.PrimaryScreenHeight * 0.08;
            return;
        }

        // GetMonitorInfo reports physical pixels; Left/Top are DIPs. Convert
        // through the window's own composition target so a scaled display
        // (125%, 150%, …) does not push the toast off-centre. rcMonitor —
        // not rcWork — because fullscreen playback covers the taskbar.
        var transform = PresentationSource.FromVisual(window)?.CompositionTarget?.TransformFromDevice
                        ?? Matrix.Identity;
        var topLeft = transform.Transform(new Point(info.rcMonitor.Left, info.rcMonitor.Top));
        var bottomRight = transform.Transform(new Point(info.rcMonitor.Right, info.rcMonitor.Bottom));

        var monitorWidth = bottomRight.X - topLeft.X;
        var monitorHeight = bottomRight.Y - topLeft.Y;

        window.Left = topLeft.X + (monitorWidth - window.ActualWidth) / 2;
        window.Top = topLeft.Y + monitorHeight * 0.08;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _holdTimer.Stop();
        try { _window?.Close(); } catch { }
        _window = null;
    }

    #region Win32

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT { public int Left, Top, Right, Bottom; }

    [StructLayout(LayoutKind.Sequential)]
    private struct MONITORINFO
    {
        public int cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public uint dwFlags;
    }

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr hwnd, int dwFlags);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

    #endregion
}
