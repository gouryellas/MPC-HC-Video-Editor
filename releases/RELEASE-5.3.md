# MPC-HC Video Editor 5.3

The toolbar is yours now: pick the buttons, put them in any order, and drop the
labels if you want it smaller.

## Customize the toolbar

Right-click the toolbar, or Options ▸ Customize toolbar. Drag any button from
the list onto your row, drag one off to remove it, and drag the ones you have
into the order you want.

There are three blocks to arrange them with: a fixed gap, a stretching gap that
pushes everything after it to the right, and a new row. Use as many of each as
you like.

Twenty-seven buttons are available, including several that were only ever in the
menus — Open video, Settings, Undo, Enter time, Detect cuts, GIF / WebP,
Chapters and more.

The preview box is the width the window can shrink to, so a row that fits there
fits at any size.

## Small button icons

Settings ▸ Appearance. Draws every toolbar button as an icon alone, the way the
camera and bin buttons already were. Hover any of them for its name. A row that
needed two lines usually fits one without the labels.

## Drag the buttons on the toolbar itself

For a quick change you do not need the dialog — drag a button along the row and
drop it where you want it.

## A typed time closes the open bookmark

With a single timestamp waiting for its end, Enter time added a second
standalone row instead of finishing the first. It completes the range now, the
same as marking the end with the hotkey.

## Leaving MPC-HC no longer steals focus

Switching from MPC-HC to anything else brought this window to the front. It now
comes forward only if it was the window behind the player.

## Menu tooltips wait three seconds

They appeared almost instantly and got in the way of reading the menu.

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
| `MPC-HC.Video.Editor.zip` | `622D517AE9036DC64279F66EFCC841C9AA3D5106F092712B9294226D758C2B59` |
| `MPC-HC Video Editor.exe` | `958C62DBA8EEE083CA60929DCB9BD21F8B1464497C79E12953AC0037FDE58921` |
| `ffmpeg.exe` | `4A01142006A4E2359293E072957DCDA7760C2003BBEEDE037B4551F2CFC8406F` |
| `ffprobe.exe` | `8B5298DA673B85E628FBC98535A88848E939E16DF72E856FC727E01AA667E243` |
| `LICENSE` | `3972DC9744F6499F0F9B2DBF76696F2AE7AD8AF9B23DDE66D6AF86C9DFB36986` |

FFmpeg is unchanged — still 9.0.1, GPL v3, from
[BtbN/FFmpeg-Builds](https://github.com/BtbN/FFmpeg-Builds).
