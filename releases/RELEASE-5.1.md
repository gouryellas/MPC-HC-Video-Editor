# MPC-HC Video Editor 5.1

The timeline has been rebuilt again — one bar, with the clock and the cuts
inside it. Cuts can no longer be made to overlap, from anywhere. The frame
arrows act on the row they appear on, which they did not before.

## The timeline

5.0 gave the playback position and the bookmark ranges a lane each, stacked.
That fixed them drawing over one another and left a two-row strip carrying two
pieces of information, with the clock for a third sitting in the corner of the
status bar, a long way from the bar it described.

It is one rectangle now, with everything inside it. The position on the left,
the length on the right, and between them a dark track carrying the playback
position in blue with the cuts marked on the same line. The status bar keeps
only what it is for — reporting what just happened — and the time is where you
are already looking.

**The bar seeks.** Click anywhere on the track and the player goes there, using
the same command as clicking a timestamp in the list. The click is measured
against the inside of the bar, not its border, which stands for no time at all.

**A cut is two brackets and nothing between them.** A coloured band the length
of the cut was a great deal of paint for two numbers. It buried whatever shared
the bar with it, and on a busy edit the timeline became a row of blocks with
the gaps — the parts being thrown away — as the only legible thing left. Facing
feet read as "from here to there" on their own.

Between a pair goes the cut's number, and a tick when it is checked. A cut too
short for both keeps the tick and drops the number: the number says which row
this is, the tick says the next action will act on it. They are all white,
except the lone opening bracket of a bookmark still waiting to be closed, which
is red.

**The waveform is gone.** Nothing draws it any more, so generating it meant an
ffmpeg per video load decoding an entire audio track for a bitmap nobody would
see.

## Cuts can no longer overlap

Two cuts that run through each other produce two clips containing the same
footage, and a merge that plays it twice. Nothing in the program checked for
it.

The frame arrows were half a check and the only one. The hotkey would open a
bookmark inside an existing cut, or close one straight over the top of the next
one. A typed range was checked for running backwards but not for running
through its neighbours. A scan accepted without **replace** appended its
proposals onto whatever was already in the list.

One test now covers all of them, and each caller refuses in the way that suits
it — the typed dialog asks again, the scan keeps the proposals that fit and
reports how many it dropped, the hotkey says why.

**Closing over a later cut leaves the bookmark open** rather than throwing it
away. A closing timestamp *before* its own opening discards the pair, because
the pair is what was wrong. Here the opening time is still perfectly good and
only this closing time will not do, so the bookmark stays open to be closed
somewhere else.

**Touching is not overlapping.** A cut may begin at the exact instant the one
before it ended. A bookmark still waiting for its closing timestamp occupies
only the instant it was opened at, having no span yet to defend.

## The frame arrows

**They moved the wrong cut.** The four arrows appear on the row under the
pointer and acted on the selected row. Hovering any other row gave you four
arrows that quietly moved a different cut, and with nothing selected, four that
did nothing at all — which is how an open bookmark, which is never the selected
row, looked like it had no working arrows. The row now comes from the arrow
itself.

**Each arrow knows how far it can go, and hides when it cannot.** An arrow with
nowhere left to go reads as a broken button, so it is taken off the row
instead.

A start is held a second clear of the cut above it and a second short of its own
close. A close is fenced by beginnings only — its own below it, the next cut's
above it — and never by another end, because two ends are never adjacent and
stopping short of a further-away one would leave the cut in between free to be
overlapped anyway. With no cut after it, a close stops a second short of the
end of the video. An open bookmark has no closing time to stay behind, so its
start may travel the whole file.

Neighbours are taken in time order rather than row order. A bookmark set from
the player is added to the end of the list and only sorted when the file is
written, so the row above was not reliably the cut before.

## One Select button

**Select All** and **None** sat side by side, and one of them was always the
wrong one to want. There is a single button now, offering whichever action the
list has left available.

Its caption moves only at the two ends — everything checked, or nothing checked
— and holds through everything in between. Flipping it the moment a selection
stopped being complete would mean unticking one row of twenty takes "clear them
all" off the toolbar, which is the moment it is wanted. Pressing it lands the
selection on one end or the other, so the caption follows from the count either
way and reads the same whether the button was pressed or the boxes were ticked
by hand.

The **Bookmarks** menu keeps its separate **Select all** and **Select none**
entries. Those name one action each and do not need the state.

## Alt on its own

5.0 removed the menu access keys, which left Alt with nothing to open. It still
put the menu bar into menu mode: focus moved to the first header, and the next
keystroke went there rather than to the window.

The timestamp hotkey is global and can be bound to anything, Alt included, so a
binding that quietly lit up the menu bar instead of recording a bookmark was
left there to be discovered. A bare Alt is now swallowed, and only a bare one —
every combination worth keeping names its other key, so Alt+F4 and Alt+Space
are untouched.

## Smaller things

**Ticked cuts did not survive leaving the window.** The check for an edit made
outside the program compares the bookmark file's timestamp against one that
only a reload ever set — so loading a video left it unset, the first return to
the window re-read a file nobody had touched, and a reload builds every
bookmark fresh. The tick lives on the bookmark rather than in the file, so four
ticked cuts came back as four unticked ones. Both the load and the save record
the stamp now.

**The window can no longer be shrunk past its own contents.** The minimums are
measured from the toolbar's buttons and the side panel's children rather than
written down, so the action row cannot be squeezed onto a second line and the
panel cannot be shortened past the MPC-HC indicator at the bottom of it.

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
| `MPC-HC.Video.Editor.zip` | `3EDF4533A14B9D3789B19E195A7B5F943F2152A753CDDD985844B4A519250946` |
| `MPC-HC Video Editor.exe` | `672E637B14A9A39275D2CBF91DC826FFF0FC8DF4F35BA3FB22C13EBCDCE32CDA` |
| `ffmpeg.exe` | `4A01142006A4E2359293E072957DCDA7760C2003BBEEDE037B4551F2CFC8406F` |
| `ffprobe.exe` | `8B5298DA673B85E628FBC98535A88848E939E16DF72E856FC727E01AA667E243` |
| `LICENSE` | `3972DC9744F6499F0F9B2DBF76696F2AE7AD8AF9B23DDE66D6AF86C9DFB36986` |

FFmpeg is unchanged — still 9.0.1, GPL v3, from
[BtbN/FFmpeg-Builds](https://github.com/BtbN/FFmpeg-Builds).
