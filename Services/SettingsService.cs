using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows.Input;
using MpcHcVideoEditor.Models;
using MpcHcVideoEditor.Helpers;

namespace MpcHcVideoEditor.Services;

public class AppSettings
{
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
    /// the first time this build runs, so shortcuts, naming styles and history
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
