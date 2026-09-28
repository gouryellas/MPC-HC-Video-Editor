# MPC-HC Video Editor 5.2.2

Two playback fixes, and the clip list is quicker to work with.

## Playing a cut actually plays it

Play all and Play selected walked through the cuts but left each one paused on
its first frame. The program was sending MPC-HC the play/pause toggle instead
of play, twice — so it started the cut and immediately stopped it.

Finishing a run also stopped the player and jumped back to 0:00. It pauses
where it is now.

## The video length matches the player

The timeline took its length from the file's own metadata, which can be wrong:
one video reported 3:15 where MPC-HC plays 3:26. Everything you mark comes off
the player's clock, so the timeline follows it now. A short timeline made the
end of the video impossible to mark and put every position on the bar slightly
out.

## The preview frames show what the clip will look like

Rotate turns them, Flip mirrors them, and a fade is drawn as the frame coming
out of black at the start and going into it at the end. Muted clips get a
crossed speaker in the corner, and a clip at anything other than normal speed
gets a small `2x` or `1.25x` badge.

Nothing is re-rendered for this, so it keeps up with the buttons.

## The duration on a row follows the speed slider

The figure in parentheses was the marked range, which does not change when you
change the speed. It is now how long the saved clip will run: a 20-second cut
at 2x reads 10s.

## Click a row to flip, rotate, mute, fade or delete it

These needed the row's checkbox ticked first. Clicking the row is enough now.

Checks still win when you have some: with three cuts checked, these act on
those three, whichever row you last clicked.

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
| `MPC-HC.Video.Editor.zip` | `7D0DB08C04C46D5127682C6669EAF05D6BA86F0AA2B5F047FA505AE1265346D9` |
| `MPC-HC Video Editor.exe` | `B99797348471B5A53A44BF5E4DB6C773801B752C5FABE0520F5446107ACF1D24` |
| `ffmpeg.exe` | `4A01142006A4E2359293E072957DCDA7760C2003BBEEDE037B4551F2CFC8406F` |
| `ffprobe.exe` | `8B5298DA673B85E628FBC98535A88848E939E16DF72E856FC727E01AA667E243` |
| `LICENSE` | `3972DC9744F6499F0F9B2DBF76696F2AE7AD8AF9B23DDE66D6AF86C9DFB36986` |

FFmpeg is unchanged — still 9.0.1, GPL v3, from
[BtbN/FFmpeg-Builds](https://github.com/BtbN/FFmpeg-Builds).
