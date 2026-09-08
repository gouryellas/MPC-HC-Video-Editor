# MPC-HC Video Editor 3.0.2

A maintenance release about things firing when they should not. The two global
hotkeys and the minimal overlay all acted on state that was true somewhere in
the program rather than in front of the user, and the fixes tie each one to
what is actually on screen.

## Fixes

**The window said 3.0 in the 3.0.1 release.** The caption was a literal in the
XAML, so bumping the version in the project file had no reason to make anyone
think of it. Both the title bar and Help ▸ About now read the version out of the
assembly, which the project file is the only source of.

**The overlay showed nine bookmarks and scrolled the rest.** Its height was
fixed at 300 px — deliberately, because a layered window that resizes does not
reliably repaint what it vacated, which is what caused the 3.0.1 flicker. The
window is now a fixed pane the height of the screen and the card inside it grows
with the list, so the layer is still never resized. Roughly forty pairs are
visible on a 1080p display before anything scrolls.

**The timestamp hotkey fired everywhere.** The hook is global, which it has to
be to work while the player has focus, but nothing checked where the press came
from — with the default middle-mouse binding, opening a link in a new tab set a
bookmark against whatever MPC-HC last had loaded. It now fires only when MPC-HC
is the active window and has a video open, and the press is passed along
untouched everywhere else.

**The X restore key fired on any "x" typed anywhere.** It was armed for as long
as the overlay existed. It is now armed only while the overlay is actually
visible, so it belongs to something the user can see.

**"Switch views automatically" turned itself off.** The automatic switch shared
a code path with the menu item, and that path ends manual control by clearing
the setting — so the first time the setting did its job, it disabled and saved
itself. The Settings checkbox came back unchecked and the next launch started
with switching off. The two paths are now separate, and nothing writes the
setting except the user.

**View ▸ Minimal gave way on the next click.** A hand-picked overlay followed
focus like an automatic one, so clicking anything else took it back down. It is
now pinned until X or View ▸ Full, and picking it makes MPC-HC the active window
so the result does not depend on which window happened to be behind this one.

## New

- **One instance, or several** (File ▸ Settings ▸ General). One instance is the
  default: starting the program again brings the running copy to the front
  instead of opening a second one. Scoped to the install folder, so a portable
  copy elsewhere is unaffected either way.

## Changed

- **"Naming style" is now "naming tag"** throughout the Options menu. The line
  showing the active tag is no longer greyed out.
- **View ▸ Minimal is disabled when there are no bookmarks.** The overlay is the
  bookmark list; an empty one has nothing to show. It also comes down on its own
  if the list empties — opening another video in the player clears it — and
  returns with the next bookmark.
- **A pinned overlay hides while another application is in front** and comes
  back for this window or MPC-HC. It is not closed and keeps its place.

## Requirements

- Windows, .NET 8 desktop runtime
- MPC-HC with the web interface enabled (Options ▸ Player ▸ Web Interface)
- ffmpeg and ffprobe, beside the executable or on PATH. WEBM output needs a
  build with libvpx and libopus.
