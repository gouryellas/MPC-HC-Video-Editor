# MPC-HC Video Editor 4.5

The program now tells you when there is a newer version.

## It says when a newer release has been published

A fix released to a page nobody is watching reaches nobody. Every version
until this one had to be found by going and looking.

Once, as the program starts, it asks GitHub for the latest release and
compares the version with the running build. If that version is newer, a
notice says so and offers the releases page. If it is not, nothing appears.

It reads a version number and nothing else. Nothing is downloaded and nothing
installs itself — this is a portable folder, and replacing it stays your
business. The notice only points you at the page.

The way to stop the check is in the notice itself, not only in a settings tab
you would have to go looking for, and the same line says where to turn it back
on:

> This check runs once each time the program starts.
> **Stop checking for new versions** — you can turn it back on at any time in
> Settings ▸ General ▸ Updates, or check by hand from Help ▸ Check for updates.

A machine that is offline when the program opens has not run into a problem
worth a dialog, so a check that cannot complete says nothing at all.

## The setting

**Settings ▸ General ▸ Updates**, on by default — including on an existing
install, whose settings file predates the setting and so takes the default.
That is deliberate: the people most worth telling about a fix are the ones
already running the build it fixes.

Turn it off and the program never contacts GitHub on its own.

## Checking on demand

**Help ▸ Check for updates** asks whenever you want it, and answers either
way — an up-to-date install says so rather than appearing to do nothing. It
works whether or not the startup check is turned on.

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
| `MPC-HC.Video.Editor.zip` | `D57808B567139B0465DA5B3C90DD1ED93C5F33AC6E10710E757C013540F10877` |
| `MPC-HC Video Editor.exe` | `74BE6E29D05C8E29AC0A52838E14AD0B4A347C27BAB71898B5DDA2F8DC33EE98` |
| `ffmpeg.exe` | `4A01142006A4E2359293E072957DCDA7760C2003BBEEDE037B4551F2CFC8406F` |
| `ffprobe.exe` | `8B5298DA673B85E628FBC98535A88848E939E16DF72E856FC727E01AA667E243` |
| `LICENSE` | `3972DC9744F6499F0F9B2DBF76696F2AE7AD8AF9B23DDE66D6AF86C9DFB36986` |

FFmpeg is unchanged — still 9.0.1, GPL v3, from
[BtbN/FFmpeg-Builds](https://github.com/BtbN/FFmpeg-Builds).
