# MPC-HC Video Editor 4.4

The status bar stops hiding the half of the message that explains the result.

## A long message now scrolls instead of being cut off

After an operation the status bar reports what was produced, and that report
carries the output file name:

> Created `<name>` — The finished file is 0.7s longer than the marked range
> (0:26 against 0:26).

With a long file name, everything after the name ran off the end of the bar and
was simply clipped. The measurement added in 4.0 — the whole point of which is
to say when a keyframe-aligned cut came out longer than what was marked — was
the part that disappeared, because it comes last. The longer the file name, the
less of the explanation survived, and file names of the length this program is
usually pointed at left nothing of it at all.

The status text now scrolls when it is wider than the bar. It holds at the
start, runs to the end, holds there, and runs back, so both ends come to rest
long enough to read. A message that fits does not move at all, and a new message
stops the scroll and starts again from the left.

## The play speed column is labeled more briefly

**PLAY SPEED OF THE SAVED CLIP** is now **PLAY SPEED**. The tooltip on the
header already explains what it changes and what it does not.

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
| `MPC-HC.Video.Editor.zip` | `858410CB3286946BCC1215CB2170E10A3216AD10100B70F3C22C07211A2AB7A0` |
| `MPC-HC Video Editor.exe` | `3E42EB66A913A4832FDEE824ED04287FDE61373EE2842AD541530C741739AB40` |
| `ffmpeg.exe` | `4A01142006A4E2359293E072957DCDA7760C2003BBEEDE037B4551F2CFC8406F` |
| `ffprobe.exe` | `8B5298DA673B85E628FBC98535A88848E939E16DF72E856FC727E01AA667E243` |
| `LICENSE` | `3972DC9744F6499F0F9B2DBF76696F2AE7AD8AF9B23DDE66D6AF86C9DFB36986` |

FFmpeg is unchanged — still 9.0.1, GPL v3, from
[BtbN/FFmpeg-Builds](https://github.com/BtbN/FFmpeg-Builds).
