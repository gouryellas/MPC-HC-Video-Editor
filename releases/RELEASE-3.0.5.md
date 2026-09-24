# MPC-HC Video Editor 3.0.5

Two deletion fixes, found by auditing every path that removes a file after
"delete files to the Recycle Bin" became a setting in 3.0.3.

## Fixes

**The confirmations said the wrong thing when the Recycle Bin was turned off.**
Every prompt and status message claimed the files were going to the Recycle
Bin, whatever the setting said — so with it off, the dialog being clicked Yes on
described a permanent, unrecoverable deletion as recoverable. That covered the
source-video prompt, the bookmark-file prompt, the converted-images prompt and
the status line afterwards.

The wording now follows the setting: with the bin off it says the files will be
deleted permanently and cannot be recovered, and reports "Permanently deleted…"
when it is done.

**Deleting a playlist destroyed it outright.** It called the unrecoverable
delete directly rather than going through the Recycle Bin, so a `.pls` was gone
for good even in the default state where every other deletion in the app can be
undone. Playlists now follow the same setting as everything else.

## Unchanged, and confirmed working

The cleanup settings themselves were audited at the same time and behave as
described: **Keep** never deletes and never asks, **Ask** prompts and honours No,
and **Delete without asking** deletes silently. The prompt defaults to No, so
Enter cannot delete anything.

Converting images still always asks, whatever the cleanup setting says. That is
deliberate — those settings apply to merge, trim, split and video convert.

## Requirements

- Windows, .NET 8 desktop runtime
- MPC-HC with the web interface enabled (Options ▸ Player ▸ Web Interface)
- ffmpeg and ffprobe, beside the executable or on PATH. WEBM output needs a
  build with libvpx and libopus.
