# MPC-HC Video Editor 4.2

A bug fix. The compact overlay could take the whole program off the screen with
it.

## The overlay no longer leaves the program with nothing on screen

**View ▸ Minimal** pins the overlay, and putting the overlay up hides the full
window — that is the swap it exists to make. While it was pinned, moving focus
to some third application — a browser, a file manager, anything that is not
MPC-HC — hid the overlay so it would not float over that window, but left the
full window hidden behind it.

The program then had nothing on screen at all: no window, no taskbar button,
and the **X** key disarmed along with the overlay it belongs to, because X is
deliberately live only while there is an overlay to explain it.

In notification-area mode the tray icon still brought it back. In application
mode there is no tray icon, so the only way back was clicking MPC-HC again —
the program was running and, to anyone who had switched to something else,
gone.

The pin now holds the *mode* rather than one particular window. With the
overlay pinned:

- MPC-HC has focus → the overlay is up, exactly as before.
- Anything else has focus → the full window comes back, and the pin is kept.
- Returning to the player drops straight back to the overlay.

So the overlay still stays out of the way of an unrelated window, which is what
hiding it was for, and one of the two windows is always on screen.

**View ▸ Switch views automatically** was not at fault and has not changed. It
is worth stating plainly why it looked broken: a pin is checked before the
setting, by design, so once the overlay had been pinned by hand the setting had
no opportunity to act. That layering is unchanged — but both paths now agree
about the full window, so it is no longer something that can strand you.

## Screenshots

The images on the project page were stale — they still showed the pre-4.1
appearance. They are now the current interface on the Graphite theme, including
all five Settings tabs.

## A plainer icon on the program file

The icon baked into the executable was the graphite camera — one theme's
colours, on a file that four themes share and that no theme can repaint. It is
now black and white: the same drawing, in no theme's colours, so it cannot
disagree with the theme actually in use.

The icons you see while the program runs are unchanged and still follow the
theme.

> **The archive was reissued after publication** to include this icon, so the
> hashes below are not the ones this release first carried. If you downloaded
> within the first couple of hours and verified against the original table,
> your copy is the same program without the new file icon. Re-download if you
> would rather the hashes match.

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
| `MPC-HC.Video.Editor.zip` | `A7E15A2FE21770897D6FB3C020A929AEF176C7F1877DE5F1598A98A5D250A0B4` |
| `MPC-HC Video Editor.exe` | `E9331E6781A79129F22698BACDD43F20D85909B2711B8D209B90FA0EFCAD08BC` |
| `ffmpeg.exe` | `4A01142006A4E2359293E072957DCDA7760C2003BBEEDE037B4551F2CFC8406F` |
| `ffprobe.exe` | `8B5298DA673B85E628FBC98535A88848E939E16DF72E856FC727E01AA667E243` |
| `LICENSE` | `3972DC9744F6499F0F9B2DBF76696F2AE7AD8AF9B23DDE66D6AF86C9DFB36986` |

FFmpeg is unchanged — still 9.0.1, GPL v3, from
[BtbN/FFmpeg-Builds](https://github.com/BtbN/FFmpeg-Builds).
