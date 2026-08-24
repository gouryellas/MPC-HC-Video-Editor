# MPC-HC Video Editor 3.0.8

Fixes the clip preview added in 3.0.7, which was effectively invisible.

## The clip preview now actually appears

3.0.7 introduced a panel showing the first and last frame of a clip. It only
ever drew once a bookmark row had been *selected* — and nothing anywhere said
so. With bookmarks in the list and none clicked, the entire feature was hidden
with no hint that it existed.

The natural thing to click on a row made it worse: clicking a timestamp seeks
the player, so the most obvious gesture revealed nothing.

**It no longer needs a selection.** With a video open and at least one complete
bookmark, the panel is simply there. Clicking a row still switches which clip
it shows.

Three related gaps closed at the same time. The panel now updates when
bookmarks are added or removed, when a bookmark gains its closing timestamp,
and when the loaded video changes — previously only changing the selection
redrew it, so it could sit showing a clip from a video no longer open.

## Smaller changes

The heading reads **CLIP [1]** rather than "SELECTED CLIP". With nothing
selected the panel shows the first clip, and calling that "selected" would be a
small untruth printed every time the app opened.

Underneath it now says what the clip will run to, and — when there is more than
one — that clicking a row previews another. The interaction is stated instead
of assumed.

## Unchanged

Everything else from 3.0.7 stands: cut accuracy, hardware encoding, web
interface detection, and the playlist work. FFmpeg is still 9.0.1, GPL v3, from
[BtbN/FFmpeg-Builds](https://github.com/BtbN/FFmpeg-Builds).

## Requirements

- Windows, .NET 8 desktop runtime
- MPC-HC with the web interface enabled (Options ▸ Player ▸ Web Interface) —
  the app will tell you plainly if it is not
- ffmpeg and ffprobe are bundled. Your own build dropped beside the executable
  still takes precedence.

## Verify your download

```powershell
Get-FileHash -Algorithm SHA256 .\MPC-HC.Video.Editor.zip
```

| File | SHA-256 |
| ---- | ------- |
| `MPC-HC.Video.Editor.zip` | `4DCB6C4FE24B109F53F2B6F048B64AAE2BD7BD87378B5324D8C8D85DC0589F86` |
| `MPC-HC Video Editor.exe` | `9ECD2CCB413BD0CB3F086DDB5534044530B66AC62D5AD55356A550F235088298` |
| `ffmpeg.exe` | `4A01142006A4E2359293E072957DCDA7760C2003BBEEDE037B4551F2CFC8406F` |
| `ffprobe.exe` | `8B5298DA673B85E628FBC98535A88848E939E16DF72E856FC727E01AA667E243` |
| `LICENSE` | `3972DC9744F6499F0F9B2DBF76696F2AE7AD8AF9B23DDE66D6AF86C9DFB36986` |
