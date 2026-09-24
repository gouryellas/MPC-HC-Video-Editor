# MPC-HC Video Editor 4.9.3

Clips found by a scan can now be acted on. Merging clips that already match no
longer re-encodes them to arrive at what it started with.

## A scan filled the list and left every button greyed out

**Bookmarks ▸ Find clips automatically…** proposed clips, you accepted them, and
then nothing could touch them. The trashcan did nothing. **Remove selected
timestamps** was greyed out, and so were **Select All**, **None**, **Play
selected**, **Merge**, **Split**, and the flip, rotate and mute buttons — no
matter how many rows were ticked.

The list was fine and so were the checkboxes. The commands were asking a
different question. Every one of them is gated on the bookmark file existing on
disk rather than on the number of rows on screen, because a timestamp that has
not been written is one an interrupted session loses. Setting a timestamp by
hand is what creates that file. Accepting a scan did not — it filled the list in
memory and left the disk untouched — so the program was right to report no
bookmark file, and everything that needs one stayed switched off. The side panel
said as much: **CURRENT BOOKMARKS** read `<none>` beside a full list of clips.

Accepting a scan now writes the file, the same way the first hand-placed
timestamp does. A scan you accept nothing from still writes nothing, so an empty
bookmark file is never brought into existence.

**If you have a scanned list open from an earlier version**, it exists only in
memory and this build cannot write it for you after the fact. Run the scan
again, or set one timestamp by hand, and the file appears.

## Merging clips that already match was slower than it needed to be

4.9.2 brought every clip to a common frame rate and frame size before joining
them, which is what stopped mixed clips from stalling. It did that whether or
not the clips differed. Merging ten clips cut from one video — same size, same
rate throughout — paid for a full re-time and re-scale of all ten to arrive at
exactly what it began with.

Both steps are now skipped when the inputs already agree, so the ordinary merge
of clips from a single source does no normalising work at all. Mixed clips are
handled exactly as before.

## One large clip no longer drags the whole batch up to meet it

The target frame size was the largest clip in the set. A single 1080p clip among
a batch of 640x360 ones pulled every clip up to 1080p — around nine times the
encoding work per clip, spent inventing detail the sources never had.

The target is now the size most of the clips already are, with the largest
breaking a tie. The odd clip is converted, rather than everything else being
converted to accommodate it. It is still always some input's real dimensions and
never a computed box, so a landscape clip merged with a portrait one does not
produce a square that neither of them is.

Frame rate is still the highest in the set, and deliberately so: raising 30 to
60 duplicates frames, loses nothing, and costs double, while lowering 60 to 30
would discard motion that cannot come back.

## An implausible frame rate no longer turns a short merge into a long one

ffprobe reports `r_frame_rate` as the lowest rate that can express every
timestamp in a stream exactly — not the rate the clip actually plays at. One
irregular gap is enough to make it report hundreds of frames per second. Because
the merge pinned every clip to the highest rate it saw, and encoding time scales
with that rate directly, one such file among the inputs could stretch a quick
merge into a very long one.

Rates above 240fps are now treated as the measurement artefact they are and
ignored when the target is chosen. 240 covers every high-speed mode a consumer
camera offers, and the cap applies only to this choice — nothing is rejected and
no clip is refused.

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
| `MPC-HC.Video.Editor.zip` | `DEEE95BEA0EEFA3221EE93E4EABEA9BF801156C4F469CE575FA46FFE81035BE4` |
| `MPC-HC Video Editor.exe` | `764F907D2FDE0E7A83B640935E88051D3E9A580112F6B2557F692E691EA6AB69` |
| `ffmpeg.exe` | `4A01142006A4E2359293E072957DCDA7760C2003BBEEDE037B4551F2CFC8406F` |
| `ffprobe.exe` | `8B5298DA673B85E628FBC98535A88848E939E16DF72E856FC727E01AA667E243` |
| `LICENSE` | `3972DC9744F6499F0F9B2DBF76696F2AE7AD8AF9B23DDE66D6AF86C9DFB36986` |

FFmpeg is unchanged — still 9.0.1, GPL v3, from
[BtbN/FFmpeg-Builds](https://github.com/BtbN/FFmpeg-Builds).
