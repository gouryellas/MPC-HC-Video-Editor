# MPC-HC Video Editor 3.0.3

A small release about telling the truth: one button that described an operation
the user was not performing, and one panel that promised something the user
could not change.

## Changed

**A single cut is an extract, not a merge.** Joining implies more than one thing
to join. With one cut there is nothing to join — the result is that span written
out on its own — so calling it a merge described an operation nobody was
performing. The label now follows the number of cuts the command would actually
act on:

| Cuts | Label |
| --- | --- |
| None loaded, or two or more with none checked | Merge |
| Two or more checked | Merge selected |
| One, unchecked | Extract |
| One, checked | Extract selected |

The toolbar button and the Actions menu item read from the same value, so they
cannot disagree, and it updates as cuts are checked. Progress and failure
messages use the same wording, so the button pressed and the status reported
agree. The colour does not change with the label: it is the same command doing
the same kind of work, and a button that changed colour as cuts were ticked
would read as a different one.

**Split is deliberately untouched.** With a single pair it does much the same
thing as an extract. Both work, and either is a reasonable thing to click.

## New

**Delete files to the Recycle Bin** (Settings ▸ Cleanup) is now a setting rather
than a fixed behaviour. It is on by default, so nothing changes unless you turn
it off, and it covers every deletion the app makes — including the
post-operation cleanup that runs without asking, which is what made those
settings safe to turn on in the first place.

Turned off, deletions are permanent and unrecoverable. The warning saying so
appears only while the setting is off, because a standing warning next to a
setting that is behaving itself is noise, and noise is what stops it being read
in the state that matters.

On a volume with no Recycle Bin — a network share, most removable media — files
are removed outright either way, because leaving one behind would be worse than
the deletion that was asked for.

## Also

The README now opens with a screenshot of the main window, and shows the
overlay, the naming tags menu and Settings.

## Requirements

- Windows, .NET 8 desktop runtime
- MPC-HC with the web interface enabled (Options ▸ Player ▸ Web Interface)
- ffmpeg and ffprobe, beside the executable or on PATH. WEBM output needs a
  build with libvpx and libopus.
