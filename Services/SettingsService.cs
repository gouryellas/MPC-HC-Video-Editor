using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows.Input;
using MpcHcVideoEditor.Models;
using MpcHcVideoEditor.Helpers;

namespace MpcHcVideoEditor.Services;

/// <summary>
/// What happens to a source file once the operation that consumed it has
/// finished successfully.
/// </summary>
/// <remarks>
/// Serialized by name, so the order of these members is not load-bearing and
/// an unrecognised value falls back to <see cref="Never"/>.
/// </remarks>
public enum CleanupMode
{
    /// <summary>Leave it alone. The default — nothing disappears unasked.</summary>
    Never,

    /// <summary>Prompt after the operation, defaulting to No.</summary>
    Ask,

    /// <summary>
    /// Delete it without prompting — to the Recycle Bin unless
    /// <see cref="AppSettings.DeleteToRecycleBin"/> has been turned off.
    /// </summary>
    Always
}

/// <summary>
/// Encoder effort. Trades file size and visual quality against how long an
/// operation takes.
/// </summary>
/// <remarks>
/// Only reaches ffmpeg when something is actually re-encoded — a flip, a speed
/// change, a convert, or a container that cannot hold H.264. A plain merge to
/// MP4 copies its segments and is unaffected by this setting.
/// </remarks>
public enum EncodingQuality
{
    /// <summary>Fastest, largest, softest. CRF 23, veryfast.</summary>
    Fast,

    /// <summary>The previous hardcoded behaviour. CRF 20, faster.</summary>
    Balanced,

    /// <summary>Slowest and best. CRF 17, medium.</summary>
    High
}

/// <summary>
/// What to do when an output filename is already taken.
/// </summary>
public enum CollisionPolicy
{
    /// <summary>Show the conflict dialog. The default.</summary>
    Ask,

    /// <summary>Bump the number inside the suffix bracket and carry on.</summary>
    Increment,

    /// <summary>Replace the existing file without comment.</summary>
    Overwrite
}

/// <summary>
/// How hard the app polls MPC-HC for position, window state and focus.
/// </summary>
public enum PollSpeed
{
    /// <summary>150 ms. Snappiest view switching, most CPU.</summary>
    Responsive,

    /// <summary>300 ms. The long-standing default.</summary>
    Balanced,

    /// <summary>750 ms. Noticeably lazier, near-zero cost.</summary>
    Light
}

/// <summary>
/// How the window behaves when it is minimised or closed.
/// </summary>
public enum RunMode
{
    /// <summary>
    /// Minimises to the taskbar; closing the window exits. The conventional
    /// desktop-application behaviour, and the default.
    /// </summary>
    Application,

    /// <summary>
    /// Minimises to the notification area and stays running when the window is
    /// closed. Exit is then only available from the tray icon's menu.
    /// </summary>
    SystemTray
}

/// <summary>Which corner of the primary screen the overlay parks in.</summary>
public enum OverlayCorner
{
    TopRight,
    TopLeft,
    BottomRight,
    BottomLeft
}

public class AppSettings
{
    /// <summary>
    /// Container written by merge, trim, split and convert. Stored as a
    /// <see cref="Services.VideoFormats.Format.Key"/>, not an extension, so
    /// the codec choices behind it can change without invalidating the file.
    /// </summary>
    public string DefaultVideoFormat { get; set; } = "mp4";

    /// <summary>
    /// What to do with the source video after merge, trim, split or convert
    /// completes. Only ever applied to sources that actually succeeded.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public CleanupMode DeleteOriginalVideo { get; set; } = CleanupMode.Never;

    /// <summary>
    /// What to do with the bookmark CSV after merge, trim or split completes.
    /// Never applies to convert, which has no bookmarks of its own.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public CleanupMode DeleteBookmarksFile { get; set; } = CleanupMode.Never;

    /// <summary>
    /// Whether files this app deletes go to the Recycle Bin. On by default.
    /// </summary>
    /// <remarks>
    /// Turning it off makes every deletion unrecoverable, including the ones
    /// the cleanup settings perform without asking. Defaulting to on is what
    /// makes those settings defensible: the worst case is a trip to the bin
    /// rather than lost footage.
    /// </remarks>
    public bool DeleteToRecycleBin { get; set; } = true;

    /// <summary>Encoder effort for anything that re-encodes.</summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public EncodingQuality Quality { get; set; } = EncodingQuality.Balanced;

