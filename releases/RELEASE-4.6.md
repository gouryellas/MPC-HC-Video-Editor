# MPC-HC Video Editor 4.6

Each cut can now be turned, silenced and named, either mark can be moved a
frame at a time, and every edit to the list can be undone.

## Rotate

**Rotate** cycles a cut through the four quarter turns, from one button beside
Flip. One target is taken for the whole selection rather than advancing each
cut from wherever it already was — otherwise a mixed selection stays mixed
forever, each press moving every cut one step and never bringing them into
agreement. Flip has always worked this way.

It is applied as `transpose`, before the speed filter, since it swaps width
and height.

## Mute

**Mute** silences a cut with `volume=0` rather than dropping the audio.

That distinction matters more than it looks. The concat demuxer requires every
segment to carry the same streams in the same order, so a segment with its
audio stripped cannot be joined to one that still has it. Silencing keeps the
stream in place carrying nothing, and a muted cut merges with an unmuted one.

## Chapter names

**Settings ▸ General ▸ Chapter names**, off by default. Each range is named as
it is created, and the name is editable on its row.

Only names the program assigned are renumbered when the list changes, so a cut
you have renamed by hand keeps its name wherever it ends up. Turning the
setting off clears every name, hand-typed ones included — that is what the
switch means, and the dialog says so before it does it.

Chapter export titles each chapter with its name when there is one.

## Frame nudging

Either mark moves by a single frame, using the rate read from the file itself.
`r_frame_rate` is rational, so `30000/1001` is divided rather than read as a
decimal. The two marks are kept at least one frame apart.

## Undo and redo

**Ctrl+Z / Ctrl+Y**, covering every edit to the cuts.

By snapshot rather than by reversible commands. A command per action would
have to be threaded through every place the list is mutated, and would still
miss anything edited straight through a data binding — which never passes a
command at all. Because of that, no existing command needed changing to gain
undo.

Edits within 600 ms coalesce, so one slider drag is one step. Ticking a
checkbox is not a step, though the selection travels in the snapshot, so undo
puts it back. History resets when a different video or bookmark file is
opened.

*File operations are deliberately excluded.* A finished merge has written an
encoded file and may have recycled its source. Undo there would be a promise
the program cannot keep.

## Reveal last output

Opens Explorer with the written file selected. The status bar has named that
file since 4.0, but naming a file is not the same as being able to get to it.

## The bookmark CSV carries the name and the per-clip settings

This was not optional. Speed and flip were held only in memory and lost
whenever the list reloaded — which the main window does *every time it is
activated* — so a name would not have survived alt-tabbing away.

The third field held `BookmarkN`, a row number that was discarded on load, and
now holds the name; a field still matching the old placeholder reads as no
name. Everything after it is optional, so a file written by any earlier build
loads unchanged.

## A menu bar reorder, and the bug it exposed

The menus are now File, Edit, Actions, Hotkey, Bookmarks, Playlist, History,
Shortcuts, Options, View, Help, each with a small glyph.

Adding those glyphs broke four menus, and the fix is the part worth recording.
`FindMenuItem` located top-level menus by matching header text exactly, so
changing `"_Options"` to `"⚙ _Options"` made the match fail. The method
returned null, every caller responded by returning quietly, and the naming
tags, Shortcuts, Playlist and quick-save lists silently stopped filling
themselves in. Nothing threw and nothing logged. The failure looked exactly
like having nothing to show.

This had already happened once before, recorded in a comment in the file: a
menu renamed from `_Suffix` to `_Options` hid newly added tags the same way.
So `FindMenuItem` is deleted rather than patched, and those four menus are
found by `x:Name` — checked by the compiler, and unable to drift when someone
rewords a header.

## Spelling

American spelling throughout the interface and the source. The historical
release notes are left as they were written.

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
| `MPC-HC.Video.Editor.zip` | `9FBD82A71BFAD77F38AEFB54BA5B2A3BDBF3D8A802E0AE523F06E77DF6F19555` |
| `MPC-HC Video Editor.exe` | `8C00A57213BBD04E1E9295F155AD5C65CE7C5A309910B4728B1EDB51503D613D` |
| `ffmpeg.exe` | `4A01142006A4E2359293E072957DCDA7760C2003BBEEDE037B4551F2CFC8406F` |
| `ffprobe.exe` | `8B5298DA673B85E628FBC98535A88848E939E16DF72E856FC727E01AA667E243` |
| `LICENSE` | `3972DC9744F6499F0F9B2DBF76696F2AE7AD8AF9B23DDE66D6AF86C9DFB36986` |

FFmpeg is unchanged — still 9.0.1, GPL v3, from
[BtbN/FFmpeg-Builds](https://github.com/BtbN/FFmpeg-Builds).
