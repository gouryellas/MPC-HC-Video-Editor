using System.Drawing;
using System.Windows;
using WinForms = System.Windows.Forms;

namespace MpcHcVideoEditor.Services;

/// <summary>
/// The notification-area icon and its menu.
/// </summary>
/// <remarks>
/// Wraps <see cref="WinForms.NotifyIcon"/>, which is still the only supported
/// way to put an icon in the tray. WPF has no equivalent, and doing it by hand
/// means owning a hidden message window plus the TaskbarCreated protocol for
/// when Explorer restarts — all of which NotifyIcon already handles.
///
/// The WinForms namespace is aliased rather than imported: with both UseWPF and
/// UseWindowsForms on, plain <c>using System.Windows.Forms</c> makes
/// <c>Application</c>, <c>MessageBox</c> and several others ambiguous
/// throughout the file.
///
/// Only ever touched from the UI thread. NotifyIcon raises its events on the
/// thread that created it, which is the dispatcher thread here.
/// </remarks>
public sealed class TrayIconService : IDisposable
{
    private readonly WinForms.NotifyIcon _icon;
    private bool _disposed;

    /// <summary>Raised when the user asks to bring the window back.</summary>
    public event Action? ShowRequested;

    /// <summary>Raised when the user asks to quit for real.</summary>
    public event Action? ExitRequested;

    public TrayIconService(string tooltip)
    {
        _icon = new WinForms.NotifyIcon
        {
            Icon = LoadIcon(),
            // Truncated because Windows silently ignores a tooltip over 63
            // characters, leaving no tooltip at all rather than a short one.
            Text = tooltip.Length > 63 ? tooltip[..63] : tooltip,
            Visible = false
        };

        var menu = new WinForms.ContextMenuStrip();

        var show = new WinForms.ToolStripMenuItem("Show window");
        show.Click += (_, _) => ShowRequested?.Invoke();

        // Bold, so double-clicking the icon and picking the default item are
        // visibly the same action.
        show.Font = new Font(show.Font, System.Drawing.FontStyle.Bold);

        var exit = new WinForms.ToolStripMenuItem("Exit");
        exit.Click += (_, _) => ExitRequested?.Invoke();

        menu.Items.Add(show);
        menu.Items.Add(new WinForms.ToolStripSeparator());
        menu.Items.Add(exit);

        _icon.ContextMenuStrip = menu;
        _icon.DoubleClick += (_, _) => ShowRequested?.Invoke();
    }

    /// <summary>
    /// Whether the icon is in the notification area.
    /// </summary>
    public bool Visible
    {
        get => !_disposed && _icon.Visible;
        set { if (!_disposed) _icon.Visible = value; }
    }

    /// <summary>Updates the hover text, e.g. to name the loaded video.</summary>
    public void SetTooltip(string text)
    {
        if (_disposed) return;
        _icon.Text = text.Length > 63 ? text[..63] : text;
    }

    /// <summary>
    /// A brief notification balloon. Used to explain, once, that closing the
    /// window did not close the program.
    /// </summary>
    public void ShowMessage(string title, string body)
    {
        if (_disposed || !_icon.Visible) return;

        try
        {
            _icon.BalloonTipTitle = title;
            _icon.BalloonTipText = body;
            _icon.BalloonTipIcon = WinForms.ToolTipIcon.Info;
            _icon.ShowBalloonTip(4000);
        }
        catch
        {
            // Balloons are suppressed entirely under some notification
            // settings. Never worth surfacing.
        }
    }

    /// <summary>
    /// Repaints the tray icon in the current theme's colours.
    /// </summary>
    /// <remarks>
    /// The previous icon is disposed after the new one is in place. NotifyIcon
    /// keeps using whatever it was last given, so freeing the old handle first
    /// is how you get a blank square in the tray.
    /// </remarks>
    public void ApplyTheme(ThemePalette palette)
    {
        if (_disposed) return;

        try
        {
            var previous = _icon.Icon;
            _icon.Icon = IconRenderer.TrayIcon(palette);
            previous?.Dispose();
        }
        catch
        {
            // A tray icon that cannot be redrawn keeps the one it has, which is
            // a wrong colour rather than no icon at all.
        }
    }

    /// <summary>
    /// The icon to start with: drawn from the active theme, falling back to the
    /// shipped asset and then the system default.
    /// </summary>
    /// <remarks>
    /// Drawn rather than loaded so it matches whichever theme is active before
    /// the first <see cref="ApplyTheme"/> call. The embedded .ico remains as a
    /// fallback for the case where rendering fails outright.
    /// </remarks>
    private static Icon LoadIcon()
    {
        try { return IconRenderer.TrayIcon(ThemeService.Current); }
        catch { /* fall through to the shipped asset */ }

        foreach (var path in new[] { "Assets/tray.ico", "Assets/app.ico" })
        {
            try
            {
                var uri = new Uri($"pack://application:,,,/{path}", UriKind.Absolute);
                var stream = System.Windows.Application.GetResourceStream(uri)?.Stream;
                if (stream != null) return new Icon(stream);
            }
            catch
            {
                // Try the next candidate.
            }
        }

        return SystemIcons.Application;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        // Hidden first: an icon disposed while visible can leave a dead entry
        // in the tray until the user hovers over it.
        try { _icon.Visible = false; } catch { }
        try { _icon.ContextMenuStrip?.Dispose(); } catch { }
        try { _icon.Dispose(); } catch { }
    }
}
