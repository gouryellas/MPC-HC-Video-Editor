# MPC-HC Video Editor 3.0.7

Four features, and a fix for a cut that has quietly been wrong all along.

## Cuts can now land where you actually marked them

A copied cut cannot begin anywhere but a keyframe. Every version until now
asked ffmpeg for your timestamp, got back the keyframe at or before it, and
wrote that — with nothing in the output to say so. On a file with keyframes
five seconds apart, marking 3.00s produced a clip starting at 0.

Measured on exactly that file, cutting 3s to 6s:

| | asked for | got | drift |
| --- | --- | --- | --- |
| Fast (copy) | 3.00s | 4.16s | **+1.16s** |
| Precise (re-encode) | 3.00s | 3.02s | +0.02s |

**Settings ▸ Encoding ▸ Cut accuracy** now chooses. Fast stays the default,
because a copied cut is lossless and instant; Precise re-encodes each segment
to land on the marked frame, costing time and one generation of quality on cuts
that would otherwise have been a straight copy. Neither is simply better, which
is why it is a choice rather than a change.

## Hardware encoding

Where video is re-encoded, it can now go through the GPU: **NVIDIA NVENC**,
**Intel Quick Sync** or **AMD AMF**. Typically several times faster than x264,
and larger at the same visual quality — the usual trade.

Being listed is not the same as working. The GPU encoders are compiled into
ffmpeg whether or not the hardware exists, so `h264_nvenc` is advertised on a
machine with no NVIDIA card in it. Opening the Settings dialog therefore runs a
real test encode for each one and greys out whatever this machine cannot do. A
saved choice that stops working — a driver change, a moved disk — falls back to
software rather than failing the next job.

Only affects MP4, MKV and MOV. The legacy containers carry their own codecs and
have nothing to choose.

## MPC-HC's web interface port is detected

The port used to be a number you had to keep in step by hand with one buried in
MPC-HC's options, and getting it wrong meant seeking silently fell back to
clicking the seek bar. The player already knows the answer, so the app now
reads it — from a portable install's `.ini` first, then the registry.

More useful than the port itself: when seeking fails, the message now says what
is actually wrong. "MPC-HC's own settings say the Web Interface is turned off —
that is the problem, not the port" is a far better answer than generic advice
about a number that was never wrong.

On by default. The manual port stays as the fallback for an install this cannot
read, so nothing gets worse.

## In and out thumbnails

Selecting a clip now shows its first and last frame in the side panel, with the
saved length underneath. A pair of timestamps tells you a range is three minutes
long; it does not tell you whether it is the right three minutes.

Only the selected clip is rendered, not every row — two frames per selection is
free, two per bookmark across a long list is hundreds of ffmpeg launches for
pictures that are mostly scrolled past. Frames are piped straight out of ffmpeg
rather than written to disk, so nothing accumulates beside the executable.

## Requirements

- Windows, .NET 8 desktop runtime
- MPC-HC with the web interface enabled (Options ▸ Player ▸ Web Interface) —
  the app will now tell you plainly if it is not
- ffmpeg and ffprobe are bundled. Your own build dropped beside the executable
  still takes precedence.

## Verify your download

```powershell
Get-FileHash -Algorithm SHA256 .\MPC-HC.Video.Editor.zip
```

| File | SHA-256 |
| ---- | ------- |
| `MPC-HC.Video.Editor.zip` | `C3D5452219C0ACEF02E46F55D33899E61291186CF61D10730CA9CA588667C409` |
| `MPC-HC Video Editor.exe` | `9328798996ED987C865FCF6033C0435C0FF7754058BB757879B64E36B719C486` |
| `ffmpeg.exe` | `4A01142006A4E2359293E072957DCDA7760C2003BBEEDE037B4551F2CFC8406F` |
| `ffprobe.exe` | `8B5298DA673B85E628FBC98535A88848E939E16DF72E856FC727E01AA667E243` |
| `LICENSE` | `3972DC9744F6499F0F9B2DBF76696F2AE7AD8AF9B23DDE66D6AF86C9DFB36986` |

FFmpeg is unchanged from 3.0.6 — still 9.0.1, GPL v3, from
[BtbN/FFmpeg-Builds](https://github.com/BtbN/FFmpeg-Builds).
