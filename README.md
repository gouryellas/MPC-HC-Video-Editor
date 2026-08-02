# MPC-HC Video Editor 3.0 (C# / WPF)

Modern rewrite of the original AutoHotkey **MPC-HC Video Editor v2.1**.

## Current features

- Automatic detection of the video open in MPC-HC
- Live current time / duration display (reads status bar)
- Bookmark system (Start → End) with incomplete state
- Per-bookmark **speed slider** (0.25x – 2.0x)
- Vertical **flip** toggle
- Multi-select with checkboxes
- **Merge** selected (or all) bookmarks → one MP4 (respects speed + flip)
- **Split** selected bookmarks → individual clips
- Convert any video(s) → H.264/AAC MP4
- Strip audio → high-quality MP3
- CSV load/save (compatible with original format)
- Dark modern UI + status bar with progress
- Menu bar + toolbar
- **Click a timestamp to seek MPC-HC** — start and end times in the bookmark
  list are clickable hyperlinks that jump the player to that moment (drives
  MPC-HC's seek bar directly via Win32, no web interface required)
- **Open video / Open bookmark** entries in the File menu for picking the
  source video or a saved CSV directly (no need to wait for MPC-HC)
- **Configurable timestamp hotkey** — pick any mouse button (Middle / Side 1
  / Side 2) or any keyboard combination (e.g. `Ctrl+Shift+T`, `F8`) as the
  hotkey that sets a bookmark timestamp. Opens a press-to-capture dialog
  from the Hotkey menu. Defaults to middle mouse button. Persists across
  restarts via `settings.json`, with one-time migration from the legacy
  `MiddleMouseHotkeyEnabled` / `KeyboardHotkey` fields.
- **Filename suffix** — all video operations (merge, split, convert, strip
  audio, bulk merge) append the active suffix to their output filenames,
  wrapped in brackets. Default is `[done]`, so `vacation.mp4` →
  `vacation[done].mp4`. If that file already exists, the system
  auto-increments: `vacation[done2].mp4`, `vacation[done3].mp4`, …
  Split follows the same linear format: clip 1 = `[done]`, clip 2 =
  `[done2]`, clip 3 = `[done3]`, etc. Manage suffixes via the Suffix menu:
  - The menu header shows **"Current: [done]"** so you always see which
    suffix is active.
  - Click any suffix in the list to make it active (marked with ✓).
  - **Add** new suffixes (alphanumeric only, max 50 chars).
  - **Rename** and **Remove** via Suffix → "Manage suffixes…" (drag to
    reorder, double-click to rename).
  - Persists across restarts via `settings.json`.
- **Folder shortcuts** — add frequently-used folders via the Shortcuts menu
  and they appear as clickable items at the bottom of the File menu. Each
  shortcut opens its folder in Explorer with one click. Shortcuts persist
  across restarts via `settings.json`.
  - **Rename** any shortcut (Shortcuts → hover → "Rename…", or via the
    Manage Shortcuts dialog) so the menu shows a friendly label instead
    of the raw folder name.
  - **Drag-reorder** shortcuts in Shortcuts → "Manage shortcuts…"
    (drag rows up/down, or use the ↑ / ↓ buttons). The order you set is
    reflected in the File menu immediately and saved to `settings.json`.
  - Backward compatible: existing `settings.json` files written by the
    previous `List<string>` format are auto-migrated on load.
- **Recent videos** — File → Recent shows the last 10 videos you played,
  most-recent-first. Click an entry (or its "Open" sub-item) to **launch
  that video in MPC-HC and start playback** — the editor's bookmarks and
  metadata load instantly while MPC-HC opens the file in the foreground.
  Hover → "Remove from list" to delete a single entry without opening it;
  "Clear recent list" wipes the whole list. Dead entries (file moved or
  deleted) are auto-pruned the first time you click them. Persists across
  restarts via `settings.json`.
- **Undo last bookmark** (Bookmarks menu) — removes the most recently
  added bookmark from both the GUI and the CSV file, with proper
  renumbering. If the removed bookmark was the in-progress one (start
  set, end not yet captured), also clears the awaiting-end state so the
  next hotkey press starts a fresh bookmark instead of trying to complete
  one that no longer exists.
- **Clear bookmarks (two flavors)** —
  - **Clear all (GUI only)…** clears the in-memory bookmark list and
    writes out an empty CSV (file remains on disk).
  - **Delete CSV file…** deletes the CSV file from disk **and** clears
    the in-memory list — the bookmarks are truly gone. The session's CSV
    path is preserved so future bookmarks save to a freshly-created CSV
    at the same path next to the video.
- **Play all cuts** (Bookmarks menu) — sequentially plays every valid cut
  in the active video: seeks MPC-HC to each bookmark's start, calls
  Play(), waits for the cut's effective duration (DurationSeconds ÷
  Speed, so a 2x bookmark plays for half as long), then seeks to the
  next bookmark's start, and so on. Incomplete bookmarks are skipped.
  While the loop is running the status bar shows
  `"Playing all cut 3/7: 1:23 → 1:45  (22s at 1x)"` so you can track
  progress. The polling timer keeps running so the live time display
  continues to update.
- **Play selected cuts** (Bookmarks menu) — same as Play all but only
  plays the bookmarks you've checked. Falls back to "nothing to play"
  if none are selected — does **not** auto-expand to all bookmarks like
  the merge command does, because for playback the user's checkmarks
  are the whole point.
- **Stop playback** (Bookmarks menu) — cancels any in-progress Play all
  / Play selected loop and pauses MPC-HC. The menu item is disabled
  (greyed out) when no playback is running, so you can't accidentally
  click it.
- **Reset everything** (File menu) — restarts the application process.
  Saves the current bookmark CSV first, then launches a fresh instance
  and shuts this one down. Settings, recent videos, suffixes, and
  shortcuts all persist across the restart (they live in
  `settings.json`). Useful for clearing transient state (mid-bookmark,
  error states, a stuck polling loop) without manually closing and
  reopening the app.

## How to build

```bash
cd MpcHcVideoEditor
dotnet restore
dotnet build -c Release
dotnet run
```

**Requirements**
- .NET 8 SDK
- `ffmpeg.exe` + `ffprobe.exe` next to the executable **or** in `%AppData%\MPC-HC Video Editor\`

## Project layout

```
Models/          Bookmark, EditSession
Services/        FFmpegService, MpcHcService, BookmarkService
ViewModels/      MainViewModel (all commands)
Views/           MainWindow.xaml
Helpers/         Converters
```

## Still missing (easy to add next)

- Timeline scrubber
- Playlist (.pls) support
- Screenshot capture of MPC-HC
- Better conflict / overwrite dialogs

Just say which feature you want next.
