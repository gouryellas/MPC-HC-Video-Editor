# MPC-HC Video Editor 4.7

Times read the same way everywhere, and the bookmark row is laid out to be
read rather than parsed.

## One rule per time style

Times had drifted into inconsistent readings — a four-second mark shown as
`0:04` in one place and `4s` in another, an hour written `1h` here and `1:00:00`
there. There are now three styles and one rule for choosing between them:

| style | reads | used for |
| ----- | ----- | -------- |
| spoken | `4s` · `1m 35s` · `4hr 25m 30s` | how long something lasts |
| clock | `45s` · `2:05` · `1:22:05` | where you are in the video |
| precise | `00:00:05` · `01:15:30` | fixed width, for editing |

The clock style has one rule about zeros: a zero may follow a figure (`5:00`)
or sit between two (`1:00:04`), but a reading never opens with one. So there is
no `0:04` and no `0:45` — below a minute there is no minutes figure to lead
with, and the reading is simply `4s`. There is no `01:45` either; the leading
figure is never padded.

That rule lives inside the clock formatter rather than in the code that calls
it. A caller that forgot it would print `0:04`, and every caller would have to
remember it for the program to read consistently.

Two things were corrected on the way:

- A length now reads `1hr`, not `1h`.
- Hours are counted from the total, not from `TimeSpan.Hours`, which rolls
  over into days. A 25-hour recording used to report as one hour in.

## The bookmark row

```
[1] < 4s > - < 12s > (8s)     pointer over the row
[1]   4s   -   12s   (8s)     pointer elsewhere
```

Every gap is one space — a literal space, not a margin in pixels, which would
only ever be a guess at how wide a space is in whatever font and size the row
ends up using.

The frame-nudge arrows now flank the timestamp they move, and they are hidden
rather than removed when the pointer is elsewhere. They keep their width, so
hovering a row reveals them instead of shoving the timestamps sideways to make
room. Nothing in the row moves except when a timestamp genuinely changes width
— at `59s → 1:00`, and again at `59:59 → 1:00:00`.

The index no longer sits in a fixed-width column that padded `[1]` out to the
width of `[10]` before the timestamps could start.

## The file actions moved next to the files

**Edit bookmarks** was a button across the top of the window, nowhere near
anything it referred to. It is now a pencil beside CURRENT BOOKMARKS, next to
the name of the file it opens, and it appears only when there are bookmarks to
edit.

CURRENT VIDEO gains a folder icon on the same terms, opening Explorer with the
video selected.

Both icons take their visibility from whether their action is available, so
"shown when there is something to act on" and "clickable when there is
something to act on" cannot drift apart.

**Enter time** keeps its Actions menu entry and loses its button. Neither it
nor Edit bookmarks was earning a place in the toolbar, which now fits on one
row.

## The bookmark count

The separate "2 items" line is gone; the caption reads **Bookmarks: 2**.

The figure changed with the move. The old count included a lone opening
timestamp still waiting for its close — a row in the list, but not yet a cut —
and so promised one more clip than a split would actually produce. The caption
counts cuts with both ends.

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
| `MPC-HC.Video.Editor.zip` | `ABDD4C7D3CA077D3353E11BF72C1960DDDC597E6F2ECB6AF3A05919B7E550FC6` |
| `MPC-HC Video Editor.exe` | `9AF3C4341BD5E9B80A7AD184D8F09C5CF4CDA2D06BD9B97AE4AA49002E9124C2` |
| `ffmpeg.exe` | `4A01142006A4E2359293E072957DCDA7760C2003BBEEDE037B4551F2CFC8406F` |
| `ffprobe.exe` | `8B5298DA673B85E628FBC98535A88848E939E16DF72E856FC727E01AA667E243` |
| `LICENSE` | `3972DC9744F6499F0F9B2DBF76696F2AE7AD8AF9B23DDE66D6AF86C9DFB36986` |

FFmpeg is unchanged — still 9.0.1, GPL v3, from
[BtbN/FFmpeg-Builds](https://github.com/BtbN/FFmpeg-Builds).
