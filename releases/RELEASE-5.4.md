# MPC-HC Video Editor 5.4

Three things for pictures: thumbnails from a video, resizing, and a say in
where any of it is written.

## Save thumbnails

Actions ▸ Save thumbnails. Takes a frame every so many seconds through the
whole video — an image per frame, or every frame tiled into a single image.

You choose the interval, the width of each thumbnail, and how many across a
tiled image is. A video with more frames than fit starts another one.

It reads the file on disk, so MPC-HC does not have to be running. With no video
loaded it asks for one.

## Resize images

Actions ▸ Resize images. One image or a folderful, at a standard size: VGA
through 4K, the two common square sizes, or type your own.

A size alone does not say what to do with a picture that is not that shape, so
it asks. Fit inside keeps the shape; Fill and crop gives the exact size and
trims the overflow; Stretch gives the exact size by squashing. Each file keeps
its own format.

## Choose where images are written

Convert images and Resize images both ask: beside each picture, in the Save to
folder, or somewhere else. Beside each picture means exactly that — a selection
spanning three folders comes back to those three folders.

## Convert images asks which format

The toolbar button for it used to do nothing, having no format to work with.
It asks now. The menu entries still name a format each.

## Small button icons

Settings ▸ Appearance. Draws the toolbar as icons alone, the way the camera and
bin buttons already were. Hover any of them for its name.

## The active naming tag is lit

Whichever tag is in use shows it on the toolbar, including "no tag".

## Flip, Rotate, Mute and Fade in the menu

They are in the Actions menu as well as on the toolbar, and are available
whenever there is a complete cut — not only once one is checked.

## Fixes

- Detect cuts and Chapters stayed greyed out for the whole session, whatever
  you opened.
- Button tooltips wait three seconds, as the menus do.
- Buttons no longer change colour under the pointer, which was wiping out the
  colours on Merge, Split, Convert and Strip audio.

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
| `MPC-HC.Video.Editor.zip` | `1C54CA37FA362E3533A2638C45F5854FB3C04448FD619483EF926891DDD09E26` |
| `MPC-HC Video Editor.exe` | `9AA991579E21A01B48BB4FD49779A4A6B4499B67A2617CE331E2916C8C3AFF18` |
| `ffmpeg.exe` | `4A01142006A4E2359293E072957DCDA7760C2003BBEEDE037B4551F2CFC8406F` |
| `ffprobe.exe` | `8B5298DA673B85E628FBC98535A88848E939E16DF72E856FC727E01AA667E243` |
| `LICENSE` | `3972DC9744F6499F0F9B2DBF76696F2AE7AD8AF9B23DDE66D6AF86C9DFB36986` |

FFmpeg is unchanged — still 9.0.1, GPL v3, from
[BtbN/FFmpeg-Builds](https://github.com/BtbN/FFmpeg-Builds).
