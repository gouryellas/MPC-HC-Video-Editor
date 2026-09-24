# MPC-HC Video Editor 4.8

Every long-running operation now reports itself the same way, and the progress
panel says one thing once.

## The panel said the same thing three ways

A merge of ten files used to show this:

```
Merge files      File 1 of 10                 27%
holiday-footage-part2[done2].mp4
Preparing 4/10
```

Three rows, two counters, and the prominent one was wrong. **File 1 of 10** was
set once before the loop started and never again, so it read `1` for the whole
job however many files went by. The counter that did move was the one buried in
the step text below it — and it was there because the step had to carry its own
count to be useful, the panel's counter being broken.

There is one counter now, it moves, and the step no longer duplicates it:

```
Merging files · Preparing          4/10      27%
clip-04.mp4
```

The action and the step share a line because the step alone is rarely a
sentence — "Preparing", "Encoding to MP4", "Extracting to MP3" — and reads as
one phrase with the action in front of it. That removed a row without removing
anything you could read.

Two step messages were repeating their own action and have been trimmed:
`Writing PNG` under **Converting images to PNG** is now `Writing`, and
`Extracting audio to MP3` under **Stripping audio** is now `Extracting to MP3`.

## The file name is the file being worked on

It used to be the output name for the whole job, which on a merge is derived
from the first input and so reads exactly like one — a name that never changes
while a counter next to it climbs to ten. It now names the input being
processed, and switches to the file that was produced when the job finishes.

## The bar has bands

Black track. Red below 25%, orange to 50, yellow to 75, blue to 99, green at
100. The previous bar interpolated continuously between black, red, orange,
yellow and green, which meant the colour at any moment was a shade between two
others and told you nothing the percentage did not already say. Five bands are
legible at a glance from across the room; a gradient is not.

## Finishing is an event

A finished job held its full bar for 600ms and vanished — long enough to notice
something had happened, not long enough to read what. The bar now lands on
solid green and holds for three seconds, with the headline replaced by what
actually occurred and the file that was created beneath it:

```
Merged 10 files into              10/10     100%
holiday-footage-part2[done2].mp4
```

Operations that write many files name the destination folder instead, there
being no single file to name.

The hold runs after the operation releases the UI rather than inside it. The
old 600ms pause sat in the middle of the work with the busy flag still set, so
it froze the toolbar for its duration; this one does not. Jobs that have
follow-up work — the cleanup prompt after a trim or a split — start it
immediately and let the bar finish holding behind the prompt. A new job started
while a finished bar is still up simply takes the panel over.

Failures and cancellations still clear the panel at once. Only a job that
produced something gets the green.

## Every action, one format

The panel behaved differently depending on which button you pressed. All seven
operations now report identically — the counter, the percentage, the banded
bar, the green hold:

| While running | On completion |
| ------------- | ------------- |
| `Merging files · Preparing` | `Merged 10 files into` |
| `Trimming cut` / `Merging cuts` | `Trimmed cut into` / `Merged 3 cuts into` |
| `Splitting clips · Cutting 0:10 → 0:25` | `Split into 8 clip(s) in` |
| `Converting images to PNG · Writing` | `Converted 12 image(s) to PNG in` |
| `Converting video · Encoding to MP4` | `Converted 4 file(s) to MP4 in` |
| `Stripping audio · Extracting to MP3` | `Extracted audio from 4 file(s) to` |
| `Writing chapters` | `Wrote 14 chapters into` |

Action names are present participles throughout — **Strip audio** became
**Stripping audio**, **Convert video** became **Converting video**. A panel that
only appears while something is running should read as something in progress.

## One progress bar, not two

The status bar carried a second progress bar at its right end, bound to the
same percentage as the panel directly above it. It is gone.

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
| `MPC-HC.Video.Editor.zip` | `71680150E4B592E0614B7672B2CA5A136C51BABA53D6AA27D5D52E5127F482A5` |
| `MPC-HC Video Editor.exe` | `694D265C6A30A81194E552CAB3B4AD18772A0C8AFE2C4FFA001D8308F18FCDA9` |
| `ffmpeg.exe` | `4A01142006A4E2359293E072957DCDA7760C2003BBEEDE037B4551F2CFC8406F` |
| `ffprobe.exe` | `8B5298DA673B85E628FBC98535A88848E939E16DF72E856FC727E01AA667E243` |
| `LICENSE` | `3972DC9744F6499F0F9B2DBF76696F2AE7AD8AF9B23DDE66D6AF86C9DFB36986` |

FFmpeg is unchanged — still 9.0.1, GPL v3, from
[BtbN/FFmpeg-Builds](https://github.com/BtbN/FFmpeg-Builds).
