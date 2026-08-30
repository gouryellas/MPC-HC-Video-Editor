# MPC-HC Video Editor 3.0.9

Two bug fixes, both found by going back over what 3.0.7 and 3.0.8 shipped.

## Clip previews could go permanently blank

Moving through the bookmark list cancels whichever preview is still being
rendered — that is deliberate, so the panel never shows a clip you have already
moved past. The mistake was what happened next: an interrupted render returned
nothing, and that nothing was **cached as the answer**.

So arrowing quickly past a clip and coming back to it left its two frames blank
for the rest of the session, with no way to bring them back short of loading a
different video. The faster you moved through the list, the more clips it
happened to.

The cache now keeps only real answers. A frame that genuinely cannot be read —
one past the end of the video, say — is still remembered as unavailable, since
re-running ffmpeg to fail again helps nobody. An interruption is no longer
mistaken for that.

## Failed encodes now say why

A job that failed reported only its exit code:

```
FFmpeg exited with code -1313558101
```

ffmpeg had explained itself on its error stream the whole time; the application
was reading that stream purely to scrape a progress percentage out of it and
discarding everything else.

Failures now carry the last few lines of what ffmpeg actually said. When a GPU
encoder is selected they also name it, because a hardware encoder that passed
the settings check can still fail on real content — an unusual resolution, a
bit depth the chip does not handle — and it is then the first thing worth
changing:

```
FFmpeg exited with code -1313558101.

[enc:h264_amf] Could not open encoder before EOF
Nothing was written into output file...
Conversion failed!

This used the AMD (AMF) encoder. If it keeps failing, switch the H.264
encoder back to Software in Settings ▸ Encoding — it works on any machine.
```

This applies to every failure, not only hardware encoding.

## Smaller fixes

The preview's cancellation token was being disposed while a render still held
it, which turned an ordinary cancellation into a different error inside a catch
that ignored everything — one of the routes into the blank-frame bug above.

A previewed clip that had its closing timestamp removed left its old frames on
screen. It now clears.

## Unchanged

Everything from 3.0.7 and 3.0.8 stands: cut accuracy, hardware encoding, web
interface detection, the clip preview, and the playlist work. FFmpeg is still
9.0.1, GPL v3, from [BtbN/FFmpeg-Builds](https://github.com/BtbN/FFmpeg-Builds).

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
| `MPC-HC.Video.Editor.zip` | `D1C1F027BBE7177256A89F1C71ED96984F5BD43E1630F4F7D7133BEA88AD729A` |
| `MPC-HC Video Editor.exe` | `6474936B5E6BD46E7973EB0A7410957A699B4CB118194F67B16F16033D4D9465` |
| `ffmpeg.exe` | `4A01142006A4E2359293E072957DCDA7760C2003BBEEDE037B4551F2CFC8406F` |
| `ffprobe.exe` | `8B5298DA673B85E628FBC98535A88848E939E16DF72E856FC727E01AA667E243` |
| `LICENSE` | `3972DC9744F6499F0F9B2DBF76696F2AE7AD8AF9B23DDE66D6AF86C9DFB36986` |
