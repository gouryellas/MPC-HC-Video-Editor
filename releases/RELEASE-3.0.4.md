# MPC-HC Video Editor 3.0.4

Two fixes about output landing where it was asked to, and a prompt staying
reachable while it waits. Plus one thing from 3.0.3 taken back out.

## Fixes

**Split made a folder nobody asked for.** It created `<name>_clips` next to the
save-to folder and wrote the clips in there, so Split was the one operation
whose output did not appear where the destination pointed — and a single cut
left a folder behind for one file. Clips now go straight into **Save to**, like
merge, convert and strip audio.

Nothing is lost with the folder gone: the clips are named `[done]`, `[done2]`,
`[done3]`… by index, so they cannot collide with each other, and a name already
taken by something else goes through the same collision prompt as everything
else.

**The view switched away from a prompt that was waiting for an answer.** A modal
loop still pumps messages, so the poll behind "switch views automatically" kept
running while "this file already exists" sat unanswered. Clicking the player was
then enough to drop to the overlay, which hid the main window and the prompt
with it — and because the prompt is modal, the app accepted input nowhere and
there was nothing left on screen to answer. Alt+Tab was the only way back.

The view no longer moves while any prompt is open: whatever is showing when one
appears is what stays until it is answered, whether the overlay was pinned or
the setting was on. The "file exists" and rename dialogs are also on top of
other windows now, so clicking away no longer buries them.

## Changed

**Merge is called Merge again.** 3.0.3 renamed it to Extract with a single cut.
That was wrong: the command is always available precisely because it does more
than join cuts — with no video and no bookmarks it opens a file picker and joins
whatever is chosen — so a label driven by the current cut count described only
one of the three things it does. With a single pair, Split and Merge both
process it, and either is a reasonable thing to click.

## Requirements

- Windows, .NET 8 desktop runtime
- MPC-HC with the web interface enabled (Options ▸ Player ▸ Web Interface)
- ffmpeg and ffprobe, beside the executable or on PATH. WEBM output needs a
  build with libvpx and libopus.
