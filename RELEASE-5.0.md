# MPC-HC Video Editor 5.0

Rotate and mute have not worked since they were added. They do now. The timeline
has been rebuilt so the playback position is visible, the side panel says what
the timestamp hotkey is, and the program no longer leaves ffmpeg processes
running behind it.

## Rotate and Mute did nothing at all

Both buttons have been greyed out since 4.6, in every version, no matter what
was selected. Not sometimes — always. Neither feature has been reachable since
the day it shipped.

They share their enabling rule with **Flip**: all three are available when at
least one complete cut is ticked. The rule was right, and Flip obeyed it. The
other two were never told to re-check it. Nothing else offers rotation or mute,
so the entire feature sat behind two buttons that could not be pressed, and the
setting written into the bookmark file could only ever be `None`.

Tick a cut and all three now light up together. A rotation still cycles a
quarter turn at a time — right, upside down, left, back to normal — and applies
to every ticked cut at once.

## The program left ffmpeg processes running

Selecting a bookmark renders the first and last frame of it for the panel on the
right. Moving to another row before those finish cancels them, which is normal
and happens constantly — it is one click to the next row.

A cancelled render abandoned its ffmpeg. It was never ended, only let go of: it
stayed in memory, blocked on a pipe nothing was reading any more, holding the
source video open, until the program was closed. One was found alive after three
minutes having used a tenth of a second of processor time. Clicking down a long
list left one behind per row.

A file held open cannot be deleted or renamed, which matters here more than it
might elsewhere: **Settings ▸ Cleanup** can be set to remove the original video
after an operation, and that is a deletion the program performs on its own.

Every short-lived helper — thumbnails, the waveform, the encoder probes at
startup — now ends its process on the way out however it leaves, including when
ffmpeg on PATH is a shim that runs the real binary as a child.

## The window could stop responding and look like a crash

**Hotkey ▸ Set timestamp hotkey…** opens a window that listens for the key you
press. Like every dialog, it disables the main window while it is up. Unlike a
dialog, it was created belonging to nothing, and Windows keeps a dialog in front
of its owner — with no owner there was nothing to keep in front of anything.

Click the taskbar button while it was open and the main window came forward,
still disabled, with the window that disabled it now hidden behind. The result
is a program that ignores every click and every key, with nothing on screen
explaining why. It is indistinguishable from a hang.

All five dialogs that were built this way now belong to the window they
interrupt, so they stay in front of it and cannot be lost behind it.

## Alt and a letter no longer belongs to the menus

Every menu had a letter — Alt+F for File, Alt+B for Bookmarks, and so on. Two of
them claimed the same letter: Hotkey and Help both answered to Alt+H, so Alt+H
opened neither and only moved the highlight between them.

Rather than reassign it, the shortcuts are gone entirely. The timestamp hotkey
can be bound to any combination you like, and a combination that collided with a
menu went to the menu bar instead of to the bookmark — a rule nobody should have
to know when choosing a hotkey. Nothing is lost: every menu still opens with the
mouse, and the one keyboard action worth having is the hotkey itself, which is
global and works while the player has focus.

## The timeline

The bar under the toolbar drew the playback position and the bookmark ranges on
top of each other, in eighteen pixels, with overlapping margins. The position
line was six pixels tall, sat behind the range marks, and disappeared underneath
them — which on a well-marked video is most of the bar.

They now have a lane each: the range marks in a strip along the top, the
playback position in a taller rounded bar beneath, with the waveform behind it.
Nothing is drawn over anything else, and the position is legible at a glance
from across the desk.

## An open bookmark is now just its start time

A bookmark waiting for its second timestamp displayed as `[3] 40s - 0s (0s)`,
which reads as a cut that begins at forty seconds and runs backwards to nothing.
It has no end and no length, so it no longer pretends to: the row is the start
time and the word `incomplete`, and the rest appears when the closing timestamp
does. The compact overlay follows the same rule.

## The side panel

**Duration** and **Edit length** now sit side by side. They are the same kind of
number and get read against each other — a minute of video, thirty seconds kept
— and stacking them put that comparison a line apart.

**Current hotkey** has been added beneath them, with a keyboard icon that opens
the capture window, matching the folder beside the video and the pencil beside
the bookmark file. It has left the status bar, which is for reporting what just
happened rather than for standing state.

`<none>` and `(not loaded)` were green, the colour the panel uses for a file it
has. They are red now. Nothing loaded is not a healthy state to report in the
healthy colour.

**Play speed** no longer heads an empty column before there are any bookmarks to
set a speed on.

## Smaller things

**The program announced changes it had made itself.** Setting a timestamp from
the player, then returning to the window, reported `Reloaded 3 bookmark(s) from
disk` — over the top of the message about the timestamp you had just set. The
check that spots an edit made in a text editor was comparing against a timestamp
the program never updated when it saved, so its own writes looked like somebody
else's. It was not only noise: the file is written in time order, so the reload
renumbered the rows underneath you.

**Increment offered a name that was already taken.** Splitting twice into one
folder leaves `[done]`, `[done2]` and `[done3]` sitting there, and the prompt
proposed `[done2]` — a file plainly on disk — then asked again for each name
already used. It now goes straight to the first free one, so the preview is
honest and a single click gets past the whole run.

**"Do this for all remaining files" was printed underneath the buttons.** In both
the overwrite prompt and the rename prompt, the checkbox and the buttons shared
one row that was not wide enough for them, so the label ran under the buttons and
most of it could not be clicked. It has a row of its own now.

**The filename example in Settings ignored your naming tag.** The line under the
filename pattern always demonstrated `[done]`, while the note beside it promised
the tag currently selected under Options. It uses the real one.

**Splitting said two different things at once.** The panel counted finished
clips and the status line counted the one in flight, so `1/3` and `Splitting
2/3` appeared together. The status line names the cut being written, the way
Convert and Strip audio name the file.

**`(1 bookmarks)`** reads `(1 bookmark)`.

## Requirements

- Windows, .NET 8 desktop runtime
- MPC-HC with the web interface enabled (Options ▸ Player ▸ Web Interface) —
  the program detects the port itself and says plainly if the interface is off
- ffmpeg and ffprobe are bundled. Your own build dropped beside the executable
  still takes precedence.

## Verify your download

```powershell
Get-FileHash -Algorithm SHA256 .\MPC-HC.Video.Editor.zip
```

| File | SHA-256 |
| ---- | ------- |
| `MPC-HC.Video.Editor.zip` | `81654B5CC4301281F229C43DE3A3ED231C8269366FE246501D5800E46B341ABA` |
| `MPC-HC Video Editor.exe` | `9D1E075DE1C764A559CA7ACDF98C2C46C7332F8B986995C53CF53168C171A0CF` |
| `ffmpeg.exe` | `4A01142006A4E2359293E072957DCDA7760C2003BBEEDE037B4551F2CFC8406F` |
| `ffprobe.exe` | `8B5298DA673B85E628FBC98535A88848E939E16DF72E856FC727E01AA667E243` |
| `LICENSE` | `3972DC9744F6499F0F9B2DBF76696F2AE7AD8AF9B23DDE66D6AF86C9DFB36986` |

FFmpeg is unchanged — still 9.0.1, GPL v3, from
[BtbN/FFmpeg-Builds](https://github.com/BtbN/FFmpeg-Builds).
