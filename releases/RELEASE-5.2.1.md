# MPC-HC Video Editor 5.2.1

Marking fixes. A timestamp that cannot be used is refused rather than added,
the refusal is said over the player instead of into a status bar you cannot
see, and playback no longer waits for a second cut.

## Marks go forwards

A new cut has to open at or after the latest time already marked. Anything
behind that is refused and nothing is added.

The old rule only refused a mark that landed *inside* an existing cut, which
left every gap behind the work already done open to a new one: with a cut at
3s–4s, marking at 2s was accepted. That is backwards in both senses, and the
row it produced had to be deleted by hand.

**A mark on a cut's first second is refused too.** It was accepted before, and
not for a reason anyone would guess: the check asked whether a span of no
length overlapped anything, and that question is false at exactly a cut's
opening instant. Anywhere else inside the cut was caught. Only the first second
was not, which is precisely the second you land on after nudging a cut's end
back to where you were watching.

Landing exactly on the last mark is still allowed — that is how one cut starts
where the last one ended.

A scan is the exception, since it proposes the whole file at once. To re-cut an
earlier stretch, delete the cuts after it or scan with **replace**.

## Refusals are shown over the player

Every refused press now floats a notice above whatever is on screen, for four
and a half seconds, naming the time it refused and the time it wanted:

> **Timestamp not added** — 0:02 is behind the last mark at 0:04. Marks go
> forwards.

This used to be gated the same way confirmations are, which holds them back
while the compact overlay is up — that is, while a video is playing, which is
the only time the hotkey is pressed. With toasts switched off it said nothing
at all, and the status bar it fell back to is behind the player.

The gate's reasoning does not hold for a refusal anyway. It stands down when
the overlay is up because the overlay already shows what happened: true of a
timestamp that was set, since a row appears, and false of one that was refused,
because the list is exactly as it was.

It is held longer than a confirmation, too. "Timestamp 3 set" only has to be
noticed; a sentence with two times in it has to be read.

Confirmations are unchanged and still respect the setting.

## Playing one cut

**Play all cuts** and **Play selected cuts** wanted two complete pairs before
they would turn on. One cut is when playback is most useful — seek to its
start, stop at its end, and see whether it is the piece you meant, which is
what you want immediately after marking your first one. Merge and Split have
always worked on a single pair.

## Smaller things

- the animated WebP option says which tools cannot play one. The export is
  sound — a file a desktop image viewer calls unreadable decodes frame for
  frame in any browser — but plenty of viewers handle only still WebP and
  report the file as broken rather than as unsupported, and GIF working in the
  same viewer makes that look like our fault

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
| `MPC-HC.Video.Editor.zip` | `C2430F2266895E48C4820D93B9C1778458FF38306863A760070024E5CC325952` |
| `MPC-HC Video Editor.exe` | `FEE63799D46BD5F6CA4E37A7B90438D9FC283E4568449D8C48624FC6769BEE47` |
| `ffmpeg.exe` | `4A01142006A4E2359293E072957DCDA7760C2003BBEEDE037B4551F2CFC8406F` |
| `ffprobe.exe` | `8B5298DA673B85E628FBC98535A88848E939E16DF72E856FC727E01AA667E243` |
| `LICENSE` | `3972DC9744F6499F0F9B2DBF76696F2AE7AD8AF9B23DDE66D6AF86C9DFB36986` |

FFmpeg is unchanged — still 9.0.1, GPL v3, from
[BtbN/FFmpeg-Builds](https://github.com/BtbN/FFmpeg-Builds).