    /// <summary>
    /// Which H.264 encoder to use where video is re-encoded.
    /// </summary>
    /// <remarks>
    /// Defaults to software. A GPU encoder is several times faster but larger
    /// at a given quality, and it is not available on every machine — so it is
    /// something the user turns on having tested it, not something assumed.
    /// </remarks>
    public VideoEncoder VideoEncoder { get; set; } = VideoEncoder.Software;

    /// <summary>
    /// Cut exactly on the requested frame, re-encoding to do it.
    /// </summary>
    /// <remarks>
    /// Off by default because the fast path is lossless. See
    /// <see cref="FFmpegService.PreciseCuts"/> for what the trade actually is.
    /// </remarks>
    public bool PreciseCuts { get; set; }

    /// <summary>
    /// Even out loudness across written clips. Off by default: it forces a
    /// re-encode, and material that was already consistent gains nothing.
    /// </summary>
    public bool NormaliseAudio { get; set; }

    /// <summary>
    /// Which colour theme the interface uses — see <see cref="ThemePalette"/>.
    /// </summary>
    public string ThemeKey { get; set; } = ThemePalette.Graphite.Key;

    /// <summary>
    /// Pattern for output filenames — see <see cref="Helpers.NameTemplate"/>.
    /// </summary>
    /// <remarks>
    /// Defaults to <c>{name}{suffix}</c>, which is precisely what the app did
    /// before templates existed, so an untouched installation writes the same
    /// filenames it always has.
    /// </remarks>
    public string NameTemplate { get; set; } = Helpers.NameTemplate.Default;

    /// <summary>Default answer when an output filename already exists.</summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public CollisionPolicy OnNameCollision { get; set; } = CollisionPolicy.Ask;

    /// <summary>How often MPC-HC is polled.</summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public PollSpeed PollSpeed { get; set; } = PollSpeed.Balanced;

    /// <summary>
    /// Port MPC-HC's web interface listens on — Options ▸ Player ▸ Web
    /// Interface. Only used for seeking; everything else goes through window
    /// messages and works regardless.
    /// </summary>
    /// <remarks>
    /// Was a <c>const</c>. Anyone who had moved the player off 13579 had a
    /// half-working app and no way to say so.
    /// </remarks>
    public int MpcWebInterfacePort { get; set; } = 13579;

    /// <summary>
    /// Take the Web Interface port from MPC-HC's own configuration instead of
    /// <see cref="MpcWebInterfacePort"/>.
    /// </summary>
    /// <remarks>
    /// On by default, because the manual value only ever had to exist to keep
    /// two numbers in step — and the player already knows the right one.
    /// <see cref="MpcWebInterfacePort"/> is still honoured when detection finds
    /// nothing, so an unusual install is no worse off than before.
    /// </remarks>
    public bool AutoDetectMpcWebInterface { get; set; } = true;

    /// <summary>
    /// Folder holding ffmpeg.exe and ffprobe.exe. Empty means search the usual
    /// places — beside the executable, an <c>ffmpeg</c> subfolder, then PATH.
    /// </summary>
    public string FfmpegFolder { get; set; } = "";

    /// <summary>
    /// Whether the window minimises to the notification area and survives
    /// being closed. Defaults to a plain application, which is what someone
    /// who has never opened Settings will expect.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public RunMode RunMode { get; set; } = RunMode.Application;

    /// <summary>
    /// Whether more than one copy of this install can run at the same time.
    /// Off by default — a second launch instead hands off to the copy that
    /// is already running. See <see cref="SingleInstanceService"/>.
    /// </summary>
    public bool AllowMultipleInstances { get; set; } = false;

    /// <summary>Whether the hotkey confirmation toast appears at all.</summary>
    public bool ToastsEnabled { get; set; } = true;

    /// <summary>How long a toast holds before fading, in seconds.</summary>
    public double ToastSeconds { get; set; } = 2.2;

    /// <summary>
    /// When set, the "Save to" folder survives a restart instead of reverting
    /// to the loaded video's own folder.
    /// </summary>
    public bool RememberSaveToFolder { get; set; }

    /// <summary>Persisted "Save to" folder, used only when the above is set.</summary>
    public string SaveToFolder { get; set; } = "";

    /// <summary>Corner of the primary screen the minimal overlay parks in.</summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public OverlayCorner OverlayCorner { get; set; } = OverlayCorner.TopRight;

