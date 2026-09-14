# MPC-HC Video Editor 4.9.2

Merging clips that do not match now produces a file that plays. The progress
counter stops running ahead of its own bar, and the pencil that opens the
bookmark file stops disappearing.

## Merging mixed clips produced a file that stalled

Join a 30fps clip to a 60fps one and playback locked up partway through —
usually at the point where the second clip began.

The merge re-encodes every input before joining them, and that step brought the
codec, pixel format and audio into line but left the frame rate alone. The
joining step does not re-time anything it is given, so the result was a single
file containing two different frame rates, under a container that declared one:

```
r_frame_rate   60/1        the container says 60
avg_frame_rate 8100/181    it is really 44.75, which matches neither clip
frame spacing  33ms for the first clip, 17ms for the second
```

Some players ride over that. MPC-HC stalls at the discontinuity, which reads as
the merge having produced a broken file from that point on.

Every clip is now brought to one frame rate before the join, and that rate is
the **highest** among them. Raising 30 to 60 duplicates frames and loses
nothing; lowering 60 to 30 would throw away half of that clip's motion to suit
the other one. Broadcast rates are carried exactly — 29.97 is handled as
30000/1001, not as a rounded decimal. This costs no extra work: every clip was
already being re-encoded at that point.

## Clips of different sizes were worse

The same step left frame size alone too. Joining a 640x360 clip to a 1280x720
one gave a file whose container said 640x360 while half its frames were 720p.

Every clip is now brought to one frame size as well — the size of the largest
clip in the set, picked by pixel count so the output matches a real source
rather than some computed box none of them has. Smaller or differently shaped
clips are **fitted inside it and padded with black**, keeping their geometry: a
4:3 clip joined to a 16:9 one gains bars down the sides rather than being
stretched. Pixel aspect ratio is normalised with it, which matters for clips
that share a size but declare different pixel shapes — the join used to take
only the first and silently stretch the rest.

## Audio drifted at every join

Found while fixing the above. AAC encodes 1024 samples at a time, so a clip
whose video runs exactly 2.000s carries slightly more audio than that — the
encoder cannot emit a partial frame. Every clip's audio outlasted its own video,
the next clip started before the previous had finished, and the error compounded
once per join. On a merge of several clips that is audio sliding out of sync.

The audio is now made continuous across the joined timeline rather than stitched
together from padded pieces. The video is still copied, so the join stays as
fast as it was.

## The progress counter ran ahead of the bar

A merge of five files reached `5/5` at around 67% and then sat there for the
rest of the job.

The counter was reporting the file it was *starting* while the percentage
reported work *finished*, so the two disagreed by a step for the whole job — and
on a merge by more than that, because the final join is a step the counter had
no room for. It now counts what is finished, so it always matches the bar: that
same merge reads `4/5` at 67% and reaches `5/5` at the join.

This was not only merge. Split, convert images, convert video and strip audio
all had the same off-by-one. All five now agree with their own progress bars.

## The pencil beside CURRENT BOOKMARKS kept vanishing

The pencil that opens the bookmark file for editing disappeared whenever the
list held only an opening timestamp with no closing one.

It is hidden when there is nothing to act on, and "something to act on" was read
as at least one complete pair. But the bookmark file is created by the *first*
timestamp, so a lone opening timestamp is a file that exists and has a row in
it — named on screen, with no way to open it. Editing by hand is exactly how a
stray timestamp gets fixed, so it went missing at the moment it was most useful.

It now follows whether the file exists, matching **Delete bookmarks** beside it.
**Bookmarks ▸ Edit bookmarks** was greyed out in the same situation and is fixed
with it.

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
| `MPC-HC.Video.Editor.zip` | `59EEFE4B243D80AEB29FA84F29842F00594A522A3AB462B1D631E05267B6E87F` |
| `MPC-HC Video Editor.exe` | `A119825352A4D0EC792441F3881B4DDB44D683D58F13E40A655E0183022C1711` |
| `ffmpeg.exe` | `4A01142006A4E2359293E072957DCDA7760C2003BBEEDE037B4551F2CFC8406F` |
| `ffprobe.exe` | `8B5298DA673B85E628FBC98535A88848E939E16DF72E856FC727E01AA667E243` |
| `LICENSE` | `3972DC9744F6499F0F9B2DBF76696F2AE7AD8AF9B23DDE66D6AF86C9DFB36986` |

FFmpeg is unchanged — still 9.0.1, GPL v3, from
[BtbN/FFmpeg-Builds](https://github.com/BtbN/FFmpeg-Builds).
