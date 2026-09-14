# MPC-HC Video Editor 4.9.1

A fix for the update notice in 4.9, which could not be dismissed.

## "Not now" did nothing

The window that appears when a newer version exists had a dead button. Clicking
**Not now** left it on screen. Esc did nothing either. The only way out was the
close button in the title bar.

The two buttons were closing the window in different ways, and only one of them
worked here. **Open the releases page** closes the window itself, in code.
**Not now** relied on `IsCancel`, which closes a window by setting its dialog
result — and a dialog result only means anything for a window opened as a modal
dialog. This notice is deliberately not modal: it arrives on its own schedule,
possibly while the main window is hidden behind the compact overlay or sitting
in the notification area, and a modal window owned by something you cannot see
is a window you cannot answer. So it is shown as an ordinary window, the dialog
result had nowhere to go, and the button quietly did nothing at all.

Both are now dismissed explicitly, and Esc closes the notice as it always should
have.

Nothing else changed. **Not now** still leaves the update check switched on —
only the **Stop checking for new versions** link inside the notice turns it off.

Everything else in 4.9 is unaffected, and every other dialog in the program was
already correct: they are all shown as modal dialogs, where `IsCancel` does
close the window.

## If you are on 4.9

This is worth taking, because the broken button is on the notice that tells you
about it. Dismiss that notice with the title bar close button, then download
below.

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
| `MPC-HC.Video.Editor.zip` | `E750D63608A7BB241690BADBE99B6F4BE6E3A762369E756932ED93DD2859D19F` |
| `MPC-HC Video Editor.exe` | `158B544D83E6D1D028E7E33A41CD5FF94DE217F679C49AF92F2D06F0FDFB68E4` |
| `ffmpeg.exe` | `4A01142006A4E2359293E072957DCDA7760C2003BBEEDE037B4551F2CFC8406F` |
| `ffprobe.exe` | `8B5298DA673B85E628FBC98535A88848E939E16DF72E856FC727E01AA667E243` |
| `LICENSE` | `3972DC9744F6499F0F9B2DBF76696F2AE7AD8AF9B23DDE66D6AF86C9DFB36986` |

FFmpeg is unchanged — still 9.0.1, GPL v3, from
[BtbN/FFmpeg-Builds](https://github.com/BtbN/FFmpeg-Builds).
