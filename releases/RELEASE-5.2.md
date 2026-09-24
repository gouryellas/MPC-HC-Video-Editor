# MPC-HC Video Editor 5.2

Five things a cut can now do that it could not before: fade in and out, be
exported as a GIF, be normalized for loudness, give up a single frame as a
picture, and be seen as a waveform. Two new themes. Strip audio asks what it
should produce, and a long job tells you when it is done.

## New to the toolbar

**Fade.** Select a cut, press ◐ Fade, and it comes up from black at the start
and goes down at the end. The length is one setting for all of them, under
Settings ▸ Cuts, because a fade that varies clip by clip is a thing to tune
rather than a thing to use. Audio fades with the picture. A faded cut is
re-encoded — there is no way to dissolve a frame without touching it — and says
so on its row.

**Save the frame on screen.** The picture the player is showing, written as a
PNG at the video's own resolution. It reads the position from MPC-HC and pulls
that frame out of the file, so the image is the frame itself rather than a
screenshot of a window with a video in it.

**Export a cut as a GIF or WebP.** Any cut, at a size and frame rate you pick.
GIF builds its own palette from the cut rather than taking the stock 216
colours, which is the difference between a gradient and a mess of bands. WebP
is smaller and keeps more colour, when whatever you are posting it to accepts
one.

**Normalize audio** now applies to Convert as well as to cuts, so a converted
file lands at the same loudness as a clip cut from it.

## The waveform is back

5.1 removed it because nothing drew it. It draws now: a dim outline of the
audio behind the timeline, with the cuts and the playback position on top of
it, so a silent stretch is visible before you go looking for it.

It is generated once per video, in the background, and the timeline works
normally while it is being made.

## Strip audio asks what it is for

"Strip audio" reads three ways and the program only did one of them. It now
asks, once per run:

- the audio on its own, as an MP3
- the video on its own, with its sound removed
- both

It also says on the dialog that the file you picked is not changed — the one
setting that does delete it is three tabs into Settings, and that is not
something to leave to be discovered.

**The silent copy is named `-silent`.** This is not decoration. MPC-HC, and
most players, attach an audio file sitting next to a video whose name starts
with the video's name. Writing `clip[tag].mp3` beside `clip[tag].mp4` handed
the player a matched pair, and the silent copy played with the very sound that
had been taken out of it — a genuinely silent file that nobody would believe
was silent. Lengthening the video's name is what breaks the match.

## A sound when a job finishes

Settings ▸ Notifications ▸ **Play a sound when an operation finishes**, on by
default. It is for the encode you walked away from, which is a thing you only
benefit from if it is already on when you leave.

It hangs off the one place every successful operation ends, so a cancelled or
failed job stays silent. Windows' own tada, with four stock sounds behind it if
that file is missing. Nothing in the chain is an error sound.

## Two more themes

**Obsidian**, near-black with a blue accent, for a dark room. **Parchment**,
warm paper and teal, for a bright one. That makes five.

**The operation buttons keep their colours in every theme.** Merge, Split,
Convert and Strip audio are the four things that write files, and they are now
the same four colours whichever theme is on — you learn where they are once.
Their labels are white in all five, which the light themes needed and the dark
ones do not mind.

## Cuts, and the rules about them

**Overlapping is refused; touching is fine.** A cut ending at 10s and the next
starting at 10s is a pair, not a conflict. Two cuts running through each other
produce two clips of the same footage and a merge that plays it twice. Every
way of making a cut now applies the same test.

**Closing a bookmark before its own opening no longer throws the pair away.**
It says why and leaves the bookmark open, because the opening time was never
the thing that was wrong.

**The arrows move a whole second.** They were moving by a frame, which at 24fps
is four hundredths of a second — invisible on a display that shows whole
seconds, and lost entirely when the bookmarks were saved. There are no
fractions of a second anywhere now: every duration is rounded to the nearest
one, so a cut that measures 16.67 seconds reads as 17 rather than 16.

**The arrows can take a cut right up against its neighbour.** They used to stop
a second short, which was the old no-touching rule outliving itself.

**The cut marks draw over the playback position**, not under it. A bracket
hidden behind the bar it belongs to is not a mark.

## Names and prompts

**A naming tag is optional.** Options ▸ Naming tag ▸ **None** writes
`clip.mp4` rather than `clip[done].mp4`. A second file of the same name still
gets a number, since two files in a folder cannot share one.

**Every output prompt names the folder it is saving into.** "Saves as" answers
half the question, and the half it left out is the one that matters when output
is going somewhere other than beside the video.

**The name is asked once per file, not once per file written.** Strip audio
with both outputs asked the same question twice, and dismissing the second one
— which reads as a repeat — silently dropped that output while the summary
claimed it had been written. Summaries now count what actually landed.

## The overlay

Its rows read like the main window's: same colours, same spacing, same
hover. The filename line is gone — the overlay is on screen because you are
watching that video — and an unfinished bookmark reads `(incomplete)`.

**Its timestamps seek by default.** The setting that makes them clickable
arrived in 4.9 and did nothing until you found it. Turning it off still
makes the overlay click-through, which is worth having; it is now the thing you
turn on rather than the thing you have to.

## Update notices say what changed

The check already received the release notes alongside the version number and
threw them away. The notice now lists up to six lines from them, so "is this
worth downloading" is answerable without opening a browser. It also explains
that replacing the whole folder keeps your settings, which it does — every save
is mirrored to %APPDATA% and restored into a folder that arrives without one.

## Smaller things

- PLAY SPEED is centred over its slider
- the image-deletion prompt asks the question instead of restating the
  operation
- "Add current video" on a playlist submenu is disabled until a video is loaded
- chapters are written in timeline order
- the release notes for every version live in `releases/` rather than filling
  the repository root

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
| `MPC-HC.Video.Editor.zip` | `D838D89179E9812602715C96412C34C4E0BD59D236EEFFBC84F38B8B290195A3` |
| `MPC-HC Video Editor.exe` | `514DF1C7922A2E6E48F929C060E253F82725BEAC2DC80A87D969878E3637D7A4` |
| `ffmpeg.exe` | `4A01142006A4E2359293E072957DCDA7760C2003BBEEDE037B4551F2CFC8406F` |
| `ffprobe.exe` | `8B5298DA673B85E628FBC98535A88848E939E16DF72E856FC727E01AA667E243` |
| `LICENSE` | `3972DC9744F6499F0F9B2DBF76696F2AE7AD8AF9B23DDE66D6AF86C9DFB36986` |

FFmpeg is unchanged — still 9.0.1, GPL v3, from
[BtbN/FFmpeg-Builds](https://github.com/BtbN/FFmpeg-Builds).