    /// <summary>
    /// Overlay background opacity, 0.3–1.0. Opaque by default — at 0.75 the
    /// text competed with whatever was playing behind it.
    /// </summary>
    public double OverlayOpacity { get; set; } = 1.0;

    public string QuickSaveFolder { get; set; } = "";
    public string PlaylistFolder { get; set; } = "";
    public List<string> RecentVideos { get; set; } = new();
    public string? KeyboardHotkey { get; set; } // e.g. "F8"
    public bool MiddleMouseHotkeyEnabled { get; set; } = true;
    /// <summary>
    /// Maximum number of entries retained in <see cref="RecentVideos"/>.
    /// Defaults to 10 — the "last 10 videos played" shown in the File menu.
    /// </summary>
    public int MaxHistory { get; set; } = 10;

    /// <summary>
    /// The user-configurable hotkey that sets a bookmark timestamp
    /// (start, then end). Defaults to middle mouse button. Persisted as a
    /// short string like <c>"MButton"</c> or <c>"Ctrl+Shift+T"</c>.
    /// Migrated from the legacy <see cref="MiddleMouseHotkeyEnabled"/> +
    /// <see cref="KeyboardHotkey"/> fields on first load if unset.
    /// </summary>
    public string? TimestampHotkey { get; set; }

    /// <summary>
    /// User-defined folder shortcuts shown in the Shortcuts menu. Clicking
    /// one opens that folder in Explorer. Entirely separate from
    /// <see cref="QuickSaveShortcuts"/> — these never change any output path.
    /// </summary>
    public List<ShortcutEntry> Shortcuts { get; set; } = new();

    /// <summary>
    /// Folder shortcuts listed in the File menu. Clicking one sets
    /// <see cref="QuickSaveFolder"/> — the destination for the one-click
    /// split button on each bookmark row. Separate list from
    /// <see cref="Shortcuts"/>, which only opens folders.
    /// </summary>
    public List<ShortcutEntry> QuickSaveShortcuts { get; set; } = new();

    /// <summary>
    /// User-defined filename suffixes appended to all video operation
    /// outputs (merge, split, convert, strip audio, bulk merge). Each
    /// entry's <see cref="SuffixEntry.Text"/> is alphanumeric only and
    /// wrapped in brackets when applied (e.g. <c>[done]</c>). The
    /// <see cref="ActiveSuffixText"/> field tracks which one is currently
    /// selected. Defaults to a single <c>"done"</c> entry, active.
    /// </summary>
    public List<SuffixEntry> Suffixes { get; set; } = new();

    /// <summary>
    /// When set, the window follows the bookmark file: loading one drops to
    /// the minimal overlay, and unloading it returns to the full window.
    /// </summary>
    public bool AutoSwitchViews { get; set; }

    /// <summary>
    /// The <see cref="SuffixEntry.Text"/> of the currently-active suffix.
    /// Null/empty if no suffix is active (falls back to the first entry
    /// in <see cref="Suffixes"/>, or to <c>"done"</c> if the list is empty).
    /// </summary>
    public string? ActiveSuffixText { get; set; }
}

