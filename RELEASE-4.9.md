# MPC-HC Video Editor 4.9

Upgrading no longer costs you your settings, the overlay can drive the player,
and a bookmark you have not finished is still a bookmark you can tick.

## Replacing the folder used to wipe your settings

The program is one folder with no installer, so the natural way to upgrade is to
delete the old folder and unpack the new one. That took `settings.json` with it.
The new copy started up, found nothing, and did what any first run does — it
started from defaults. Shortcuts, naming tags, suffixes, the timestamp hotkey,
recent files: gone, with nothing on screen to say why.

Every settings save now also writes a copy to
`%APPDATA%\MPC-HC Video Editor\settings.json`. That copy is read in exactly one
situation: the program starts, finds no settings beside the executable, and
finds the copy. Then it restores from it.

The program is still portable. It reads and writes its settings beside the
executable, as before, and a local file always wins — so a copy of the folder
you deliberately made fresh stays fresh, and two installs on one machine do not
argue about which is in charge. The backup is a spare key, not a second home.

If you have ever run a pre-portable version, this is the same place it kept its
settings, and the same step carries them over. That has always worked; it is now
the general rule rather than a one-time migration.

## The overlay can seek

**Settings ▸ Overlay ▸ Clicking.** Off by default. Turn it on and the
timestamps on the compact overlay seek MPC-HC when clicked, the same as the ones
in the full window.

Read the trade before you turn it on. The overlay is normally click-through:
clicks land on the video behind it, so it can sit over a fullscreen player
without being in the way. Making the timestamps clickable means the panel
catches clicks — that corner of the screen stops pausing the video. The
transparent area around the panel is unaffected, and only the panel itself
changes behaviour.

The overlay still never takes focus. Seeking from it leaves MPC-HC in front, so
the full window does not come back mid-click.

## A bookmark with one timestamp can be ticked

Set an opening timestamp and its checkbox was greyed out until you set a closing
one. That made a lone opening timestamp the only row in the list you could not
mark — and "Delete selected" works on marks, so it was also the only row you
could not delete that way. Deleting the thing you just started was the one job
the check most needed to do.

Every row is checkable now. **Select All** includes open bookmarks, and a check
survives a bookmark being reopened instead of being silently cleared.

Nothing acts on a half-finished cut as a result. Merge, split, play selected,
flip, rotate and mute read past the check for a real start and end, so an open
bookmark is invisible to them however it is ticked.

## The timeline position is blue

It was amber, drawn over a timeline whose range marks are also amber. The
playhead and the cuts were the same colour on the default theme. Blue is the one
hue none of the marks use, so the position now reads as separate from them at a
glance, in every theme.

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
| `MPC-HC.Video.Editor.zip` | `8439A497649D43CD70133DE00104B18DCAA887C003BCF713746616B92F249D0E` |
| `MPC-HC Video Editor.exe` | `4245A4A83873D8EC81CE99684E3F6756C525DBBD79E13C93AFB5162AA647CAA3` |
| `ffmpeg.exe` | `4A01142006A4E2359293E072957DCDA7760C2003BBEEDE037B4551F2CFC8406F` |
| `ffprobe.exe` | `8B5298DA673B85E628FBC98535A88848E939E16DF72E856FC727E01AA667E243` |
| `LICENSE` | `3972DC9744F6499F0F9B2DBF76696F2AE7AD8AF9B23DDE66D6AF86C9DFB36986` |

FFmpeg is unchanged — still 9.0.1, GPL v3, from
[BtbN/FFmpeg-Builds](https://github.com/BtbN/FFmpeg-Builds).
