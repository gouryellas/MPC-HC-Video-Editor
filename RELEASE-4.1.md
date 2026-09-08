# MPC-HC Video Editor 4.1

A new look, and the interface's colours are now yours to choose.

## Colour themes

**Settings ▸ General ▸ Appearance** offers three:

- **Graphite** — warm charcoal with an amber accent. The new default.
- **Midnight** — cool blue-grey with a cyan accent.
- **Daylight** — light surfaces with an indigo accent.

Picking one applies it immediately, so it can be judged rather than guessed at
from a name. Cancel puts the previous one back, including if the dialog is
closed with Escape.

Underneath, every colour in the interface now resolves through a named role
rather than being written out where it is used. The application previously
spelled out 55 distinct values across twelve files, which is why it had exactly
one appearance; a fourth theme is now a single entry in one table.

## A new icon, and it follows the theme

The application icon is now a film camera, drawn in the colours of whichever
theme is active. The window icon, the taskbar icon while the program is
running, and the notification-area icon all repaint when the theme changes, and
come back in the right colours the next time the program is opened.

**One exception, stated plainly:** the icon on the program file itself — what
Explorer shows, and what a pinned shortcut uses — is fixed when the program is
built and cannot follow the theme. Changing it would mean the program rewriting
its own executable, which would invalidate the published checksums below and is
a thing well-behaved software does not do.

## Filename variables, explained properly

The naming pattern settings used to describe their variables in a single dense
paragraph, with two — `{index}` and `{index2}` — whose difference the names did
nothing to convey.

They are now a table: each variable, a worked example of what it produces, and
what it is for. Click one to insert it. Four ready-made patterns sit below,
each with its actual output, applied with a click.

The numbering variables now say what they do:

| | | |
| --- | --- | --- |
| `{number}` | `3` | Which clip this is. |
| `{number2}` | `03` | The same number to two digits, so ten clips sort 01, 02 … 10 rather than 1, 10, 2. |
| `{number3}` | `003` | Three digits, for lists past ninety-nine. |

The trailing digit is the width, which makes the set read itself.

A pattern saved under 4.0 using the old names is rewritten automatically. The
old names also still expand, so no filename changes as a result of this.

## Curly braces are allowed in filenames

`{` and `}` may now appear in naming tags and output names. Windows never
objected to them; only this program's own rules did.

A useful side effect: a mistyped variable survives visibly. `{nam}` now produces
`{nam}[done].mp4` rather than silently becoming `nam[done].mp4`, so the mistake
is one you can see. Characters Windows genuinely rejects — a colon, slash or
asterisk — are still removed.

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
| `MPC-HC.Video.Editor.zip` | `BCE30165101A380B48AEBB0EC3AAA6F26BD33597DAFE1205627A2178DC9F0EEC` |
| `MPC-HC Video Editor.exe` | `76710B4BA8DD540EC0792AF265F13D8A0F8415FF792C32E23CF34B1C7F6EBB55` |
| `ffmpeg.exe` | `4A01142006A4E2359293E072957DCDA7760C2003BBEEDE037B4551F2CFC8406F` |
| `ffprobe.exe` | `8B5298DA673B85E628FBC98535A88848E939E16DF72E856FC727E01AA667E243` |
| `LICENSE` | `3972DC9744F6499F0F9B2DBF76696F2AE7AD8AF9B23DDE66D6AF86C9DFB36986` |

FFmpeg is unchanged — still 9.0.1, GPL v3, from
[BtbN/FFmpeg-Builds](https://github.com/BtbN/FFmpeg-Builds).
