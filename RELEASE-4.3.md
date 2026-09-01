# MPC-HC Video Editor 4.3

A connection bug that made the program look broken on current MPC-HC, and the
end of picking the view by hand.

## The program no longer loses the video a few seconds after you open it

Reported in [#10](https://github.com/gouryellas/MPC-HC-Video-Editor/issues/10)
by **Land-Strider**, on MPC-HC 2.8.1.

Open a video and everything populated correctly — the overlay showed the file
and its bookmarks, the full window agreed. Seven or eight seconds later the lot
went blank, as though no video were loaded, with the player still running and
still playing.

Nothing was disconnecting. The program identifies what is open by reading
MPC-HC's window title, and it insisted the title contain the words *Media Player
Classic*. That is what the older builds say. MPC-HC 2.x titles its window
`<file> - MPC-HC`, which never matched, so the file was never identified and the
program concluded — on the very first check — that nothing was playing.

The eight seconds were the grace period that holds off the "nothing is playing"
conclusion while a newly opened file settles. It was the only thing keeping the
session alive, and when it expired the session was cleared. So a total failure
to read the title looked like a connection dropping out after a delay.

The title is now read for the *file* rather than for the player name, which is
not something to depend on: the longest leading run of the title that looks like
a file name is taken, working right to left over the separators so that
`Artist - Track.mp4 - MPC-HC` stays intact. Every arrangement now works —
`- MPC-HC`, `- MPC-HC x64`, `- Media Player Classic`, `- Media Player Classic
Home Cinema`, `- MPC-BE x64`, a full path with no player name at all, and a bare
file name.

This affected every user of MPC-HC 2.x whose title bar was set to show the file
name. It did not affect anyone whose title bar shows the full path, because a
path that exists on disk was recognised by a separate route — which is why it
went unnoticed here.

## What is playing is now read from the Web Interface

The window title is no longer the only source. MPC-HC's Web Interface reports
the full path of the open file outright, with no player name to strip and no
dependence on what the title bar is configured to show, and it is now asked
first. The title remains the fallback for players with the interface switched
off.

This also covers the one case the title cannot: with MPC-HC's **Title bar text**
set to **Don't display**, there is no file name on screen anywhere, and before
this the program had nothing to go on.

The interface is read on a background task and never on the poll, so a player
that stops answering cannot stall the window. An interface that is off, or on a
different port, costs nothing beyond falling back to the title.

## One view control, on by default

**View ▸ Minimal** and **View ▸ Full** are gone. **View ▸ Switch views
automatically** is now the whole of it, and is **on by default**.

Picking the view by hand was a mode with nothing on screen naming it. Choosing
Minimal pinned the overlay until X or Full, and the pin outlived the overlay:
it survived the overlay hiding itself for an unrelated window, so returning to
the player put the overlay back up with nothing anywhere saying why. Worse, the
pin was checked ahead of the setting, so **unchecking "switch views
automatically" did not stop the view switching** for anyone who had picked
Minimal earlier in the session. That whole layer is removed rather than patched.

What is left is the behaviour both were mostly being used to get: the overlay
while MPC-HC is the active window, the full window as soon as it is not.

Upgrading turns the setting on once. It has been written to the settings file on
every run since it existed, so an install upgrading from 4.2 carries an explicit
"off" that a changed default would never reach — and with Minimal gone, that
would leave no way to the overlay at all. Turn it off after upgrading and it
stays off.

## Leaving the player now brings the window to the front

Clicking away from MPC-HC restores the full window. It was being restored
*behind* whatever had just been clicked, which is not a window coming back in
any sense you can see.

It now comes to the front. Briefly, and once — it is not left always-on-top, and
clicking back to the player hands the foreground straight over.

## X works from a fullscreen player

**X** brings the full window back. Against a fullscreen player it appeared to do
nothing: the key was arriving and the window was being restored, but a
fullscreen player covers the monitor, holds the foreground and sits above the
ordinary window order, so the window came back behind it.

X now takes the player out of fullscreen on the way. Nothing can be put in front
of a fullscreen player, so leaving it is part of what showing the window costs. A
windowed player is left exactly as it is.

## The overlay says which way out applies

The overlay's footer said *press X* whichever way the player was presented. With
the player in a window, clicking anywhere else already brings the full window
back — so a user who did the obvious thing saw the window appear for a reason
the overlay had never mentioned.

It now names whichever applies:

- Fullscreen — `Hitting  X  restores the program`
- Windowed — `Unfocusing MPC-HC restores the program`

X works either way. The line picks which one to name, not which one exists.

## The timestamp hotkey confirms itself when the overlay is off

With automatic switching turned off, the full window stays up permanently — and
it is behind the player when the hotkey is pressed. The overlay is not there to
list the new bookmark either, so the hotkey recorded a timestamp and produced no
visible response at all.

Pressing it now always confirms on screen, with the bookmark number and the time
recorded. This appears even with toasts switched off, but only when there is
genuinely nothing else to show it — never while the overlay is up or while the
full window has focus, because in both of those cases something on screen
already says what happened.

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
| `MPC-HC.Video.Editor.zip` | _pending build_ |
| `MPC-HC Video Editor.exe` | _pending build_ |
| `ffmpeg.exe` | _pending build_ |
| `ffprobe.exe` | _pending build_ |
| `LICENSE` | _pending build_ |

FFmpeg is unchanged — still 9.0.1, GPL v3, from
[BtbN/FFmpeg-Builds](https://github.com/BtbN/FFmpeg-Builds).
