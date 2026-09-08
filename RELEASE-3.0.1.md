# MPC-HC Video Editor 3.0.1

A maintenance release. Several long-standing bugs turned out to share one
cause — the same fact stored in more than one place — and the fixes remove the
duplicate state rather than adding more of it.

## Fixes

**Timestamps went to the wrong bookmark.** Pressing the hotkey to close
bookmark 1 could open bookmark 2 instead. Whether a bookmark was open was
recorded in three places at once: a `_awaitingEnd` flag in the ViewModel
(assigned from twelve places), an `IsIncomplete` flag on the bookmark, and the
end time itself. They drifted, and a bookmark could claim to be complete while
holding an end time of zero — rendering as `6:11 → 0:00 (0s)`. Both flags are
gone; a bookmark is open when its end time is not after its start, computed
rather than stored. Existing CSV files with the bad rows now load correctly
without editing.

**The program kept running after its window closed.** The overlay and the
toast are shown once and thereafter only hidden, so under WPF's default
`ShutdownMode.OnLastWindowClose` closing the main window was never the last
close. `ShutdownMode` is now `OnMainWindowClose`, the helper windows are closed
explicitly, and `MainViewModel.Dispose` — which nothing had ever called — is
called, so the input hooks, timers and any running ffmpeg stop too.

**The minimal overlay flickered between two states.** `AllowsTransparency`
makes it a layered window, and `SizeToContent` meant its height changed with
the bookmark list; a layered window that changes height does not reliably
repaint what it vacated, so the previous frame stayed painted underneath. Fixed
height with a scrolling list.

**View ▸ Minimal appeared to do nothing.** Choosing it necessarily happens from
the main window, so the player is not focused at that moment, and the
focus-follow rule put the window straight back a tick later.

**Deleting a source file after an operation failed.** Two causes: ffmpeg's
`Exited` event fires before its file handles are released, and MPC-HC holds an
open handle on whatever it is playing. The app now waits for the process to
flush, closes the media in the player (leaving the player running), and retries
while the file is genuinely in use.

**The one-click split on each bookmark row always proposed the same filename,**
so every click after the first reported the file as already existing. It now
walks `[done]`, `[done2]`, `[done3]`… silently, as it always claimed to.

**Merge required two cuts.** A single cut is now a legitimate job — a trim —
and runs down the same path.

## New

- **Settings** (File ▸ Settings), in five tabs: output container, encoding
  quality, filename-collision policy, post-operation cleanup, MPC-HC web
  interface port, ffmpeg folder, poll rate, recent-history size, toasts,
  overlay corner and opacity.
- **Run in system tray** — minimises to the notification area and survives the
  window being closed; exit from the tray menu. The alternative, **run as
  application**, is the default and behaves conventionally.
- **Output formats** beyond MP4: MKV, MOV, AVI, WMV, MPG, MPEG, WEBM. MP4, MKV
  and MOV are written by copying the cut segments, so they are lossless and
  fast; the others cannot hold H.264 and re-encode with codecs appropriate to
  the container.
- **Post-operation cleanup** — optionally delete the source video and/or the
  bookmark file after merge, trim, split or convert, either on confirmation or
  silently. Everything goes to the Recycle Bin.
- **Help menu** with About, licensing and links to the repository.
- **Application and tray icons.**
- **Spaces in filenames are converted to dashes automatically** instead of
  prompting. Other disallowed characters still ask.

## Changed

- View switching follows **focus** rather than whether the player is
  fullscreen. Leaving MPC-HC always restores the main window; returning to it
  drops to the overlay when following is on.
- **View ▸ Minimal is never disabled,** and choosing it turns automatic
  switching off.
- "Switch views automatically" moved from Options to the View menu.
- Deletions throughout go to the Recycle Bin rather than being unrecoverable.

## Requirements

- Windows, .NET 8 desktop runtime
- MPC-HC with the web interface enabled (Options ▸ Player ▸ Web Interface)
- ffmpeg and ffprobe, beside the executable or on PATH. WEBM output needs a
  build with libvpx and libopus.
