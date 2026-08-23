using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using MpcHcVideoEditor.Helpers;

namespace MpcHcVideoEditor.Services;

/// <summary>
/// Integration with Media Player Classic - Home Cinema.
/// </summary>
public class MpcHcService
{
    private const string MpcClassName = "MediaPlayerClassicW";

    #region Win32

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern IntPtr FindWindow(string? lpClassName, string? lpWindowName);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern int GetWindowTextLength(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool IsWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool IsZoomed(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool IsIconic(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr hWnd, uint flags);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MonitorInfo info);

    // Reuses the RECT already declared for the existing GetWindowRect import.
    [StructLayout(LayoutKind.Sequential)]
    private struct MonitorInfo
    {
        public int Size;
        public RECT Monitor;
        public RECT Work;
        public uint Flags;
    }

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern IntPtr GetAncestor(IntPtr hWnd, uint flags);

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out POINT lpPoint);

    [DllImport("user32.dll")]
    private static extern IntPtr WindowFromPoint(POINT point);

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT { public int X, Y; }

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, StringBuilder lParam);

    // Cross-process SendMessage blocks until the TARGET process pumps the
    // message — MPC-HC can sit on it for seconds while decoding, seeking or
    // opening a file, and the caller has no way out. These timeout variants
    // bound the wait instead. SMTO_ABORTIFHUNG returns immediately if the
    // target is already known-hung.
    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr SendMessageTimeout(IntPtr hWnd, uint Msg, IntPtr wParam,
        IntPtr lParam, uint fuFlags, uint uTimeout, out IntPtr lpdwResult);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr SendMessageTimeout(IntPtr hWnd, uint Msg, IntPtr wParam,
        StringBuilder lParam, uint fuFlags, uint uTimeout, out IntPtr lpdwResult);

    private const uint SMTO_ABORTIFHUNG = 0x0002;

    /// <summary>Milliseconds to wait on any single cross-process send.</summary>
    private const uint SendTimeoutMs = 40;

    [DllImport("user32.dll")]
    private static extern bool EnumChildWindows(IntPtr hWndParent, EnumChildProc lpEnumFunc, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT { public int Left, Top, Right, Bottom; }

    private delegate bool EnumChildProc(IntPtr hWnd, IntPtr lParam);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

    private const uint WM_COMMAND = 0x0111;
    private const uint WM_LBUTTONDOWN = 0x0201;
    private const uint WM_LBUTTONUP = 0x0202;
    private const uint SB_GETTEXTLENGTHW = 0x040C;
    private const uint SB_GETTEXTW = 0x040D;
    private const uint SB_GETPARTS = 0x0406;

    // Trackbar class name used to identify MPC-HC's seek bar.
    private const string TrackbarClass = "msctls_trackbar32";

    private const int CMD_PLAY = 889;
    private const int CMD_PAUSE = 890;
    private const int CMD_STOP = 891;
    private const int CMD_PLAYPAUSE = 888;

    /// <summary>
    /// <c>ID_FILE_CLOSEMEDIA</c> — closes the open file and leaves the player
    /// running.
    /// </summary>
    /// <remarks>
    /// Taken from MPC-HC's own <c>resource.h</c>, not from the several online
    /// command tables that give 804. 804 is <c>ID_FILE_CLOSE_AND_RESTORE</c>,
    /// which is a different action.
    ///
    /// Stop is not a substitute: MPC-HC keeps its handle on the file when
    /// stopped, so the file stays locked. Only closing the media releases it.
    /// </remarks>
    private const int CMD_CLOSE_FILE = 803;

    #endregion

    public bool IsRunning => FindMpcWindow() != IntPtr.Zero;

    public IntPtr FindMpcWindow() => FindWindow(MpcClassName, null);

    public string? GetWindowTitle()
    {
        var hwnd = FindMpcWindow();
        if (hwnd == IntPtr.Zero) return null;

        int len = GetWindowTextLength(hwnd);
        if (len <= 0) return string.Empty;

        var sb = new StringBuilder(len + 1);
        GetWindowText(hwnd, sb, sb.Capacity);
        return sb.ToString();
    }

    public string? GetCurrentFilePath()
    {
        var title = GetWindowTitle();
        if (string.IsNullOrWhiteSpace(title)) return null;

        // "filename.ext - Media Player Classic"  or full path
        var m = Regex.Match(title, @"^(.+?)\s+-\s+Media Player Classic", RegexOptions.IgnoreCase);
        if (m.Success)
        {
            var candidate = m.Groups[1].Value.Trim();
            if (File.Exists(candidate)) return candidate;
            return candidate; // may be just filename
        }

        if (File.Exists(title.Trim())) return title.Trim();
        return null;
    }

    /// <summary>
    /// Tries multiple methods to get current position + duration.
    /// Returns (currentSeconds, durationSeconds). Either may be 0 if unavailable.
    /// </summary>
    public (double CurrentSeconds, double DurationSeconds) GetPlaybackPosition()
    {
        var hwnd = FindMpcWindow();
        if (hwnd == IntPtr.Zero) return (0, 0);

        // Method 1: Status bar text
        var fromStatus = TryReadStatusBar(hwnd);
        if (fromStatus.Duration > 0 || fromStatus.Current > 0)
        {
            _statusBarWorks = true;
            return fromStatus;
        }

        // Method 2: Look for any child window text that looks like a time.
        // This walks every child window, so only fall back to it while the
        // status bar has never worked for this build of MPC-HC. Once the
        // status bar has answered even once, a miss just means the player is
        // momentarily busy — retrying via a full tree walk every 300ms would
        // burn CPU for nothing.
        if (!_statusBarWorks)
        {
            var fromChildren = TryReadAnyChildTime(hwnd);
            if (fromChildren.Duration > 0 || fromChildren.Current > 0)
                return fromChildren;
        }

        return (0, 0);
    }

    // The status bar handle is stable for the lifetime of MPC-HC's window, so
    // cache it per main-window handle instead of re-walking the child window
    // tree on every poll.
    private IntPtr _cachedStatusBarOwner = IntPtr.Zero;
    private IntPtr _cachedStatusBar = IntPtr.Zero;

    /// <summary>
    /// Set once the status bar has successfully reported a time, which means
    /// the expensive child-window-scan fallback is not needed on this system.
    /// </summary>
    private bool _statusBarWorks;

    private IntPtr FindStatusBar(IntPtr mainHwnd)
    {
        if (_cachedStatusBarOwner == mainHwnd &&
            _cachedStatusBar != IntPtr.Zero &&
            IsWindow(_cachedStatusBar))
        {
            return _cachedStatusBar;
        }

        IntPtr statusBar = IntPtr.Zero;
        EnumChildWindows(mainHwnd, (child, _) =>
        {
            var cls = new StringBuilder(256);
            GetClassName(child, cls, cls.Capacity);
            var className = cls.ToString();

            if (className.Contains("statusbar", StringComparison.OrdinalIgnoreCase) ||
                className == "msctls_statusbar32")
            {
                statusBar = child;
                return false; // stop enumeration
            }
            return true;
        }, IntPtr.Zero);

        _cachedStatusBarOwner = mainHwnd;
        _cachedStatusBar = statusBar;
        return statusBar;
    }

    private (double Current, double Duration) TryReadStatusBar(IntPtr mainHwnd)
    {
        var statusBar = FindStatusBar(mainHwnd);
        if (statusBar == IntPtr.Zero) return (0, 0);

        // Every read below is time-bounded. If MPC-HC is mid-seek or busy
        // decoding it simply won't answer in time — we bail and let the caller
        // keep the last known position rather than parking the calling thread
        // on a cross-process send. Parts are scanned in order and we stop as
        // soon as a "current / duration" pair parses, which in practice is the
        // first non-empty part, so the common case is two quick sends.
        var fullText = new StringBuilder();
        for (int part = 0; part < 8; part++)
        {
            if (SendMessageTimeout(statusBar, SB_GETTEXTLENGTHW, (IntPtr)part, IntPtr.Zero,
                    SMTO_ABORTIFHUNG, SendTimeoutMs, out var lengthResult) == IntPtr.Zero)
            {
                break; // timed out or target hung — give up this cycle
            }

            int length = (int)lengthResult;
            if (length <= 0) continue;

            var sb = new StringBuilder(length + 2);
            if (SendMessageTimeout(statusBar, SB_GETTEXTW, (IntPtr)part, sb,
                    SMTO_ABORTIFHUNG, SendTimeoutMs, out _) == IntPtr.Zero)
            {
                break;
            }

            fullText.Append(' ').Append(sb);

            var parsed = ParseTimePair(fullText.ToString());
            if (parsed.Duration > 0 || parsed.Current > 0) return parsed;
        }

        return ParseTimePair(fullText.ToString());
    }

    private (double Current, double Duration) TryReadAnyChildTime(IntPtr mainHwnd)
    {
        string? found = null;

        EnumChildWindows(mainHwnd, (child, _) =>
        {
            var sb = new StringBuilder(512);
            GetWindowText(child, sb, sb.Capacity);
            var text = sb.ToString();
            if (string.IsNullOrWhiteSpace(text)) return true;

            // Look for patterns like 00:01:23 / 00:45:00 or 1:23 / 45:00
            if (Regex.IsMatch(text, @"\d{1,2}:\d{2}(?::\d{2})?\s*/\s*\d{1,2}:\d{2}(?::\d{2})?"))
            {
                found = text;
                return false;
            }
            return true;
        }, IntPtr.Zero);

        return found != null ? ParseTimePair(found) : (0, 0);
    }

    private static (double Current, double Duration) ParseTimePair(string text)
    {
        // 00:01:23 / 00:45:12
        // 1:23 / 45:12
        // 00:01:23.45 / 00:45:12.00
        var m = Regex.Match(text,
            @"(\d{1,2}:\d{2}(?::\d{2})?(?:\.\d+)?)\s*/\s*(\d{1,2}:\d{2}(?::\d{2})?(?:\.\d+)?)");

        if (!m.Success) return (0, 0);

        return (ParseTimeString(m.Groups[1].Value), ParseTimeString(m.Groups[2].Value));
    }

    private static double ParseTimeString(string s)
    {
        try
        {
            var parts = s.Split(':');
            if (parts.Length == 3)
                return double.Parse(parts[0]) * 3600 + double.Parse(parts[1]) * 60 + double.Parse(parts[2]);
            if (parts.Length == 2)
                return double.Parse(parts[0]) * 60 + double.Parse(parts[1]);
            return double.Parse(parts[0]);
        }
        catch
        {
            return 0;
        }
    }

    public void BringToFront()
    {
        var hwnd = FindMpcWindow();
        if (hwnd == IntPtr.Zero) return;
        ShowWindow(hwnd, 9); // SW_RESTORE
        SetForegroundWindow(hwnd);
    }

    /// <summary>
    /// Launches the given video file in MPC-HC (or, if MPC-HC cannot be
    /// located, in the user's default video player via shell-execute).
    /// </summary>
    /// <remarks>
    /// <para>
    /// If MPC-HC is already running, this replaces the currently-loaded
    /// file in the existing instance (MPC-HC's default single-instance
    /// behavior). If MPC-HC is not running, a new instance is started.
    /// </para>
    /// <para>
    /// The lookup order for the MPC-HC executable is:
    /// </para>
    /// <list type="number">
    ///   <item>The <c>MPC-HC</c> / <c>MPC-BE</c> install directories under
    ///         <c>%ProgramFiles%</c> and <c>%ProgramFiles(x86)%</c>.</item>
    ///   <item>The K-Lite Codec Pack install directory (which ships
    ///         MPC-HC as its default player).</item>
    ///   <item>Fallback: shell-execute the video file itself, which
    ///         opens it in whatever the user has registered as the
    ///         default player for that extension.</item>
    /// </list>
    /// </remarks>
    /// <returns><c>true</c> if a launch was attempted, <c>false</c> if the
    /// path was missing or empty.</returns>
    public bool LaunchVideo(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return false;

        // 1) Try to locate mpc-hc.exe / mpc-hc64.exe / mpc-be.exe directly.
        var exe = FindMpcExecutable();
        if (exe != null)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = exe,
                    Arguments = $"\"{path}\"",
                    UseShellExecute = false
                });
                return true;
            }
            catch
            {
                // Fall through to shell-execute below.
            }
        }

        // 2) Fallback: shell-execute the file with its registered handler.
        //    For a user of "MPC-HC Video Editor" this is almost always
        //    MPC-HC itself, but this also gracefully handles users who
        //    have registered a different player as their default.
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = path,
                UseShellExecute = true,
                Verb = "open"
            });
            return true;
        }
        catch
        {
            // 3) Last-ditch: ask explorer.exe to open it.
            try
            {
                Process.Start("explorer.exe", $"\"{path}\"");
                return true;
            }
            catch
            {
                return false;
            }
        }
    }

    /// <summary>
    /// Reads the Web Interface settings out of the player's own configuration,
    /// or returns <c>null</c> when they cannot be found.
    /// </summary>
    /// <remarks>
    /// Lives here because locating the player is already this service's job,
    /// and a portable install's settings sit beside its executable.
    /// </remarks>
    public static MpcHcWebConfig? DetectWebInterface()
        => MpcHcConfig.Detect(FindMpcExecutable());

    /// <summary>
    /// The Web Interface configuration read at the last call to
    /// <see cref="DetectWebInterface"/> that the app acted on, so a failure
    /// message can say what the player is actually set to rather than guessing.
    /// </summary>
    public MpcHcWebConfig? LastDetectedWebConfig { get; set; }

    /// <summary>
    /// One line describing what the player's own settings say, for the Settings
    /// dialog.
    /// </summary>
    /// <remarks>
    /// Gives the Web Interface being switched off the same prominence as the
    /// port. A wrong port is the problem people expect; an interface that was
    /// never turned on is the one they actually have.
    /// </remarks>
    public static string DescribeWebInterface()
    {
        var config = DetectWebInterface();
        if (config is null)
            return "MPC-HC's settings could not be read — the port below will be used.";

        return config.Enabled
            ? $"MPC-HC is serving on port {config.Port} (read from {config.Source})."
            : $"MPC-HC's Web Interface is turned OFF. Its port is set to {config.Port}, but nothing " +
              "will answer until \"Listen on port\" is ticked in Options → Player → Web Interface.";
    }

    /// <summary>
    /// Looks in the common install locations for MPC-HC or MPC-BE.
    /// Returns the first executable found, or <c>null</c> if none match.
    /// </summary>
    private static string? FindMpcExecutable()
    {
        var programDirs = new[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "K-Lite Codec Pack"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "K-Lite Codec Pack")
        };

        var exeNames = new[] { "mpc-hc64.exe", "mpc-hc.exe", "mpc-be64.exe", "mpc-be.exe" };

        foreach (var dir in programDirs)
        {
            if (string.IsNullOrEmpty(dir)) continue;

            // Direct install (e.g. C:\Program Files\MPC-HC\mpc-hc64.exe)
            foreach (var exe in exeNames)
            {
                var direct = Path.Combine(dir, "MPC-HC", exe);
                if (File.Exists(direct)) return direct;
                var directBe = Path.Combine(dir, "MPC-BE", exe);
                if (File.Exists(directBe)) return directBe;
            }

            // K-Lite Codec Pack layout: <KLiteRoot>\MPC-HC\mpc-hc64.exe
            if (dir.EndsWith("K-Lite Codec Pack", StringComparison.OrdinalIgnoreCase))
            {
                foreach (var exe in exeNames)
                {
                    var klite = Path.Combine(dir, "MPC-HC", exe);
                    if (File.Exists(klite)) return klite;
                    var kliteBe = Path.Combine(dir, "MPC-BE", exe);
                    if (File.Exists(kliteBe)) return kliteBe;
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Human-readable reason the most recent <see cref="SeekToAsync"/> call
    /// failed, or <c>null</c> if it succeeded (or hasn't been called yet).
    /// </summary>
    public string? LastSeekFailureReason { get; private set; }

    private static readonly HttpClient _webHttp = new() { Timeout = TimeSpan.FromSeconds(2) };

    /// <summary>
    /// Port MPC-HC's Web Interface listens on. This matches MPC-HC's own
    /// default (Options → Player → Web Interface → Listen on port); change
    /// here if you've configured MPC-HC to use a different port.
    /// </summary>
    /// <remarks>
    /// Settable, because MPC-HC's own port is settable. It was a <c>const</c>,
    /// which left anyone who had moved it with seeking silently falling back
    /// to the slower window-message path and no way to say so.
    /// </remarks>
    public int WebInterfacePort { get; set; } = 13579;

    /// <summary>
    /// Seeks MPC-HC's currently loaded video to the given position (in seconds).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Tries MPC-HC's built-in Web Interface first — an HTTP endpoint MPC-HC
    /// itself exposes specifically for external control, including setting
    /// the playback position directly. This is the reliable mechanism; it
    /// requires the user to have enabled it once in MPC-HC (Options →
    /// Player → Web Interface → check "Listen on port", default 13579).
    /// </para>
    /// <para>
    /// If the Web Interface isn't reachable (not enabled, or a different
    /// port), we fall back to simulating a mouse click on the seek-bar
    /// trackbar at the pixel position for the target time. That fallback is
    /// inherently less reliable — it depends on MPC-HC's seek bar actually
    /// being a plain Win32 trackbar control at a predictable location, which
    /// isn't guaranteed with every skin/theme — so we verify it actually
    /// moved the position afterward before reporting success.
    /// </para>
    /// </remarks>
    public async Task<bool> SeekToAsync(double seconds)
    {
        LastSeekFailureReason = null;
        if (double.IsNaN(seconds) || double.IsInfinity(seconds))
        {
            LastSeekFailureReason = "Invalid seek time.";
            return false;
        }
        seconds = Math.Max(0, seconds);

        if (await TrySeekViaWebInterfaceAsync(seconds))
            return true;

        return await SeekViaTrackbarAsync(seconds);
    }

    private async Task<bool> TrySeekViaWebInterfaceAsync(double seconds)
    {
        var ts = TimeSpan.FromSeconds(seconds);
        // MPC-HC's Web Interface expects HH:MM:SS for the position parameter.
        string position = $"{(int)ts.TotalHours:00}:{ts.Minutes:00}:{ts.Seconds:00}";

        try
        {
            var url = $"http://127.0.0.1:{WebInterfacePort}/command.html?wm_command=-1&position={Uri.EscapeDataString(position)}";
            using var response = await _webHttp.GetAsync(url);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            // Not enabled, wrong port, or MPC-HC not running — fall back.
            return false;
        }
    }

    private async Task<bool> SeekViaTrackbarAsync(double seconds)
    {
        var hwnd = FindMpcWindow();
        if (hwnd == IntPtr.Zero)
        {
            LastSeekFailureReason = "MPC-HC's window couldn't be found.";
            return false;
        }

        // We need the total duration to map seconds → pixel position.
        var (_, duration) = GetPlaybackPosition();
        if (duration <= 0)
        {
            // The player's own settings usually say exactly what is wrong, so
            // read them rather than repeating generic advice at someone whose
            // port was never the problem.
            var config = LastDetectedWebConfig ?? DetectWebInterface();
            LastSeekFailureReason =
                "Couldn't reach MPC-HC's Web Interface, and couldn't read position/duration " +
                "from its status bar either. " +
                (config is null
                    ? "MPC-HC's settings couldn't be read, so check Options → Player → Web Interface: " +
                      $"tick \"Listen on port\" and set it to {WebInterfacePort}, then click Apply."
                    : !config.Enabled
                        ? "MPC-HC's own settings say the Web Interface is turned off — that is the " +
                          "problem, not the port. Tick \"Listen on port\" in Options → Player → " +
                          "Web Interface, click Apply, then try again."
                        : config.Port != WebInterfacePort
                            ? $"MPC-HC is set to port {config.Port} but this app is using " +
                              $"{WebInterfacePort}. Turn on automatic detection in Settings → Player, " +
                              "or set the port to match."
                            : $"MPC-HC is set to serve on port {config.Port}, which matches. It may " +
                              "still be starting up, or something is blocking the connection.");
            return false;
        }

        seconds = Math.Clamp(seconds, 0, duration);

        var slider = FindSeekBar(hwnd);
        if (slider == IntPtr.Zero)
        {
            LastSeekFailureReason = "Couldn't reach MPC-HC's Web Interface, and couldn't find a seek bar " +
                "control to click instead. Easiest fix: in MPC-HC, go to Options → Player → Web Interface, " +
                $"check \"Listen on port\" (this app expects {WebInterfacePort}), click Apply, then try again.";
            return false;
        }

        // Get the trackbar's client-area size.
        if (!GetWindowRect(slider, out var rect))
        {
            LastSeekFailureReason = "Found the seek bar, but couldn't read its screen position.";
            return false;
        }
        int sliderWidth = rect.Right - rect.Left;
        int sliderHeight = rect.Bottom - rect.Top;
        if (sliderWidth <= 0 || sliderHeight <= 0)
        {
            LastSeekFailureReason = "The seek bar reported a zero size (it may be hidden).";
            return false;
        }

        // Compute the fraction of the bar to click.
        double fraction = seconds / duration;

        // Leave a small margin so we don't click the very edge pixels
        // (which the trackbar may treat as arrow buttons).
        int margin = Math.Max(4, sliderWidth / 20);
        int clickableWidth = sliderWidth - 2 * margin;
        int x = margin + (int)Math.Round(fraction * clickableWidth);
        int y = sliderHeight / 2;

        // Pack into lParam: LOWORD = x, HIWORD = y (client coordinates).
        IntPtr lParam = (IntPtr)((y & 0xFFFF) << 16 | (x & 0xFFFF));
        IntPtr mkLButton = (IntPtr)0x0001; // MK_LBUTTON

        // Post a mouse-down / mouse-up pair.  Posting (async) avoids
        // re-entrancy issues that SendMessage can cause across processes.
        PostMessage(slider, WM_LBUTTONDOWN, mkLButton, lParam);
        PostMessage(slider, WM_LBUTTONUP, IntPtr.Zero, lParam);

        // Verify it actually worked instead of trusting the click blindly:
        // this trackbar might be the wrong control entirely (e.g. volume,
        // if MPC-HC's real seek bar isn't a plain Win32 trackbar in this
        // skin), so re-read the position and confirm it landed near target.
        await Task.Delay(200);
        var (nowPos, _) = GetPlaybackPosition();
        if (Math.Abs(nowPos - seconds) > Math.Max(3, duration * 0.02))
        {
            LastSeekFailureReason = "Clicked MPC-HC's seek bar, but the position didn't actually change " +
                "(this control likely isn't the real seek bar in your MPC-HC skin/theme). " +
                "Easiest fix: in MPC-HC, go to Options → Player → Web Interface, check \"Listen on port\" " +
                $"(this app expects {WebInterfacePort}), click Apply, then try again — that path doesn't depend on clicking a control at all.";
            return false;
        }

        return true;
    }

    /// <summary>
    /// Finds MPC-HC's seek bar (a Win32 trackbar / msctls_trackbar32 child window).
    /// MPC-HC's main window hosts several trackbars (seek, volume, etc.); the
    /// seek bar is always the widest one, regardless of video duration.
    /// Using width is more reliable than range because the volume slider's
    /// 0-100 range can exceed a short video's seek bar range.
    /// </summary>
    private IntPtr FindSeekBar(IntPtr mainHwnd)
    {
        IntPtr best = IntPtr.Zero;
        int bestWidth = -1;

        EnumChildWindows(mainHwnd, (child, _) =>
        {
            var cls = new StringBuilder(256);
            GetClassName(child, cls, cls.Capacity);
            var className = cls.ToString();

            if (!className.Equals(TrackbarClass, StringComparison.OrdinalIgnoreCase) &&
                !className.Contains("trackbar", StringComparison.OrdinalIgnoreCase))
                return true;

            if (GetWindowRect(child, out var rect))
            {
                int width = rect.Right - rect.Left;
                if (width > bestWidth)
                {
                    bestWidth = width;
                    best = child;
                }
            }
            return true;
        }, IntPtr.Zero);

        return best;
    }

    public void SendCommand(int commandId)
    {
        var hwnd = FindMpcWindow();
        if (hwnd != IntPtr.Zero)
            SendMessage(hwnd, WM_COMMAND, (IntPtr)commandId, IntPtr.Zero);
    }

    /// <summary>How the player's window is currently presented.</summary>
    public enum PlayerWindowState
    {
        /// <summary>Not running, or its window could not be found.</summary>
        NotRunning,

        /// <summary>Windowed, or minimised — anything that is not covering a screen.</summary>
        Normal,

        Maximized,

        /// <summary>Borderless and covering its monitor.</summary>
        Fullscreen
    }

    /// <summary>
    /// Reports whether the player is fullscreen, maximised, or neither.
    /// </summary>
    /// <remarks>
    /// Fullscreen is detected geometrically rather than from a window style.
    /// A player going fullscreen keeps an ordinary top-level window and simply
    /// drops its border and grows to cover the monitor, so there is no style
    /// bit that reliably distinguishes it from maximised — comparing the
    /// window rect against the monitor's does.
    /// </remarks>
    public PlayerWindowState GetWindowState()
    {
        var hwnd = FindMpcWindow();
        if (hwnd == IntPtr.Zero || !IsWindow(hwnd)) return PlayerWindowState.NotRunning;

        // Minimised is not "covering the screen", so it reads as Normal.
        if (IsIconic(hwnd)) return PlayerWindowState.Normal;

        if (!GetWindowRect(hwnd, out var window))
            return PlayerWindowState.Normal;

        const uint MONITOR_DEFAULTTONEAREST = 2;
        var monitor = MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST);
        if (monitor != IntPtr.Zero)
        {
            var info = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
            if (GetMonitorInfo(monitor, ref info))
            {
                // Covering the full monitor bounds (not just the work area,
                // which excludes the taskbar) means fullscreen. A small
                // tolerance absorbs off-by-one borders.
                const int slack = 2;
                var coversMonitor =
                    window.Left   <= info.Monitor.Left   + slack &&
                    window.Top    <= info.Monitor.Top    + slack &&
                    window.Right  >= info.Monitor.Right  - slack &&
                    window.Bottom >= info.Monitor.Bottom - slack;

                if (coversMonitor) return PlayerWindowState.Fullscreen;
            }
        }

        return IsZoomed(hwnd) ? PlayerWindowState.Maximized : PlayerWindowState.Normal;
    }

    /// <summary>
    /// True when MPC-HC is the window the user is currently working in.
    /// </summary>
    /// <remarks>
    /// Compares against the foreground window's <em>root</em>, not the
    /// foreground window itself. MPC-HC's video area, its seek bar and its
    /// playlist are child windows in their own right, and clicking one makes
    /// that child the focus — so a direct handle comparison reports "not
    /// focused" precisely when the user is most obviously using the player.
    /// GA_ROOT walks back up to the top-level window in every case.
    ///
    /// Returns false when the player is not running at all, which is the
    /// answer the caller wants anyway.
    /// </remarks>
    public bool IsForeground()
    {
        var mpc = FindMpcWindow();
        if (mpc == IntPtr.Zero || !IsWindow(mpc)) return false;

        var foreground = GetForegroundWindow();
        if (foreground == IntPtr.Zero) return false;

        const uint GA_ROOT = 2;
        var root = GetAncestor(foreground, GA_ROOT);
        if (root == IntPtr.Zero) root = foreground;

        return root == mpc;
    }

    /// <summary>
    /// True when the mouse cursor is currently over MPC-HC's window, whether
    /// or not that window has focus.
    /// </summary>
    /// <remarks>
    /// Same GA_ROOT walk as <see cref="IsForeground"/>, applied to
    /// <c>WindowFromPoint</c> instead of <c>GetForegroundWindow</c> — the
    /// point under the cursor lands on a child control (the video area, the
    /// seek bar) just as often as a click does, for the same reason a direct
    /// handle comparison would misreport there too.
    ///
    /// Returns false when the player is not running, same as
    /// <see cref="IsForeground"/>.
    /// </remarks>
    public bool IsPointerOver()
    {
        var mpc = FindMpcWindow();
        if (mpc == IntPtr.Zero || !IsWindow(mpc)) return false;

        if (!GetCursorPos(out var pt)) return false;

        var hit = WindowFromPoint(pt);
        if (hit == IntPtr.Zero) return false;

        const uint GA_ROOT = 2;
        var root = GetAncestor(hit, GA_ROOT);
        if (root == IntPtr.Zero) root = hit;

        return root == mpc;
    }

    public void Play() => SendCommand(CMD_PLAY);
    public void Pause() => SendCommand(CMD_PAUSE);
    public void Stop() => SendCommand(CMD_STOP);
    public void PlayPause() => SendCommand(CMD_PLAYPAUSE);

    /// <summary>
    /// Closes whatever file the player has open, without closing the player.
    /// </summary>
    /// <remarks>
    /// The reason this exists is deletion: MPC-HC holds an open handle on the
    /// file it is playing, so the source video cannot be removed while it is
    /// loaded — waiting does not help, because nothing is going to let go on
    /// its own.
    /// </remarks>
    public void CloseFile() => SendCommand(CMD_CLOSE_FILE);

    public bool HasVideoLoaded()
    {
        var title = GetWindowTitle();
        if (string.IsNullOrWhiteSpace(title)) return false;
        return !title.Equals("Media Player Classic", StringComparison.OrdinalIgnoreCase)
               && !title.StartsWith("Media Player Classic", StringComparison.OrdinalIgnoreCase);
    }
}
