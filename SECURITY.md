# Security Policy

MPC-HC Video Editor is a portable Windows desktop application. It runs as the
logged-in user, needs no elevation, installs nothing, and listens on no network
port. This document describes what it actually does on your machine, what is
known to be weak, and how to report something that isn't listed here.

---

## Supported versions

| Version | Supported |
| ------- | --------- |
| 3.0.8   | Yes       |
| 3.0.7 and earlier | No |

This is a single-maintainer project. Only the latest release gets fixes — there
are no backport branches. If you are on an older build, update before reporting
anything.

---

## Reporting a vulnerability

Use **GitHub's private vulnerability reporting** on this repository: the
*Security* tab → *Report a vulnerability*. That opens a private thread visible
only to the maintainer.

**Do not open a public issue for a security problem.** The public bug tracker is
for ordinary bugs.

Please include:

- The version (Help → About, or the release you downloaded).
- What an attacker gains, concretely — code execution, file overwrite outside
  the chosen folder, reading files the user did not select.
- Steps to reproduce, and a sample file if one is involved. If the sample is
  malicious media, say so plainly and do not attach it to anything public.
- Your Windows version and, if relevant, where the app is installed from.

**What to expect.** This is a hobby project maintained by one person, so no
response-time guarantee is offered in good faith. Realistically: an
acknowledgement within about a week, and a fix in the next release for anything
that holds up. If a report is going to sit longer than that, you will be told
rather than left waiting. You will be credited in the release notes unless you
ask not to be.

---

## What the application does

Stated plainly, so you can judge the surface for yourself.

**Processes it starts.** `ffmpeg.exe` and `ffprobe.exe` for all media work;
MPC-HC itself, to open a video; `explorer.exe`, to reveal a file; `cmd.exe`, to
read a file association via `assoc`/`ftype`; `rundll32.exe`, for the Windows
*Open With* dialog; `where`, to locate binaries. All of these run as the current
user with no elevation.

**Network.** One outbound HTTP request, to `http://127.0.0.1:13579` — the MPC-HC
web interface, used to read and set playback position. The port is configurable
in Settings. The request never leaves the loopback interface, and the app opens
no listening socket of its own.

**There is no update check, no telemetry, no crash reporting, and no analytics.**
Nothing about your files, your usage, or your machine is transmitted anywhere.
The only other outbound connection possible is your default browser opening the
project page when you click the link in the About dialog.

**Files it writes.** Everything lives beside the executable — `settings.json`
(folder paths, shortcuts, hotkeys, naming tags) and `stalls.log` (UI-responsiveness
timings, no file paths). Output clips go where you point them. On first launch
it reads `%APPDATA%\MPC-HC Video Editor\settings.json` once, if present, to carry
over an older non-portable install; nothing is written back there.

**Single-instance lock.** A session-local named mutex (`Local\`, not `Global\`),
so it cannot be squatted from another user's session.

---

## Known weaknesses

These are real and currently unfixed. They are listed here rather than left for
you to discover.

### Bundled FFmpeg — update if you are on 3.0.5 or earlier

**3.0.5 and every release before it shipped FFmpeg 4.2.1, built in 2019** —
five major versions behind, on a branch long past upstream end-of-life, and
carrying publicly documented, upstream-fixed vulnerabilities in its demuxers
and decoders. This matters more here than it would elsewhere, because feeding
media files to FFmpeg is the entire point of the application: **opening a
maliciously crafted video file is the most plausible route to code execution.**
If you are on 3.0.5 or older, update.

3.0.6 onward bundles **FFmpeg 9.0.1**, a GPL v3 build from the BtbN builds that
ffmpeg.org links as an official Windows source. Keeping it current is a
release-time responsibility — the application never checks for updates or
contacts anything at runtime, by design.

You can always substitute your own build: drop `ffmpeg.exe` and `ffprobe.exe`
beside the executable and they take precedence over everything else, with no
configuration.

### Binaries are unsigned

Releases carry no Authenticode signature. SmartScreen will warn on first run,
and there is no cryptographic way to prove a download came from this project.
Verify the SHA-256 hashes published with each release. Code signing costs money
this project does not have.

### Binary resolution order

FFmpeg and FFprobe are located in this order: beside the executable, then via
`where`, then each `PATH` entry in turn. `where` searches the **current working
directory before PATH**. If the app is launched with a working directory an
attacker controls *and* no bundled copy is present, a planted `ffmpeg.exe` could
be run. Shipping the binaries in the archive is what keeps normal installs off
this path — it is a deliberate mitigation, not an accident.

The same applies to `where`, `cmd.exe`, `explorer.exe` and `rundll32.exe`, which
are started by bare name and so follow the Windows `CreateProcess` search order
(application directory and current directory before `System32`). Anyone who can
write to the application directory can already replace the application itself,
so this only widens an existing compromise rather than creating one.

### Portable data is as protected as its folder

`settings.json` is plain text containing local filesystem paths, and it sits
beside the executable with no special permissions. Installing to a location
other users can read — a shared drive, a sync folder, a world-readable
directory — exposes those paths. Keep the folder somewhere only you can read.

---

## Release integrity

Every release archive from 3.0.6 onward contains exactly these four files:

```
MPC-HC Video Editor.exe
ffmpeg.exe
ffprobe.exe
LICENSE
```

No configuration, no logs, no user data. If an archive from this project
contains anything else, treat it as suspect and report it.

**SHA-256 hashes are published in the release notes, starting with 3.0.6.**
Releases up to and including 3.0.5 predate this and have none — there is no way
to verify those downloads, which is another reason to take the newest release.
Verify with:

```powershell
Get-FileHash -Algorithm SHA256 .\MPC-HC.Video.Editor.zip
```

### Past advisories

**v3.0 (2 August 2026) — maintainer data in the release archive.** The v3.0
archive was packaged with the maintainer's own `settings.json` and `stalls.log`
included. The settings file exposed local folder paths and a Windows username;
`RecentVideos` was empty, so no filenames or viewing history were disclosed.
No user of the application was affected — the data was the maintainer's own.
Fixed in v3.0.1, which restricted the archive to the three executables above.

---

## Out of scope

- Vulnerabilities in **MPC-HC** itself — report those to the MPC-HC project.
- Vulnerabilities in **FFmpeg** upstream — report those to FFmpeg, but do tell
  us, since it may justify pulling the bundled version forward.
- Attacks requiring administrator rights, physical access, or the ability to
  write to the application folder. If you can write there, you can replace the
  executable, and nothing this app does changes that.
- Missing compiler or OS hardening flags, absent a demonstrated exploit.
- The absence of code signing, which is documented above.
- Social engineering, and reports consisting only of automated scanner output.

---

## Disclosure

Coordinated disclosure, please. Give a reasonable window to ship a fix before
publishing — 90 days is the usual expectation and is more than enough here.
If a reported issue is being actively exploited, say so and it will be
prioritised over everything else.
