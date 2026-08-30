# MPC-HC Video Editor 4.0

Nine features, and a new version scheme. Releases are two-part from here —
4.0, 4.1, 4.2 — rather than the three-part 3.0.x numbering that came before.

## Finding clips for you

**Bookmarks ▸ Find clips automatically…** scans a video and proposes a set of
clips. Three ways of looking, for three kinds of material:

- **Silence** — for anything speech-driven, where the gaps are the joins
- **Black frames** — for recordings that fade between segments, and adverts
- **Scene changes** — for edited footage with hard cuts and no audio cue

Scanning proposes; it never applies. The list comes back with everything ticked
and you untick what it got wrong, because detection on real footage is a good
first guess and nothing more.

## Playlists that survive a reorganised library

**Find moved files…** is the counterpart to removing dead entries, and usually
the one actually wanted. A file that is not where the playlist says has far more
often been moved than deleted. Point it at a folder and it searches for the
missing files by name and repoints the playlist at them.

Entries on a disconnected drive are never touched by it — their files are
probably exactly where they always were, and quietly repointing them at a
same-named file elsewhere would be worse than doing nothing.

**`.m3u8` playlists** are now read and written alongside `.pls`, and either can
be converted to the other. m3u8 is UTF-8 by definition, which sidesteps the
whole class of encoding problem that older `.pls` files carry.

**Relative playlist entries now resolve against the playlist's own folder**
rather than wherever the application happened to be started from. That was
simply wrong before — a perfectly good relative playlist read as entirely
missing. It also makes a playlist portable: a folder and its `.m3u8` can be
copied to a USB stick and still work.

## Knowing what you actually got

**Output is now checked against what was asked for.** A copied cut can only
begin at a keyframe, so a clip can run over a second longer than the range you
marked — and nothing used to say so. Now it does:

> The finished file is 1.1s longer than the marked range (0:04 against 0:03).
> Copied cuts can only start at a keyframe, so a clip may begin before its mark.
> Turn on Precise in Settings ▸ Encoding ▸ Cut accuracy to cut exactly.

**Failed jobs explain themselves.** ffmpeg had been reporting its reasons all
along; the application was reading that stream only to scrape a progress
percentage and throwing the rest away, leaving you with an exit code. The last
few lines are now kept and shown, and a GPU encoder is named when one is
selected.

## Seeing the audio

The timeline now draws the video's **waveform** behind the range marks, so
where sound actually is can be seen while marking rather than scrubbed for. It
renders in the background and simply does not appear for a video with no audio.

## Naming and output

**Filename templates.** The naming tags cover "put [done] on the end"; a
template covers what they cannot:

```
{name}_{index2}_{start}-{end}_{duration}s{suffix}
    → holiday_03_0-01-23-0-01-45_22s[done].mp4
```

`{name}` `{suffix}` `{index}` `{index2}` `{start}` `{end}` `{duration}` `{date}`,
with a live example in Settings. The default reproduces the previous naming
exactly, so an untouched installation writes the same filenames it always has —
and the result still goes through the project's filename rules, so a template
cannot produce a path the filesystem rejects.

**Export as chapters.** The alternative to cutting: one file with the bookmarks
attached as navigable chapter marks, instead of N files on disk. Streams are
copied, so it is quick and lossless.

**Loudness normalisation.** An optional EBU R128 pass so a merge of material
recorded at different levels does not jump in volume. Like precise cutting it
forces a re-encode, which the setting says outright.

## A silent data-loss bug, fixed

Bookmark CSVs were read as UTF-8 without checking. A CSV written in the legacy
Windows code page — which the AutoHotkey predecessor to this application would
have produced — turns every accented character into a replacement character,
and the stored path then matches no file on disk. The same defect was fixed for
playlists in 3.0.6; the detection is now shared, and both use it.

If you have CSVs carried over from the older tool, this is the release that
reads them correctly.

## Requirements

- Windows, .NET 8 desktop runtime
- MPC-HC with the web interface enabled (Options ▸ Player ▸ Web Interface) —
  the app detects the port itself and will tell you plainly if the interface is
  switched off
- ffmpeg and ffprobe are bundled. Your own build dropped beside the executable
  still takes precedence.

## Verify your download

```powershell
Get-FileHash -Algorithm SHA256 .\MPC-HC.Video.Editor.zip
```

| File | SHA-256 |
| ---- | ------- |
| `MPC-HC.Video.Editor.zip` | `BF74619095C5F17CA269B4AAEBF78746F42E2FE8965CF968CAB3388FA66166F2` |
| `MPC-HC Video Editor.exe` | `4C42FCF34F3553A26B39631096B0484C5F02E2EBEB77F8399B1880CAD965EB3F` |
| `ffmpeg.exe` | `4A01142006A4E2359293E072957DCDA7760C2003BBEEDE037B4551F2CFC8406F` |
| `ffprobe.exe` | `8B5298DA673B85E628FBC98535A88848E939E16DF72E856FC727E01AA667E243` |
| `LICENSE` | `3972DC9744F6499F0F9B2DBF76696F2AE7AD8AF9B23DDE66D6AF86C9DFB36986` |

FFmpeg is unchanged from 3.0.6 — still 9.0.1, GPL v3, from
[BtbN/FFmpeg-Builds](https://github.com/BtbN/FFmpeg-Builds).
