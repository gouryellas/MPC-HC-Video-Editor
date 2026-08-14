using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using System.Runtime.InteropServices;
using MpcHcVideoEditor.Services;

namespace MpcHcVideoEditor.Views;

/// <summary>
/// Click-through overlay listing just the bookmarks, for when the video
/// covers the main window. Purely informational — it takes no input at all;
/// the X key (handled globally by the ViewModel) restores the full window, and
/// is live only while this window is actually showing, which is what the
/// "Press X" hint at the bottom of the card can therefore be trusted to mean.
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

    /// <summary>
    /// How much shorter than the monitor the card may grow, in DIPs.
    /// </summary>
    /// <remarks>
    /// The card takes as much height as the bookmark list needs until it is
    /// this far short of the screen, and only then does the list scroll. A
    /// fixed 300 px pane was the old behaviour: it scrolled after nine rows and
    /// so hid the most recent bookmarks, which are the ones being worked on.
    /// </remarks>
    private const double ScreenHeightReserve = 100;

    /// <summary>Floor for the pane height on a very short screen.</summary>
    private const double MinPaneHeight = 200;

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

        // Before the first Show, so the pane is never briefly the 300 px the
        // XAML declares. PositionInCorner re-applies it on every show.
        ApplyPaneHeight(SystemParameters.PrimaryScreenHeight);

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
    /// Retained for callers that do not care which corner. Prefer
    /// <see cref="PositionInCorner"/>.
    /// </remarks>
    public void PositionTopRight() => PositionInCorner(OverlayCorner.TopRight);

    /// <summary>
    /// Parks the overlay in the requested corner of the primary screen and
    /// sizes its pane to that screen.
    /// </summary>
    /// <remarks>
    /// Uses the full screen bounds, not the work area: over a full-screened
    /// player there is no taskbar to avoid, and the work area would leave the
    /// overlay floating oddly inset.
    ///
    /// The pane is the same near-screen height whichever corner is asked for;
    /// what changes is the edge the card is aligned to inside it. That is also
    /// why the bottom corners can work from the height just applied instead of
    /// forcing a layout pass to read <see cref="FrameworkElement.ActualHeight"/>
    /// — the pane's height no longer depends on its content.
    /// </remarks>
    public void PositionInCorner(OverlayCorner corner)
    {
        const double margin = 16;

        var screenWidth = SystemParameters.PrimaryScreenWidth;
        var screenHeight = SystemParameters.PrimaryScreenHeight;

        // Re-applied on every show rather than only at construction, so moving
        // the app to a differently-sized monitor is picked up.
        var paneHeight = ApplyPaneHeight(screenHeight);

        var left = corner is OverlayCorner.TopLeft or OverlayCorner.BottomLeft;
        Left = left ? margin : screenWidth - Width - margin;

        var top = corner is OverlayCorner.TopLeft or OverlayCorner.TopRight;

        // Anchoring the card to the same edge the pane is parked against is
        // what makes it read as sitting in the corner; left to stretch, it
        // would fill the whole pane instead of hugging the list.
        Panel.VerticalAlignment = top ? VerticalAlignment.Top : VerticalAlignment.Bottom;

        Top = top ? margin : screenHeight - paneHeight - margin;
    }

    /// <summary>
    /// Sizes the pane so the card inside it can reach the screen height less
    /// <see cref="ScreenHeightReserve"/>. Returns the height applied.
    /// </summary>
    /// <remarks>
    /// The card is inset by its own margin — room for the drop shadow, which
    /// would otherwise be clipped at the pane edge — so the pane has to be that
    /// much taller than the card is allowed to grow.
    ///
    /// Only ever grows the layer in practice: the value comes from the monitor,
    /// so a repeat show sets the same number and WPF makes it a no-op.
    /// </remarks>
    private double ApplyPaneHeight(double screenHeight)
    {
        var shadowInset = Panel.Margin.Top + Panel.Margin.Bottom;
        var height = Math.Max(MinPaneHeight,
                              screenHeight - ScreenHeightReserve + shadowInset);
        Height = height;
        return height;
    }

    /// <summary>
    /// Sets the panel's background opacity, 0.3–1.0. Fully opaque by default.
    /// </summary>
    /// <remarks>
    /// Applied to the inner <see cref="Border"/>, not to the window: the
    /// window itself is transparent by design, and lowering
    /// <see cref="UIElement.Opacity"/> on it would fade the text along with
    /// the backing, making a dim overlay unreadable rather than merely
    /// unobtrusive.
    ///
    /// The colour matches the Background in XAML, so the default value here
    /// repaints the panel exactly as designed rather than subtly shifting it.
    /// </remarks>
    public void SetBackgroundOpacity(double opacity)
    {
        if (Content is not Border border) return;

        border.Background = new SolidColorBrush(
            Color.FromArgb((byte)(Math.Clamp(opacity, 0.3, 1.0) * 255), 0x14, 0x14, 0x14));
    }
}
