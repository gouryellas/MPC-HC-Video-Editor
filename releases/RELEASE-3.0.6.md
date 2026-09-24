# MPC-HC Video Editor 3.0.6

A security release. No behaviour in the application itself has changed — what
changed is the FFmpeg it hands your files to.

## The reason to update

**Every release up to 3.0.5 bundled FFmpeg 4.2.1, built in August 2019.** That
is five major versions behind, on a branch long past upstream end-of-life, and
it carries publicly documented vulnerabilities that upstream has since fixed.

This matters more here than it would in most applications, because feeding
media files to FFmpeg is the whole point of this one. Opening a maliciously
crafted video is the most plausible route to code execution against anyone
running it. If your files come from your own camera and recordings the
practical risk was small; if any of them came from elsewhere, it was not.

**3.0.6 bundles FFmpeg 9.0.1** — a GPL v3 build from
[BtbN/FFmpeg-Builds](https://github.com/BtbN/FFmpeg-Builds), one of the two
Windows sources linked from ffmpeg.org.

## Verified, not assumed

Jumping five major versions can break command-line arguments, so every FFmpeg
invocation the application makes was run against the new build before this was
packaged: trimming by stream copy, trimming with the flip and speed filters
re-encoded, the `atempo` chain used for slow motion, all seven output formats
(MP4, MKV, MOV, AVI, WMV, MPG, WEBM), MP3 audio extraction, the segment-and-
concat path behind bulk merge, and the `ffprobe` duration query. All pass.

## Also in this release

**The LICENSE file is now in the archive.** It never had been, so Help ▸ About
told every user "no licence file is installed alongside this application" — and
shipping GPL-licensed binaries without the licence text is a compliance gap on
its own. Both are fixed.

**The About dialog's third-party section was wrong about FFmpeg.** It said
FFmpeg "is not distributed as part of this application", which has never been
true of these releases — it is in the archive. It now names the bundled
version, states the GPL v3 terms it is licensed under, and points at the
corresponding source.

## Two things worth knowing

**The download is larger — 169 MB, up from 108 MB.** That is entirely the new
FFmpeg binaries, which are roughly twice the size of the 2019 pair. Nothing was
added to the application itself.

**The binaries are still unsigned**, so SmartScreen will warn on first run.
There is no way around that without a code-signing certificate. Verify what you
downloaded instead:

```powershell
Get-FileHash -Algorithm SHA256 .\MPC-HC.Video.Editor.zip
```

| File | SHA-256 |
| ---- | ------- |
| `MPC-HC.Video.Editor.zip` | `32F501568406B287B9CA481CF6CC9932CB47D52DD2449DD810D65527251C1870` |
| `MPC-HC Video Editor.exe` | `AB727A11674D6CA1F072936CB101D0B2C8241EC8E48E4250889965EC10A261A0` |
| `ffmpeg.exe` | `4A01142006A4E2359293E072957DCDA7760C2003BBEEDE037B4551F2CFC8406F` |
| `ffprobe.exe` | `8B5298DA673B85E628FBC98535A88848E939E16DF72E856FC727E01AA667E243` |
| `LICENSE` | `3972DC9744F6499F0F9B2DBF76696F2AE7AD8AF9B23DDE66D6AF86C9DFB36986` |

These are the first release hashes this project has published. Earlier releases
have none, and there is no way to verify those downloads after the fact.

## Requirements

- Windows, .NET 8 desktop runtime
- MPC-HC with the web interface enabled (Options ▸ Player ▸ Web Interface)
- ffmpeg and ffprobe are bundled — no separate install. Your own build dropped
  beside the executable still takes precedence if you prefer one.
