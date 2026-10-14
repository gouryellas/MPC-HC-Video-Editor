# MPC-HC Video Editor 5.5.1

Addressed and corrected an audio desync issue, plus small fixes.

## Addressed and corrected audio desync issue

Joining several cuts together could leave the audio drifting further out of
sync with the video the more clips were merged — a small per-segment rounding
gap that used to compound at every join. Audio is now rebuilt once across the
whole merged file instead of copied piece by piece, so the drift has nowhere
to accumulate. Cutting exactly on the marked frame is also on by default now,
instead of a setting you had to turn on yourself.

## Small fixes

- A clip whose finished length doesn't match what was marked now shows up as
  a notice you have to read, not status-bar text already scrolled past.
- The button colors dialog dropped its explanatory heading.
- Toolbar button tooltips are shorter.

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
| `MPC-HC.Video.Editor.zip` | `F7FC7FFBE22F95B107E0171E5F7BCC7D7DD392A2EC0D827CD5360DDF6B64E6A3` |
| `MPC-HC Video Editor.exe` | `C731DCC7E0A0A2822C9F1CD255849150E23EC3B61BAE403921EC4716021734BB` |
| `ffmpeg.exe` | `4A01142006A4E2359293E072957DCDA7760C2003BBEEDE037B4551F2CFC8406F` |
| `ffprobe.exe` | `8B5298DA673B85E628FBC98535A88848E939E16DF72E856FC727E01AA667E243` |
| `LICENSE` | `3972DC9744F6499F0F9B2DBF76696F2AE7AD8AF9B23DDE66D6AF86C9DFB36986` |

FFmpeg is unchanged — still 9.0.1, GPL v3, from
[BtbN/FFmpeg-Builds](https://github.com/BtbN/FFmpeg-Builds).