public class SettingsService
{
    private readonly string _path;
    public AppSettings Current { get; private set; } = new();

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        Converters =
        {
            new ShortcutEntryJsonConverter(),
            new HotkeyBindingJsonConverter(),
            new SuffixEntryJsonConverter()
        }
    };

    public SettingsService()
    {
        // Beside the executable, not %APPDATA% — the install is one folder.
        var dir = PortablePaths.AppFolder;
        Directory.CreateDirectory(dir);
        _path = Path.Combine(dir, "settings.json");

        MigrateFromAppDataIfNeeded();
        Load();
    }

    /// <summary>
    /// Copies a pre-portable %APPDATA% settings file next to the executable
    /// the first time this build runs, so shortcuts, naming tags and history
    /// survive the move. Only ever copies — the original is left in place.
    /// </summary>
    private void MigrateFromAppDataIfNeeded()
    {
        if (File.Exists(_path)) return;

        try
        {
            var legacy = Path.Combine(PortablePaths.LegacyAppDataFolder, "settings.json");
            if (File.Exists(legacy)) File.Copy(legacy, _path);
        }
        catch
        {
            // Starting from defaults is a fine outcome; never block startup.
        }
    }

    public void Load()
    {
        try
        {
            if (File.Exists(_path))
            {
                var json = File.ReadAllText(_path);
                Current = JsonSerializer.Deserialize<AppSettings>(json, _jsonOptions) ?? new AppSettings();
            }
        }
        catch
        {
            Current = new AppSettings();
        }

        // Normalise the output container up front: a settings file written by
        // an older build has no key at all, and a hand-edited one may name a
        // format that no longer exists. Either way every later read of this
        // field can then be taken at face value.
        Current.DefaultVideoFormat = VideoFormats.FromKey(Current.DefaultVideoFormat).Key;

        // Same reasoning for the numeric settings: clamp once here so no
        // caller has to defend against a settings.json that says the port is
        // 0 or the overlay is invisible.
        ClampNumericSettings();

        // Older settings files (or a user-lowered MaxHistory) may have
        // more entries than the current cap. Trim once on load so the
        // File → Recent submenu never shows more than MaxHistory items.
        if (Current.RecentVideos.Count > Current.MaxHistory)
            Current.RecentVideos = Current.RecentVideos.Take(Current.MaxHistory).ToList();

        // One-time migration from the legacy MiddleMouseHotkeyEnabled /
        // KeyboardHotkey fields into the unified TimestampHotkey setting.
        // If TimestampHotkey is already set (either from a newer
        // settings.json or from a prior migration), leave it alone — we
        // never want to clobber a user's explicit choice.
        if (string.IsNullOrWhiteSpace(Current.TimestampHotkey))
        {
            if (!string.IsNullOrWhiteSpace(Current.KeyboardHotkey))
            {
                // A keyboard hotkey was configured in the old format —
                // promote it to the new TimestampHotkey field. (This
                // replaces MButton with the keyboard combo, matching the
                // old app's "both fire the same action" behavior — but
                // since the new model only supports one hotkey, we pick
                // the keyboard one because the user explicitly set it.)
                Current.TimestampHotkey = Current.KeyboardHotkey;
            }
            else if (Current.MiddleMouseHotkeyEnabled)
            {
                Current.TimestampHotkey = HotkeyBinding.DefaultMouse.ToSettingsString();
            }
            else
            {
                Current.TimestampHotkey = HotkeyBinding.None.ToSettingsString();
            }

            // Persist the migration so we don't redo it next launch.
            try { Save(); } catch { /* ignore */ }
        }

        // Suffix migration: ensure the user always has at least one
        // suffix and an active selection. On first launch (or when
        // upgrading from a settings.json that predates the suffix
        // feature), seed the list with the default "done" entry.
        if (Current.Suffixes == null || Current.Suffixes.Count == 0)
        {
            Current.Suffixes = new List<SuffixEntry> { new SuffixEntry("done") };
            Current.ActiveSuffixText = "done";
            try { Save(); } catch { /* ignore */ }
        }
        else if (string.IsNullOrWhiteSpace(Current.ActiveSuffixText) ||
                 !Current.Suffixes.Any(s => string.Equals(s.Text, Current.ActiveSuffixText, StringComparison.OrdinalIgnoreCase)))
        {
            // Active suffix is missing or doesn't match any entry —
            // fall back to the first one in the list.
            Current.ActiveSuffixText = Current.Suffixes[0].Text;
            try { Save(); } catch { /* ignore */ }
        }
    }

    public void Save()
    {
        try
        {
            // The settings dialog validates its own input, but commands and
            // migrations write here too — clamping on the way out means the
            // file on disk is always in range.
            ClampNumericSettings();

            var json = JsonSerializer.Serialize(Current, _jsonOptions);
            File.WriteAllText(_path, json);
        }
        catch { /* ignore */ }
    }

    public void AddRecent(string videoPath)
    {
        if (string.IsNullOrWhiteSpace(videoPath)) return;
        Current.RecentVideos.RemoveAll(p => string.Equals(p, videoPath, StringComparison.OrdinalIgnoreCase));
        Current.RecentVideos.Insert(0, videoPath);
        if (Current.RecentVideos.Count > Current.MaxHistory)
            Current.RecentVideos = Current.RecentVideos.Take(Current.MaxHistory).ToList();
        Save();
    }

    /// <summary>
    /// Removes a single entry from the recent videos list (case-insensitive).
    /// Used when the user clicks a recent entry whose file no longer exists,
    /// or removes an entry manually from the File → Recent submenu.
    /// </summary>
    public bool RemoveRecent(string? videoPath)
    {
        if (string.IsNullOrWhiteSpace(videoPath)) return false;
        int removed = Current.RecentVideos.RemoveAll(p =>
            string.Equals(p, videoPath, StringComparison.OrdinalIgnoreCase));
        if (removed > 0) Save();
        return removed > 0;
    }

    /// <summary>
    /// Clears the entire recent videos list. Called by the
    /// "Clear recent list" entry in the File → Recent submenu.
    /// </summary>
    public void ClearRecents()
    {
        if (Current.RecentVideos.Count == 0) return;
        Current.RecentVideos.Clear();
        Save();
    }

    /// <summary>
    /// Returns the parsed timestamp hotkey from settings. Falls back to
    /// <see cref="HotkeyBinding.DefaultMouse"/> if the settings string
    /// is missing or unparseable, so the hotkey feature never breaks.
    /// </summary>
    public HotkeyBinding GetTimestampHotkey()
    {
        var raw = Current.TimestampHotkey;
        if (string.IsNullOrWhiteSpace(raw)) return HotkeyBinding.DefaultMouse;
        var parsed = HotkeyBinding.Parse(raw);
        // Parse() returns None on failure, but a missing string should
        // default to MButton, not disabled. Keep None if the user
        // explicitly chose "disabled".
        return parsed;
    }

    /// <summary>
    /// Persists a new timestamp hotkey binding.
    /// </summary>
    public void SetTimestampHotkey(HotkeyBinding binding)
    {
        Current.TimestampHotkey = binding.ToSettingsString();
        // Mirror into the legacy fields too so old code paths and any
        // external tooling reading settings.json still see a consistent
        // state. These fields are no longer used by the app itself.
        Current.MiddleMouseHotkeyEnabled = binding.Kind == HotkeyBinding.HotkeyKind.Mouse
            && binding.Mouse == HotkeyBinding.MouseButtonKind.MButton;
        Current.KeyboardHotkey = binding.Kind == HotkeyBinding.HotkeyKind.Keyboard
            ? binding.ToSettingsString()
            : null;
        Save();
    }

    /// <summary>
    /// Adds a folder shortcut. Returns true if added, false if it was
    /// already present (duplicates are silently ignored).
    /// </summary>
    public bool AddShortcut(string folderPath, string? name = null)
    {
        if (string.IsNullOrWhiteSpace(folderPath)) return false;
        var normalized = folderPath.TrimEnd('\\', '/') + "\\";
        if (Current.Shortcuts.Any(s => string.Equals(
                (s.Path ?? "").TrimEnd('\\', '/'), folderPath.TrimEnd('\\', '/'), StringComparison.OrdinalIgnoreCase)))
            return false;
        Current.Shortcuts.Add(new ShortcutEntry(normalized, name));
        Save();
        return true;
    }

    /// <summary>
    /// Removes a folder shortcut by exact (case-insensitive) path match.
    /// Returns true if a shortcut was removed.
    /// </summary>
    public bool RemoveShortcut(string? folderPath)
    {
        if (string.IsNullOrWhiteSpace(folderPath)) return false;
        int removed = Current.Shortcuts.RemoveAll(s => string.Equals(
            (s.Path ?? "").TrimEnd('\\', '/'), folderPath.TrimEnd('\\', '/'), StringComparison.OrdinalIgnoreCase));
        if (removed > 0) Save();
        return removed > 0;
    }

    /// <summary>
    /// Renames the shortcut whose path matches <paramref name="folderPath"/>
    /// (case-insensitive). Returns true if a shortcut was renamed.
    /// </summary>
    public bool RenameShortcut(string? folderPath, string newName)
    {
        if (string.IsNullOrWhiteSpace(folderPath)) return false;
        var trimmedName = (newName ?? "").Trim();
        if (string.IsNullOrEmpty(trimmedName)) return false;

        var entry = Current.Shortcuts.FirstOrDefault(s => string.Equals(
            (s.Path ?? "").TrimEnd('\\', '/'), folderPath.TrimEnd('\\', '/'), StringComparison.OrdinalIgnoreCase));
        if (entry == null) return false;

        entry.Name = trimmedName;
        Save();
        return true;
    }

    /// <summary>
    /// Replaces the current shortcut list with <paramref name="reordered"/>
    /// in the given order and persists. Used by the Manage Shortcuts dialog
    /// when the user drag-reorders the list.
    /// </summary>
    public void ReorderShortcuts(IEnumerable<ShortcutEntry> reordered)
    {
        Current.Shortcuts = reordered.ToList();
        Save();
    }

    /// <summary>
    /// Rewrites a saved filename pattern that still uses the 4.0 variable
    /// names.
    /// </summary>
    /// <remarks>
    /// <c>{index}</c> and <c>{index2}</c> shipped in 4.0 and were renamed to
    /// <c>{number}</c> and <c>{number2}</c> because nothing about "index2" said
    /// what made it different. Both still expand, so this changes nothing about
    /// the filenames produced — it exists so the Settings box shows a pattern
    /// built from names that are actually in the list beneath it, rather than
    /// two that quietly still work but are documented nowhere.
    /// </remarks>
    private void MigrateNameTemplate()
    {
        var template = Current.NameTemplate;
        if (string.IsNullOrEmpty(template)) return;
        if (!template.Contains("{index", StringComparison.Ordinal)) return;

        // Longest first: replacing {index} before {index2} would leave a "2".
        Current.NameTemplate = template
            .Replace("{index2}", "{number2}", StringComparison.Ordinal)
            .Replace("{index}", "{number}", StringComparison.Ordinal);
    }

    /// <summary>
    /// Forces every numeric setting into a range the app can actually work
    /// with. Called on load and again on save.
    /// </summary>
    private void ClampNumericSettings()
    {
        MigrateNameTemplate();

        // 1–65535 is the whole legal port space; anything outside it cannot be
        // what the user meant, so fall back rather than guess.
        if (Current.MpcWebInterfacePort is < 1 or > 65535)
            Current.MpcWebInterfacePort = 13579;

        // Below 1 the recent list is pointless; the File menu stops being a
        // menu somewhere above 50.
        Current.MaxHistory = Math.Clamp(Current.MaxHistory, 1, 50);

        // Under half a second a toast cannot be read; over ten it is in the way.
        Current.ToastSeconds = Math.Clamp(Current.ToastSeconds, 0.5, 10.0);

        // Fully transparent would be an invisible overlay reported as broken.
        Current.OverlayOpacity = Math.Clamp(Current.OverlayOpacity, 0.3, 1.0);
    }

    // ------------------------------------------------------------------
    // Derived settings
    // ------------------------------------------------------------------

    /// <summary>
    /// ffmpeg output flags for the configured quality, used wherever video is
    /// actually re-encoded.
    /// </summary>
    /// <remarks>
    /// Rate control only; which encoder those flags belong to is
    /// <see cref="Current"/>'s <see cref="AppSettings.VideoEncoder"/>, and the
    /// two must be applied together — a CRF handed to NVENC is rejected
    /// outright. Returned without a trailing space; callers join with one.
    /// </remarks>
    public string GetQualityArgs() =>
        VideoEncoders.QualityArgsFor(Current.VideoEncoder, Current.Quality);

    /// <summary>Poll interval for the MPC-HC watcher.</summary>
    public TimeSpan GetPollInterval() => Current.PollSpeed switch
    {
        PollSpeed.Responsive => TimeSpan.FromMilliseconds(150),
        PollSpeed.Light => TimeSpan.FromMilliseconds(750),
        _ => TimeSpan.FromMilliseconds(300)
    };

    // ------------------------------------------------------------------
    // Output format
    // ------------------------------------------------------------------

    /// <summary>
    /// The container merge, trim, split and convert write. Always returns a
    /// usable format — an unknown key resolves to MP4.
    /// </summary>
    public VideoFormats.Format GetDefaultVideoFormat() =>
        VideoFormats.FromKey(Current.DefaultVideoFormat);

    /// <summary>Persists the default output container by key.</summary>
    public void SetDefaultVideoFormat(string? key)
    {
        Current.DefaultVideoFormat = VideoFormats.FromKey(key).Key;
        Save();
    }

    // ------------------------------------------------------------------
    // Suffix management
    // ------------------------------------------------------------------

    /// <summary>
    /// Returns the active suffix text. Falls back to the first entry's
    /// text if <see cref="AppSettings.ActiveSuffixText"/> is unset or
    /// doesn't match any entry, or to <c>"done"</c> if the list is empty.
    /// Never returns null/empty — always guarantees a usable suffix.
    /// </summary>
    public string GetActiveSuffixText()
    {
        if (Current.Suffixes.Count == 0) return "done";

        if (!string.IsNullOrWhiteSpace(Current.ActiveSuffixText) &&
            Current.Suffixes.Any(s => string.Equals(s.Text, Current.ActiveSuffixText, StringComparison.OrdinalIgnoreCase)))
            return Current.ActiveSuffixText!;

        return Current.Suffixes[0].Text;
    }

    /// <summary>
    /// Sets the active suffix by text (case-insensitive). The text must
    /// match an existing entry. Returns true if set successfully.
    /// </summary>
    public bool SetActiveSuffix(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return false;
        var match = Current.Suffixes.FirstOrDefault(s =>
            string.Equals(s.Text, text, StringComparison.OrdinalIgnoreCase));
        if (match == null) return false;
        Current.ActiveSuffixText = match.Text;
        Save();
        return true;
    }

    /// <summary>
    /// Adds a new suffix. Returns true if added, false if the text was
    /// empty, invalid, or already present (duplicates are rejected).
    /// The caller is responsible for pre-validating (alphanumeric, ≤50 chars).
    /// </summary>
    public bool AddSuffix(string text)
    {
        var trimmed = (text ?? "").Trim();
        if (string.IsNullOrEmpty(trimmed)) return false;
        if (Current.Suffixes.Any(s => string.Equals(s.Text, trimmed, StringComparison.OrdinalIgnoreCase)))
            return false;
        Current.Suffixes.Add(new SuffixEntry(trimmed));
        Save();
        return true;
    }

    /// <summary>
    /// Removes a suffix by text (case-insensitive). If the removed suffix
    /// was the active one, the active selection falls back to the first
    /// remaining entry (or "done" if the list is now empty). Returns true
    /// if a suffix was removed.
    /// </summary>
    public bool RemoveSuffix(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return false;
        int removed = Current.Suffixes.RemoveAll(s =>
            string.Equals(s.Text, text, StringComparison.OrdinalIgnoreCase));
        if (removed == 0) return false;

        // Re-validate the active selection.
        if (string.IsNullOrWhiteSpace(Current.ActiveSuffixText) ||
            !Current.Suffixes.Any(s => string.Equals(s.Text, Current.ActiveSuffixText, StringComparison.OrdinalIgnoreCase)))
        {
            Current.ActiveSuffixText = Current.Suffixes.Count > 0
                ? Current.Suffixes[0].Text
                : "done";
        }
        Save();
        return true;
    }

    /// <summary>
    /// Renames a suffix by its current text (case-insensitive). Returns
    /// true if renamed. If the renamed suffix was the active one, the
    /// active selection is updated to the new text.
    /// </summary>
    public bool RenameSuffix(string? oldText, string newText)
    {
        if (string.IsNullOrWhiteSpace(oldText)) return false;
        var trimmed = (newText ?? "").Trim();
        if (string.IsNullOrEmpty(trimmed)) return false;

        // Reject if the new text collides with a different entry.
        if (Current.Suffixes.Any(s =>
            !string.Equals(s.Text, oldText, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(s.Text, trimmed, StringComparison.OrdinalIgnoreCase)))
            return false;

        var entry = Current.Suffixes.FirstOrDefault(s =>
            string.Equals(s.Text, oldText, StringComparison.OrdinalIgnoreCase));
        if (entry == null) return false;

        bool wasActive = string.Equals(Current.ActiveSuffixText, entry.Text, StringComparison.OrdinalIgnoreCase);
        entry.Text = trimmed;
        if (wasActive) Current.ActiveSuffixText = trimmed;
        Save();
        return true;
    }

    /// <summary>
    /// Replaces the current suffix list with <paramref name="reordered"/>
    /// in the given order and persists. Used by the Manage Suffixes dialog
    /// when the user drag-reorders the list. Preserves the active selection
    /// (matched by text, case-insensitive).
    /// </summary>
    public void ReorderSuffixes(IEnumerable<SuffixEntry> reordered)
    {
        var wasActive = Current.ActiveSuffixText;
        Current.Suffixes = reordered.ToList();

        // Re-validate active after reorder.
        if (string.IsNullOrWhiteSpace(wasActive) ||
            !Current.Suffixes.Any(s => string.Equals(s.Text, wasActive, StringComparison.OrdinalIgnoreCase)))
        {
            Current.ActiveSuffixText = Current.Suffixes.Count > 0
                ? Current.Suffixes[0].Text
                : "done";
        }
        Save();
    }
}
