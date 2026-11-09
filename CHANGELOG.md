# Changelog

Every released version, newest first. Generated from the published
GitHub releases; Help ▸ About shows this same file.

## 5.8.2 — 2026-11-08

### Save frame thumbnail

- Fixed. Saved frames were ahead of the frames on screen.
- Merge and split were affected, but only for mp4s that had an edit list in the metadata, which was rare.

### Merging files

- The base merge operation (the one where you choose multiple video files in explorer to join) will now show a list of what you selected in the bookmarks area, with a step tracker.
- The step tracker will work with any view.

### Playlists

- When loaded, they will only be loaded to the editor as "active". The loaded playlist determines where the video for "Add current video to playlist" and "+ to playlist" goes.
- New "Add files..." for each playlist. Choose files in explorer you want added.
- New "Open all" for each playlist. Loads the entire playlist, notwithstanding any missing entries, into the MPC-HC playlist queue and plays the first video.
- Fixed "Load this playlist" to no longer open a video in MPC-HC. All it does is load the playlist in the editor.
- Dropping a playlist into the editor loads that playlist, but will also ask if you want to add all the videos in it to MPC-HC.
- You can drop .m3u and .m3u8 file types now.

### Settings

- Fixed issue with settings that sometimes failed to save. There was no warning of this.
- Reworked how the program restores user settings. Settings get written to a temporary file first. This is done so that a crash mid-write cannot leave the file truncated and prevent bad files from attempted restore actions.
- If, for some reason, there is an issue saving, there will be an error shown. In the past it just went on in silence.

### Diagnostics

- New "Help > Diagnostics". Shows data that can be used for troubleshooting errors.
- Crashes are now logged.

### Interface

- The seekbar can be hidden from the right click menu over it now.

## 5.8.1 — 2026-11-04

### Processing order

- New action under bookmarks. This will allow you to reorder your clips by just typing the order you want the operation to run them in.
- All operations will follow it except for exporting chapters.
- The order will show above the bookmarks list and can be removed from there.
- Nothing is written to the bookmark file. This is a temporary action and the order will be cleared when the video file is unloaded.

### Merging

- The progress bar takes the concatenating step into account. It will no longer reach 100% before it is actually finished.
- Fixed the remaining time left...timer.

### Tooltips

- I removed most of the tooltips. If an action needs an explanation, there is a ? beside it that you can hover over.

### Icons

- Every menu glyph has a real icon

### Hotkey

- Added an option that will block the program's hotkey in mpc-hc. This is to prevent two actions from occurring at once.

### Playlists

- 'Remove missing entries' shows how many are missing.

### Views

- The step tracker works in any view.

### Seekbar

- You can change the playhead color.

### Appearance

- Scrollbars will follow the theme. It was hard to see before.

### Settings

- Removed a lot of unnecessary explainers.

## 5.8 — 2026-10-29

### Notifications

- Toast notifications have not worked at all, until now. Fixed.
- Operations will indicate their progress, success, or failure with more detail.

### Enter time

- Added a list of examples that show all the different ways you can manually add a time or range of times.

### Tags

- Moved to its own menu location.
- Any reference to suffix is now stated as tag.

### Menus

- Greatly improved the organization and cleanliness of the menu items.
- Added headers that follow color themes.
- The edit menu will have a history of actions performed that you can look through (up to 20 each way) and click to restore the state of the editor to that moment.

### History

- Removing a video via the "Remove" menu item no longer opens that video file.

### Toolbar

- Can be hidden now.

### Bookmarks

- Replaced the radio button for the quick save action with a dynamic small save icon.
- Added a step tracker that will show your operation progress. It will temporarily show a yellow arrow on the bookmark list. Right now this only works if you are using the list view.

### Progress

- The status bar flashes green on success and red on failure for finished operations.
- Elapsed time was broken and now works accurately.

### Playlists

- The "Find moved files" operation allows you to select a drive to search through. If that fails, there are other search options available that take longer and demand more resources.

## 5.7 — 2026-10-19

### Bookmark storage change

- They are stored in one location now: a bookmarks folder inside the program folder. A copy is kept in %appdata%, so replacing the program folder for an upgrade will have these restored. Existing bookmark files need to be moved into the program bookmarks folder if you want them to be picked up by this change. The %appdata% bookmarks is a mirror of the program bookmarks.

### New toolbar icons

- Icons are actual icons instead of generated programmatically.

### Toolbar button customization

- You can remove the background box of the toolbar buttons

### Play/Pause button

- Added a dynamic play/pause button

### Window status

- Minimize is respected by the editor now

### Failed jobs

- A job that fails now says so and sounds like it, instead of finishing on a success chime behind the error.

### Requirements

- Windows, .NET 8 desktop runtime
- MPC-HC with the web interface enabled (Options ▸ Player ▸ Web Interface) — the program detects the port itself and says plainly if the interface is off
- ffmpeg and ffprobe are bundled. Your own build dropped beside the executable still takes precedence.

### Verify your download

```powershell
Get-FileHash -Algorithm SHA256 .\MPC-HC.Video.Editor.zip
```

- | File | SHA-256 | | ---- | ------- | | `MPC-HC.Video.Editor.zip` | `B39C33439B17472C451C3104F4907F73DD9C795937C9BDA40E3667993ECBFAC7` | | `MPC-HC Video Editor.exe` | `314F78984EC3FF43606C7AB3F6F8BB4445114E4EAE35DF979B96260678B2BD00` | | `ffmpeg.exe` | `4A01142006A4E2359293E072957DCDA7760C2003BBEEDE037B4551F2CFC8406F` | | `ffprobe.exe` | `8B5298DA673B85E628FBC98535A88848E939E16DF72E856FC727E01AA667E243` |

## 5.6 — 2026-10-18

### New bookmark views

- There are 4 new thumbnail views in addition to the original list view. To change the speed in the new views, just right click on a thumbnail.

### Resize cuts on the timeline

- You can drag the cut's brackets on the seekbar to change it.

### Jobs done

- Merge shows some additional information about the job upon completion.

### Seekbar preview

- Shows a frame on mouse-hover over the seekbar.

### New logo

- The previous one was too generic.

### Hotkey works from editor

- You can use the seekbar in the editor to create bookmarks without having to do it from MPC-HC.

### Customization

- The seekbar and the toolbar can be customized by right clicking on them. Colors can be changed.

### Seekbar

- Can be hidden completely in the settings.

### System theme

- You can choose to have the editor follow the system theme colors.

### Requirements

- Windows, .NET 8 desktop runtime
- MPC-HC with the web interface enabled (Options ▸ Player ▸ Web Interface) — the program detects the port itself and says plainly if the interface is off
- ffmpeg and ffprobe are bundled. Your own build dropped beside the executable still takes precedence.

### Verify your download

```powershell
Get-FileHash -Algorithm SHA256 .\MPC-HC.Video.Editor.zip
```

- | File | SHA-256 | | ---- | ------- | | `MPC-HC.Video.Editor.zip` | `F2A7D4816F4919073B15F925BB918255C7856C5543FB1AA3BDFD5A0A4B36DCD9` | | `MPC-HC Video Editor.exe` | `A6CEBEFBFB1F0FB51748DC2F9FA8BFF44DC67E1C78B5F3E84AB70343CE7C344E` | | `ffmpeg.exe` | `4A01142006A4E2359293E072957DCDA7760C2003BBEEDE037B4551F2CFC8406F` | | `ffprobe.exe` | `8B5298DA673B85E628FBC98535A88848E939E16DF72E856FC727E01AA667E243` |

## 5.5.1 — 2026-10-14

- Addressed and corrected an audio desync issue, plus small fixes.

### Addressed and corrected audio desync issue

- Joining several cuts together could leave the audio drifting further out of sync with the video the more clips were merged — a small per-segment rounding gap that used to compound at every join. Audio is now rebuilt once across the whole merged file instead of copied piece by piece, so the drift has nowhere to accumulate. Cutting exactly on the marked frame is also on by default now, instead of a setting you had to turn on yourself.

### Small fixes

- A clip whose finished length doesn't match what was marked now shows up as a notice you have to read, not status-bar text already scrolled past.
- The button colors dialog dropped its explanatory heading.
- Toolbar button tooltips are shorter.

### Requirements

- Windows, .NET 8 desktop runtime
- MPC-HC with the web interface enabled (Options ▸ Player ▸ Web Interface) — the program detects the port itself and says plainly if the interface is off
- ffmpeg and ffprobe are bundled. Your own build dropped beside the executable still takes precedence.

### Verify your download

```powershell
Get-FileHash -Algorithm SHA256 .\MPC-HC.Video.Editor.zip
```

- | File | SHA-256 | | ---- | ------- | | `MPC-HC.Video.Editor.zip` | `F7FC7FFBE22F95B107E0171E5F7BCC7D7DD392A2EC0D827CD5360DDF6B64E6A3` | | `MPC-HC Video Editor.exe` | `C731DCC7E0A0A2822C9F1CD255849150E23EC3B61BAE403921EC4716021734BB` | | `ffmpeg.exe` | `4A01142006A4E2359293E072957DCDA7760C2003BBEEDE037B4551F2CFC8406F` | | `ffprobe.exe` | `8B5298DA673B85E628FBC98535A88848E939E16DF72E856FC727E01AA667E243` | | `LICENSE` | `3972DC9744F6499F0F9B2DBF76696F2AE7AD8AF9B23DDE66D6AF86C9DFB36986` |

- FFmpeg is unchanged — still 9.0.1, GPL v3, from [BtbN/FFmpeg-Builds](https://github.com/BtbN/FFmpeg-Builds).

## 5.5 — 2026-10-13

- What a picture is, a crop for video, colour on the toolbar, icons in the menu, a round of fixes, and a new logo.

### Image info shows metadata

- Actions ▸ Image info, or drop a picture onto the window. Reads the file's own details: size, colour depth, camera settings, any title or copyright, and where it was taken — pulled from the same decoders the rest of the program already uses, so nothing extra to install.

- Pictures from ComfyUI and Automatic1111 are read further: the checkpoint, LoRAs, sampler settings and seed, and the positive and negative prompts, shown first and in full rather than behind a "click to show". A workflow that builds its prompt as it runs rather than writing it down is worked out from the graph itself.

- A picture dropped on the window opens this now, instead of being handed to MPC-HC as if it were a video.

### Crop video

- Actions ▸ Crop, or a Crop button from Customize toolbar. Draw a rectangle on a still from the cut — drag to move, grab a corner to resize, or hold 16:9, 9:16, 1:1 or 4:3. Works on the chosen cuts, or on the whole video with none chosen.

### Toolbar button colour options

- Toolbar ▸ right-click ▸ Change button colors, also under Options. Four families — video, audio, images, everything else — each with one colour, and a redesigned dialog: a card and a live preview for each family instead of an unlabelled swatch.

### Menu item icons

- The Actions menu is grouped by family now — video, audio, images, then everything else — with a small icon beside every entry. Flip, Rotate and Fade are named the same as their toolbar buttons. An empty Current video / bookmarks / playlist heading in the side panel now offers its own "open" icon.

### Various fixes

- Converting or resizing images now asks for the picture before asking what to do with it, and cancelling out of either says so instead of leaving the status bar reporting an old result.
- Undoing past a timing change could silently clear every fade in the list; fades are now kept in the undo history properly.
- The main window remembers its size and position, and no longer opens taller than the screen.
- Button colors and Resize images could run off the bottom of a small or scaled-up screen; both now scroll.
- The overlay could appear on the wrong monitor on a two-screen setup; it now follows the video.
- Hint text throughout the program was a point too small to read comfortably.
- The Playlist menu could disagree with itself about whether a folder was still there; a missing one now says so.
- Two labels in Settings stayed black regardless of theme.
- The side panel said "(not loaded)" in one place and "<none>" everywhere else for the same thing.

### Updated program logo

- A film reel with a play triangle and a cyan sweep, carrying its own fixed colours rather than following the theme. Shown in the taskbar, and now in the About window too.

### Requirements

- Windows, .NET 8 desktop runtime
- MPC-HC with the web interface enabled (Options ▸ Player ▸ Web Interface) — the program detects the port itself and says plainly if the interface is off
- ffmpeg and ffprobe are bundled. Your own build dropped beside the executable still takes precedence.

### Verify your download

```powershell
Get-FileHash -Algorithm SHA256 .\MPC-HC.Video.Editor.zip
```

- | File | SHA-256 | | ---- | ------- | | `MPC-HC.Video.Editor.zip` | `C61A5D2749A2E5441E397A5CA8CFF65F768491811E1336718CD7B936C804D8A0` | | `MPC-HC Video Editor.exe` | `384808995E942CFE214B4C7F59177738195D0971C6500A848D24A465F4EC5DDD` | | `ffmpeg.exe` | `4A01142006A4E2359293E072957DCDA7760C2003BBEEDE037B4551F2CFC8406F` | | `ffprobe.exe` | `8B5298DA673B85E628FBC98535A88848E939E16DF72E856FC727E01AA667E243` | | `LICENSE` | `3972DC9744F6499F0F9B2DBF76696F2AE7AD8AF9B23DDE66D6AF86C9DFB36986` |

## 5.4 — 2026-10-06

- Three things for pictures: thumbnails from a video, resizing, and a say in where any of it is written.

### Save thumbnails

- Actions ▸ Save thumbnails. Takes a frame every so many seconds through the whole video — an image per frame, or every frame tiled into a single image.

- You choose the interval, the width of each thumbnail, and how many across a tiled image is. A video with more frames than fit starts another one.

- It reads the file on disk, so MPC-HC does not have to be running. With no video loaded it asks for one.

### Resize images

- Actions ▸ Resize images. One image or a folderful, at a standard size: VGA through 4K, the two common square sizes, or type your own.

- A size alone does not say what to do with a picture that is not that shape, so it asks. Fit inside keeps the shape; Fill and crop gives the exact size and trims the overflow; Stretch gives the exact size by squashing. Each file keeps its own format.

### Choose where images are written

- Convert images and Resize images both ask: beside each picture, in the Save to folder, or somewhere else. Beside each picture means exactly that — a selection spanning three folders comes back to those three folders.

### Convert images asks which format

- The toolbar button for it used to do nothing, having no format to work with. It asks now. The menu entries still name a format each.

### Small button icons

- Settings ▸ Appearance. Draws the toolbar as icons alone, the way the camera and bin buttons already were. Hover any of them for its name.

### The active naming tag is lit

- Whichever tag is in use shows it on the toolbar, including "no tag".

### Flip, Rotate, Mute and Fade in the menu

- They are in the Actions menu as well as on the toolbar, and are available whenever there is a complete cut — not only once one is checked.

### Fixes

- Detect cuts and Chapters stayed greyed out for the whole session, whatever you opened.
- Button tooltips wait three seconds, as the menus do.
- Buttons no longer change colour under the pointer, which was wiping out the colours on Merge, Split, Convert and Strip audio.

### Requirements

- Windows, .NET 8 desktop runtime
- MPC-HC with the web interface enabled (Options ▸ Player ▸ Web Interface) — the program detects the port itself and says plainly if the interface is off
- ffmpeg and ffprobe are bundled. Your own build dropped beside the executable still takes precedence.

### Verify your download

```powershell
Get-FileHash -Algorithm SHA256 .\MPC-HC.Video.Editor.zip
```

- | File | SHA-256 | | ---- | ------- | | `MPC-HC.Video.Editor.zip` | `1C54CA37FA362E3533A2638C45F5854FB3C04448FD619483EF926891DDD09E26` | | `MPC-HC Video Editor.exe` | `9AA991579E21A01B48BB4FD49779A4A6B4499B67A2617CE331E2916C8C3AFF18` | | `ffmpeg.exe` | `4A01142006A4E2359293E072957DCDA7760C2003BBEEDE037B4551F2CFC8406F` | | `ffprobe.exe` | `8B5298DA673B85E628FBC98535A88848E939E16DF72E856FC727E01AA667E243` | | `LICENSE` | `3972DC9744F6499F0F9B2DBF76696F2AE7AD8AF9B23DDE66D6AF86C9DFB36986` |

- FFmpeg is unchanged — still 9.0.1, GPL v3, from [BtbN/FFmpeg-Builds](https://github.com/BtbN/FFmpeg-Builds).

## 5.3 — 2026-10-01

- The toolbar is yours now: pick the buttons, put them in any order, and drop the labels if you want it smaller.

### Customize the toolbar

- Right-click the toolbar, or Options ▸ Customize toolbar. Drag any button from the list onto your row, drag one off to remove it, and drag the ones you have into the order you want.

- There are three blocks to arrange them with: a fixed gap, a stretching gap that pushes everything after it to the right, and a new row. Use as many of each as you like.

- Twenty-seven buttons are available, including several that were only ever in the menus — Open video, Settings, Undo, Enter time, Detect cuts, GIF / WebP, Chapters and more.

- The preview box is the width the window can shrink to, so a row that fits there fits at any size.

### Small button icons

- Settings ▸ Appearance. Draws every toolbar button as an icon alone, the way the camera and bin buttons already were. Hover any of them for its name. A row that needed two lines usually fits one without the labels.

### Drag the buttons on the toolbar itself

- For a quick change you do not need the dialog — drag a button along the row and drop it where you want it.

### A typed time closes the open bookmark

- With a single timestamp waiting for its end, Enter time added a second standalone row instead of finishing the first. It completes the range now, the same as marking the end with the hotkey.

### Leaving MPC-HC no longer steals focus

- Switching from MPC-HC to anything else brought this window to the front. It now comes forward only if it was the window behind the player.

### Menu tooltips wait three seconds

- They appeared almost instantly and got in the way of reading the menu.

### Requirements

- Windows, .NET 8 desktop runtime
- MPC-HC with the web interface enabled (Options ▸ Player ▸ Web Interface) — the program detects the port itself and says plainly if the interface is off
- ffmpeg and ffprobe are bundled. Your own build dropped beside the executable still takes precedence.

### Verify your download

```powershell
Get-FileHash -Algorithm SHA256 .\MPC-HC.Video.Editor.zip
```

- | File | SHA-256 | | ---- | ------- | | `MPC-HC.Video.Editor.zip` | `622D517AE9036DC64279F66EFCC841C9AA3D5106F092712B9294226D758C2B59` | | `MPC-HC Video Editor.exe` | `958C62DBA8EEE083CA60929DCB9BD21F8B1464497C79E12953AC0037FDE58921` | | `ffmpeg.exe` | `4A01142006A4E2359293E072957DCDA7760C2003BBEEDE037B4551F2CFC8406F` | | `ffprobe.exe` | `8B5298DA673B85E628FBC98535A88848E939E16DF72E856FC727E01AA667E243` | | `LICENSE` | `3972DC9744F6499F0F9B2DBF76696F2AE7AD8AF9B23DDE66D6AF86C9DFB36986` |

- FFmpeg is unchanged — still 9.0.1, GPL v3, from [BtbN/FFmpeg-Builds](https://github.com/BtbN/FFmpeg-Builds).

## 5.2.2 — 2026-09-28

- Two playback fixes, and the clip list is quicker to work with.

### Playing a cut actually plays it

- Play all and Play selected walked through the cuts but left each one paused on its first frame. The program was sending MPC-HC the play/pause toggle instead of play, twice — so it started the cut and immediately stopped it.

- Finishing a run also stopped the player and jumped back to 0:00. It pauses where it is now.

### The video length matches the player

- The timeline took its length from the file's own metadata, which can be wrong: one video reported 3:15 where MPC-HC plays 3:26. Everything you mark comes off the player's clock, so the timeline follows it now. A short timeline made the end of the video impossible to mark and put every position on the bar slightly out.

### The preview frames show what the clip will look like

- Rotate turns them, Flip mirrors them, and a fade is drawn as the frame coming out of black at the start and going into it at the end. Muted clips get a crossed speaker in the corner, and a clip at anything other than normal speed gets a small `2x` or `1.25x` badge.

- Nothing is re-rendered for this, so it keeps up with the buttons.

### The duration on a row follows the speed slider

- The figure in parentheses was the marked range, which does not change when you change the speed. It is now how long the saved clip will run: a 20-second cut at 2x reads 10s.

### Click a row to flip, rotate, mute, fade or delete it

- These needed the row's checkbox ticked first. Clicking the row is enough now.

- Checks still win when you have some: with three cuts checked, these act on those three, whichever row you last clicked.

### Requirements

- Windows, .NET 8 desktop runtime
- MPC-HC with the web interface enabled (Options ▸ Player ▸ Web Interface) — the program detects the port itself and says plainly if the interface is off
- ffmpeg and ffprobe are bundled. Your own build dropped beside the executable still takes precedence.

### Verify your download

```powershell
Get-FileHash -Algorithm SHA256 .\MPC-HC.Video.Editor.zip
```

- | File | SHA-256 | | ---- | ------- | | `MPC-HC.Video.Editor.zip` | `7D0DB08C04C46D5127682C6669EAF05D6BA86F0AA2B5F047FA505AE1265346D9` | | `MPC-HC Video Editor.exe` | `B99797348471B5A53A44BF5E4DB6C773801B752C5FABE0520F5446107ACF1D24` | | `ffmpeg.exe` | `4A01142006A4E2359293E072957DCDA7760C2003BBEEDE037B4551F2CFC8406F` | | `ffprobe.exe` | `8B5298DA673B85E628FBC98535A88848E939E16DF72E856FC727E01AA667E243` | | `LICENSE` | `3972DC9744F6499F0F9B2DBF76696F2AE7AD8AF9B23DDE66D6AF86C9DFB36986` |

- FFmpeg is unchanged — still 9.0.1, GPL v3, from [BtbN/FFmpeg-Builds](https://github.com/BtbN/FFmpeg-Builds).

## 5.2.1 — 2026-09-24

- Marking fixes. A timestamp that cannot be used is refused rather than added, the refusal is said over the player instead of into a status bar you cannot see, and playback no longer waits for a second cut.

### Timestamps can only be set after your last mark

- A new cut has to open at or after the latest time already marked. Anything behind that is refused and nothing is added.

- The old rule only refused a mark that landed *inside* an existing cut, which left every gap behind the work already done open to a new one: with a cut at 3s–4s, marking at 2s was accepted. That is backwards in both senses, and the row it produced had to be deleted by hand.

- **A mark on a cut's first second is refused too.** It was accepted before, and not for a reason anyone would guess: the check asked whether a span of no length overlapped anything, and that question is false at exactly a cut's opening instant. Anywhere else inside the cut was caught. Only the first second was not, which is precisely the second you land on after nudging a cut's end back to where you were watching.

- Landing exactly on the last mark is still allowed — that is how one cut starts where the last one ended.

- A scan is the exception, since it proposes the whole file at once. To re-cut an earlier stretch, delete the cuts after it or scan with **replace**.

### A refused timestamp now says why, over the video

- Every refused press now floats a notice above whatever is on screen, for four and a half seconds, naming the time it refused and the time it wanted:

- > **Timestamp not added** — 0:02 is behind the last mark at 0:04. Marks go > forwards.

- This used to be gated the same way confirmations are, which holds them back while the compact overlay is up — that is, while a video is playing, which is the only time the hotkey is pressed. With toasts switched off it said nothing at all, and the status bar it fell back to is behind the player.

- The gate's reasoning does not hold for a refusal anyway. It stands down when the overlay is up because the overlay already shows what happened: true of a timestamp that was set, since a row appears, and false of one that was refused, because the list is exactly as it was.

- It is held longer than a confirmation, too. "Timestamp 3 set" only has to be noticed; a sentence with two times in it has to be read.

- Confirmations are unchanged and still respect the setting.

### Play works with a single cut marked

- **Play all cuts** and **Play selected cuts** wanted two complete pairs before they would turn on. One cut is when playback is most useful — seek to its start, stop at its end, and see whether it is the piece you meant, which is what you want immediately after marking your first one. Merge and Split have always worked on a single pair.

### The WebP export says which viewers can open it

- The export is sound — a file a desktop image viewer calls unreadable decodes frame for frame in any browser — but plenty of viewers handle only still WebP and report an animated one as broken rather than as unsupported. GIF working in the same viewer makes that look like our fault, so the option now says which tools cannot play one.

### Requirements

- Windows, .NET 8 desktop runtime
- MPC-HC with the web interface enabled (Options ▸ Player ▸ Web Interface) — the program detects the port itself and says plainly if the interface is off
- ffmpeg and ffprobe are bundled. Your own build dropped beside the executable still takes precedence.

### Verify your download

```powershell
Get-FileHash -Algorithm SHA256 .\MPC-HC.Video.Editor.zip
```

- | File | SHA-256 | | ---- | ------- | | `MPC-HC.Video.Editor.zip` | `C2430F2266895E48C4820D93B9C1778458FF38306863A760070024E5CC325952` | | `MPC-HC Video Editor.exe` | `FEE63799D46BD5F6CA4E37A7B90438D9FC283E4568449D8C48624FC6769BEE47` | | `ffmpeg.exe` | `4A01142006A4E2359293E072957DCDA7760C2003BBEEDE037B4551F2CFC8406F` | | `ffprobe.exe` | `8B5298DA673B85E628FBC98535A88848E939E16DF72E856FC727E01AA667E243` | | `LICENSE` | `3972DC9744F6499F0F9B2DBF76696F2AE7AD8AF9B23DDE66D6AF86C9DFB36986` |

- FFmpeg is unchanged — still 9.0.1, GPL v3, from [BtbN/FFmpeg-Builds](https://github.com/BtbN/FFmpeg-Builds).

## 5.2 — 2026-09-24

- Five things a cut can now do that it could not before: fade in and out, be exported as a GIF, be normalized for loudness, give up a single frame as a picture, and be seen as a waveform. Two new themes. Strip audio asks what it should produce, and a long job tells you when it is done.

### New to the toolbar

- **Fade.** Select a cut, press ◐ Fade, and it comes up from black at the start and goes down at the end. The length is one setting for all of them, under Settings ▸ Cuts, because a fade that varies clip by clip is a thing to tune rather than a thing to use. Audio fades with the picture. A faded cut is re-encoded — there is no way to dissolve a frame without touching it — and says so on its row.

- **Save the frame on screen.** The picture the player is showing, written as a PNG at the video's own resolution. It reads the position from MPC-HC and pulls that frame out of the file, so the image is the frame itself rather than a screenshot of a window with a video in it.

- **Export a cut as a GIF or WebP.** Any cut, at a size and frame rate you pick. GIF builds its own palette from the cut rather than taking the stock 216 colours, which is the difference between a gradient and a mess of bands. WebP is smaller and keeps more colour, when whatever you are posting it to accepts one.

- **Normalize audio** now applies to Convert as well as to cuts, so a converted file lands at the same loudness as a clip cut from it.

### The waveform is back

- 5.1 removed it because nothing drew it. It draws now: a dim outline of the audio behind the timeline, with the cuts and the playback position on top of it, so a silent stretch is visible before you go looking for it.

- It is generated once per video, in the background, and the timeline works normally while it is being made.

### Strip audio asks what it is for

- "Strip audio" reads three ways and the program only did one of them. It now asks, once per run:

- the audio on its own, as an MP3
- the video on its own, with its sound removed
- both

- It also says on the dialog that the file you picked is not changed — the one setting that does delete it is three tabs into Settings, and that is not something to leave to be discovered.

- **The silent copy is named `-silent`.** This is not decoration. MPC-HC, and most players, attach an audio file sitting next to a video whose name starts with the video's name. Writing `clip[tag].mp3` beside `clip[tag].mp4` handed the player a matched pair, and the silent copy played with the very sound that had been taken out of it — a genuinely silent file that nobody would believe was silent. Lengthening the video's name is what breaks the match.

### A sound when a job finishes

- Settings ▸ Notifications ▸ **Play a sound when an operation finishes**, on by default. It is for the encode you walked away from, which is a thing you only benefit from if it is already on when you leave.

- It hangs off the one place every successful operation ends, so a cancelled or failed job stays silent. Windows' own tada, with four stock sounds behind it if that file is missing. Nothing in the chain is an error sound.

### Two more themes

- **Obsidian**, near-black with a blue accent, for a dark room. **Parchment**, warm paper and teal, for a bright one. That makes five.

- **The operation buttons keep their colours in every theme.** Merge, Split, Convert and Strip audio are the four things that write files, and they are now the same four colours whichever theme is on — you learn where they are once. Their labels are white in all five, which the light themes needed and the dark ones do not mind.

### Cuts, and the rules about them

- **Overlapping is refused; touching is fine.** A cut ending at 10s and the next starting at 10s is a pair, not a conflict. Two cuts running through each other produce two clips of the same footage and a merge that plays it twice. Every way of making a cut now applies the same test.

- **Closing a bookmark before its own opening no longer throws the pair away.** It says why and leaves the bookmark open, because the opening time was never the thing that was wrong.

- **The arrows move a whole second.** They were moving by a frame, which at 24fps is four hundredths of a second — invisible on a display that shows whole seconds, and lost entirely when the bookmarks were saved. There are no fractions of a second anywhere now: every duration is rounded to the nearest one, so a cut that measures 16.67 seconds reads as 17 rather than 16.

- **The arrows can take a cut right up against its neighbour.** They used to stop a second short, which was the old no-touching rule outliving itself.

- **The cut marks draw over the playback position**, not under it. A bracket hidden behind the bar it belongs to is not a mark.

### Names and prompts

- **A naming tag is optional.** Options ▸ Naming tag ▸ **None** writes `clip.mp4` rather than `clip[done].mp4`. A second file of the same name still gets a number, since two files in a folder cannot share one.

- **Every output prompt names the folder it is saving into.** "Saves as" answers half the question, and the half it left out is the one that matters when output is going somewhere other than beside the video.

- **The name is asked once per file, not once per file written.** Strip audio with both outputs asked the same question twice, and dismissing the second one — which reads as a repeat — silently dropped that output while the summary claimed it had been written. Summaries now count what actually landed.

### The overlay

- Its rows read like the main window's: same colours, same spacing, same hover. The filename line is gone — the overlay is on screen because you are watching that video — and an unfinished bookmark reads `(incomplete)`.

- **Its timestamps seek by default.** The setting that makes them clickable arrived in 4.9 and did nothing until you found it. Turning it off still makes the overlay click-through, which is worth having; it is now the thing you turn on rather than the thing you have to.

### Update notices say what changed

- The check already received the release notes alongside the version number and threw them away. The notice now lists up to six lines from them, so "is this worth downloading" is answerable without opening a browser. It also explains that replacing the whole folder keeps your settings, which it does — every save is mirrored to %APPDATA% and restored into a folder that arrives without one.

### Smaller things

- PLAY SPEED is centred over its slider
- the image-deletion prompt asks the question instead of restating the operation
- "Add current video" on a playlist submenu is disabled until a video is loaded
- chapters are written in timeline order
- the release notes for every version live in `releases/` rather than filling the repository root

### Requirements

- Windows, .NET 8 desktop runtime
- MPC-HC with the web interface enabled (Options ▸ Player ▸ Web Interface) — the program detects the port itself and says plainly if the interface is off
- ffmpeg and ffprobe are bundled. Your own build dropped beside the executable still takes precedence.

### Verify your download

```powershell
Get-FileHash -Algorithm SHA256 .\MPC-HC.Video.Editor.zip
```

- | File | SHA-256 | | ---- | ------- | | `MPC-HC.Video.Editor.zip` | `D838D89179E9812602715C96412C34C4E0BD59D236EEFFBC84F38B8B290195A3` | | `MPC-HC Video Editor.exe` | `514DF1C7922A2E6E48F929C060E253F82725BEAC2DC80A87D969878E3637D7A4` | | `ffmpeg.exe` | `4A01142006A4E2359293E072957DCDA7760C2003BBEEDE037B4551F2CFC8406F` | | `ffprobe.exe` | `8B5298DA673B85E628FBC98535A88848E939E16DF72E856FC727E01AA667E243` | | `LICENSE` | `3972DC9744F6499F0F9B2DBF76696F2AE7AD8AF9B23DDE66D6AF86C9DFB36986` |

- FFmpeg is unchanged — still 9.0.1, GPL v3, from [BtbN/FFmpeg-Builds](https://github.com/BtbN/FFmpeg-Builds).

## 5.1 — 2026-09-21

- The timeline has been rebuilt again — one bar, with the clock and the cuts inside it. Cuts can no longer be made to overlap, from anywhere. The frame arrows act on the row they appear on, which they did not before.

### The timeline

- 5.0 gave the playback position and the bookmark ranges a lane each, stacked. That fixed them drawing over one another and left a two-row strip carrying two pieces of information, with the clock for a third sitting in the corner of the status bar, a long way from the bar it described.

- It is one rectangle now, with everything inside it. The position on the left, the length on the right, and between them a dark track carrying the playback position in blue with the cuts marked on the same line. The status bar keeps only what it is for — reporting what just happened — and the time is where you are already looking.

- **The bar seeks.** Click anywhere on the track and the player goes there, using the same command as clicking a timestamp in the list. The click is measured against the inside of the bar, not its border, which stands for no time at all.

- **A cut is two brackets and nothing between them.** A coloured band the length of the cut was a great deal of paint for two numbers. It buried whatever shared the bar with it, and on a busy edit the timeline became a row of blocks with the gaps — the parts being thrown away — as the only legible thing left. Facing feet read as "from here to there" on their own.

- Between a pair goes the cut's number, and a tick when it is checked. A cut too short for both keeps the tick and drops the number: the number says which row this is, the tick says the next action will act on it. They are all white, except the lone opening bracket of a bookmark still waiting to be closed, which is red.

- **The waveform is gone.** Nothing draws it any more, so generating it meant an ffmpeg per video load decoding an entire audio track for a bitmap nobody would see.

### Cuts can no longer overlap

- Two cuts that run through each other produce two clips containing the same footage, and a merge that plays it twice. Nothing in the program checked for it.

- The frame arrows were half a check and the only one. The hotkey would open a bookmark inside an existing cut, or close one straight over the top of the next one. A typed range was checked for running backwards but not for running through its neighbours. A scan accepted without **replace** appended its proposals onto whatever was already in the list.

- One test now covers all of them, and each caller refuses in the way that suits it — the typed dialog asks again, the scan keeps the proposals that fit and reports how many it dropped, the hotkey says why.

- **Closing over a later cut leaves the bookmark open** rather than throwing it away. A closing timestamp *before* its own opening discards the pair, because the pair is what was wrong. Here the opening time is still perfectly good and only this closing time will not do, so the bookmark stays open to be closed somewhere else.

- **Touching is not overlapping.** A cut may begin at the exact instant the one before it ended. A bookmark still waiting for its closing timestamp occupies only the instant it was opened at, having no span yet to defend.

### The frame arrows

- **They moved the wrong cut.** The four arrows appear on the row under the pointer and acted on the selected row. Hovering any other row gave you four arrows that quietly moved a different cut, and with nothing selected, four that did nothing at all — which is how an open bookmark, which is never the selected row, looked like it had no working arrows. The row now comes from the arrow itself.

- **Each arrow knows how far it can go, and hides when it cannot.** An arrow with nowhere left to go reads as a broken button, so it is taken off the row instead.

- A start is held a second clear of the cut above it and a second short of its own close. A close is fenced by beginnings only — its own below it, the next cut's above it — and never by another end, because two ends are never adjacent and stopping short of a further-away one would leave the cut in between free to be overlapped anyway. With no cut after it, a close stops a second short of the end of the video. An open bookmark has no closing time to stay behind, so its start may travel the whole file.

- Neighbours are taken in time order rather than row order. A bookmark set from the player is added to the end of the list and only sorted when the file is written, so the row above was not reliably the cut before.

### One Select button

- **Select All** and **None** sat side by side, and one of them was always the wrong one to want. There is a single button now, offering whichever action the list has left available.

- Its caption moves only at the two ends — everything checked, or nothing checked — and holds through everything in between. Flipping it the moment a selection stopped being complete would mean unticking one row of twenty takes "clear them all" off the toolbar, which is the moment it is wanted. Pressing it lands the selection on one end or the other, so the caption follows from the count either way and reads the same whether the button was pressed or the boxes were ticked by hand.

- The **Bookmarks** menu keeps its separate **Select all** and **Select none** entries. Those name one action each and do not need the state.

### Alt on its own

- 5.0 removed the menu access keys, which left Alt with nothing to open. It still put the menu bar into menu mode: focus moved to the first header, and the next keystroke went there rather than to the window.

- The timestamp hotkey is global and can be bound to anything, Alt included, so a binding that quietly lit up the menu bar instead of recording a bookmark was left there to be discovered. A bare Alt is now swallowed, and only a bare one — every combination worth keeping names its other key, so Alt+F4 and Alt+Space are untouched.

### Smaller things

- **Ticked cuts did not survive leaving the window.** The check for an edit made outside the program compares the bookmark file's timestamp against one that only a reload ever set — so loading a video left it unset, the first return to the window re-read a file nobody had touched, and a reload builds every bookmark fresh. The tick lives on the bookmark rather than in the file, so four ticked cuts came back as four unticked ones. Both the load and the save record the stamp now.

- **The window can no longer be shrunk past its own contents.** The minimums are measured from the toolbar's buttons and the side panel's children rather than written down, so the action row cannot be squeezed onto a second line and the panel cannot be shortened past the MPC-HC indicator at the bottom of it.

### Requirements

- Windows, .NET 8 desktop runtime
- MPC-HC with the web interface enabled (Options ▸ Player ▸ Web Interface) — the program detects the port itself and says plainly if the interface is off
- ffmpeg and ffprobe are bundled. Your own build dropped beside the executable still takes precedence.

### Verify your download

```powershell
Get-FileHash -Algorithm SHA256 .\MPC-HC.Video.Editor.zip
```

- | File | SHA-256 | | ---- | ------- | | `MPC-HC.Video.Editor.zip` | `3EDF4533A14B9D3789B19E195A7B5F943F2152A753CDDD985844B4A519250946` | | `MPC-HC Video Editor.exe` | `672E637B14A9A39275D2CBF91DC826FFF0FC8DF4F35BA3FB22C13EBCDCE32CDA` | | `ffmpeg.exe` | `4A01142006A4E2359293E072957DCDA7760C2003BBEEDE037B4551F2CFC8406F` | | `ffprobe.exe` | `8B5298DA673B85E628FBC98535A88848E939E16DF72E856FC727E01AA667E243` | | `LICENSE` | `3972DC9744F6499F0F9B2DBF76696F2AE7AD8AF9B23DDE66D6AF86C9DFB36986` |

- FFmpeg is unchanged — still 9.0.1, GPL v3, from [BtbN/FFmpeg-Builds](https://github.com/BtbN/FFmpeg-Builds).

## 5.0 — 2026-09-20

- Rotate and mute have not worked since they were added. They do now. The timeline has been rebuilt so the playback position is visible, the side panel says what the timestamp hotkey is, and the program no longer leaves ffmpeg processes running behind it.

### Rotate and Mute did nothing at all

- Both buttons have been greyed out since 4.6, in every version, no matter what was selected. Not sometimes — always. Neither feature has been reachable since the day it shipped.

- They share their enabling rule with **Flip**: all three are available when at least one complete cut is ticked. The rule was right, and Flip obeyed it. The other two were never told to re-check it. Nothing else offers rotation or mute, so the entire feature sat behind two buttons that could not be pressed, and the setting written into the bookmark file could only ever be `None`.

- Tick a cut and all three now light up together. A rotation still cycles a quarter turn at a time — right, upside down, left, back to normal — and applies to every ticked cut at once.

### The program left ffmpeg processes running

- Selecting a bookmark renders the first and last frame of it for the panel on the right. Moving to another row before those finish cancels them, which is normal and happens constantly — it is one click to the next row.

- A cancelled render abandoned its ffmpeg. It was never ended, only let go of: it stayed in memory, blocked on a pipe nothing was reading any more, holding the source video open, until the program was closed. One was found alive after three minutes having used a tenth of a second of processor time. Clicking down a long list left one behind per row.

- A file held open cannot be deleted or renamed, which matters here more than it might elsewhere: **Settings ▸ Cleanup** can be set to remove the original video after an operation, and that is a deletion the program performs on its own.

- Every short-lived helper — thumbnails, the waveform, the encoder probes at startup — now ends its process on the way out however it leaves, including when ffmpeg on PATH is a shim that runs the real binary as a child.

### The window could stop responding and look like a crash

- **Hotkey ▸ Set timestamp hotkey…** opens a window that listens for the key you press. Like every dialog, it disables the main window while it is up. Unlike a dialog, it was created belonging to nothing, and Windows keeps a dialog in front of its owner — with no owner there was nothing to keep in front of anything.

- Click the taskbar button while it was open and the main window came forward, still disabled, with the window that disabled it now hidden behind. The result is a program that ignores every click and every key, with nothing on screen explaining why. It is indistinguishable from a hang.

- All five dialogs that were built this way now belong to the window they interrupt, so they stay in front of it and cannot be lost behind it.

### Alt and a letter no longer belongs to the menus

- Every menu had a letter — Alt+F for File, Alt+B for Bookmarks, and so on. Two of them claimed the same letter: Hotkey and Help both answered to Alt+H, so Alt+H opened neither and only moved the highlight between them.

- Rather than reassign it, the shortcuts are gone entirely. The timestamp hotkey can be bound to any combination you like, and a combination that collided with a menu went to the menu bar instead of to the bookmark — a rule nobody should have to know when choosing a hotkey. Nothing is lost: every menu still opens with the mouse, and the one keyboard action worth having is the hotkey itself, which is global and works while the player has focus.

### The timeline

- The bar under the toolbar drew the playback position and the bookmark ranges on top of each other, in eighteen pixels, with overlapping margins. The position line was six pixels tall, sat behind the range marks, and disappeared underneath them — which on a well-marked video is most of the bar.

- They now have a lane each: the range marks in a strip along the top, the playback position in a taller rounded bar beneath, with the waveform behind it. Nothing is drawn over anything else, and the position is legible at a glance from across the desk.

### An open bookmark is now just its start time

- A bookmark waiting for its second timestamp displayed as `[3] 40s - 0s (0s)`, which reads as a cut that begins at forty seconds and runs backwards to nothing. It has no end and no length, so it no longer pretends to: the row is the start time and the word `incomplete`, and the rest appears when the closing timestamp does. The compact overlay follows the same rule.

### The side panel

- **Duration** and **Edit length** now sit side by side. They are the same kind of number and get read against each other — a minute of video, thirty seconds kept — and stacking them put that comparison a line apart.

- **Current hotkey** has been added beneath them, with a keyboard icon that opens the capture window, matching the folder beside the video and the pencil beside the bookmark file. It has left the status bar, which is for reporting what just happened rather than for standing state.

- `<none>` and `(not loaded)` were green, the colour the panel uses for a file it has. They are red now. Nothing loaded is not a healthy state to report in the healthy colour.

- **Play speed** no longer heads an empty column before there are any bookmarks to set a speed on.

### Smaller things

- **The program announced changes it had made itself.** Setting a timestamp from the player, then returning to the window, reported `Reloaded 3 bookmark(s) from disk` — over the top of the message about the timestamp you had just set. The check that spots an edit made in a text editor was comparing against a timestamp the program never updated when it saved, so its own writes looked like somebody else's. It was not only noise: the file is written in time order, so the reload renumbered the rows underneath you.

- **Increment offered a name that was already taken.** Splitting twice into one folder leaves `[done]`, `[done2]` and `[done3]` sitting there, and the prompt proposed `[done2]` — a file plainly on disk — then asked again for each name already used. It now goes straight to the first free one, so the preview is honest and a single click gets past the whole run.

- **"Do this for all remaining files" was printed underneath the buttons.** In both the overwrite prompt and the rename prompt, the checkbox and the buttons shared one row that was not wide enough for them, so the label ran under the buttons and most of it could not be clicked. It has a row of its own now.

- **The filename example in Settings ignored your naming tag.** The line under the filename pattern always demonstrated `[done]`, while the note beside it promised the tag currently selected under Options. It uses the real one.

- **Splitting said two different things at once.** The panel counted finished clips and the status line counted the one in flight, so `1/3` and `Splitting 2/3` appeared together. The status line names the cut being written, the way Convert and Strip audio name the file.

- **`(1 bookmarks)`** reads `(1 bookmark)`.

### Requirements

- Windows, .NET 8 desktop runtime
- MPC-HC with the web interface enabled (Options ▸ Player ▸ Web Interface) — the program detects the port itself and says plainly if the interface is off
- ffmpeg and ffprobe are bundled. Your own build dropped beside the executable still takes precedence.

### Verify your download

```powershell
Get-FileHash -Algorithm SHA256 .\MPC-HC.Video.Editor.zip
```

- | File | SHA-256 | | ---- | ------- | | `MPC-HC.Video.Editor.zip` | `81654B5CC4301281F229C43DE3A3ED231C8269366FE246501D5800E46B341ABA` | | `MPC-HC Video Editor.exe` | `9D1E075DE1C764A559CA7ACDF98C2C46C7332F8B986995C53CF53168C171A0CF` | | `ffmpeg.exe` | `4A01142006A4E2359293E072957DCDA7760C2003BBEEDE037B4551F2CFC8406F` | | `ffprobe.exe` | `8B5298DA673B85E628FBC98535A88848E939E16DF72E856FC727E01AA667E243` | | `LICENSE` | `3972DC9744F6499F0F9B2DBF76696F2AE7AD8AF9B23DDE66D6AF86C9DFB36986` |

- FFmpeg is unchanged — still 9.0.1, GPL v3, from [BtbN/FFmpeg-Builds](https://github.com/BtbN/FFmpeg-Builds).

## 4.9.3 — 2026-09-17

- Clips found by a scan can now be acted on. Merging clips that already match no longer re-encodes them to arrive at what it started with.

### A scan filled the list and left every button greyed out

- **Bookmarks ▸ Find clips automatically…** proposed clips, you accepted them, and then nothing could touch them. The trashcan did nothing. **Remove selected timestamps** was greyed out, and so were **Select All**, **None**, **Play selected**, **Merge**, **Split**, and the flip, rotate and mute buttons — no matter how many rows were ticked.

- The list was fine and so were the checkboxes. The commands were asking a different question. Every one of them is gated on the bookmark file existing on disk rather than on the number of rows on screen, because a timestamp that has not been written is one an interrupted session loses. Setting a timestamp by hand is what creates that file. Accepting a scan did not — it filled the list in memory and left the disk untouched — so the program was right to report no bookmark file, and everything that needs one stayed switched off. The side panel said as much: **CURRENT BOOKMARKS** read `<none>` beside a full list of clips.

- Accepting a scan now writes the file, the same way the first hand-placed timestamp does. A scan you accept nothing from still writes nothing, so an empty bookmark file is never brought into existence.

- **If you have a scanned list open from an earlier version**, it exists only in memory and this build cannot write it for you after the fact. Run the scan again, or set one timestamp by hand, and the file appears.

### Merging clips that already match was slower than it needed to be

- 4.9.2 brought every clip to a common frame rate and frame size before joining them, which is what stopped mixed clips from stalling. It did that whether or not the clips differed. Merging ten clips cut from one video — same size, same rate throughout — paid for a full re-time and re-scale of all ten to arrive at exactly what it began with.

- Both steps are now skipped when the inputs already agree, so the ordinary merge of clips from a single source does no normalising work at all. Mixed clips are handled exactly as before.

### One large clip no longer drags the whole batch up to meet it

- The target frame size was the largest clip in the set. A single 1080p clip among a batch of 640x360 ones pulled every clip up to 1080p — around nine times the encoding work per clip, spent inventing detail the sources never had.

- The target is now the size most of the clips already are, with the largest breaking a tie. The odd clip is converted, rather than everything else being converted to accommodate it. It is still always some input's real dimensions and never a computed box, so a landscape clip merged with a portrait one does not produce a square that neither of them is.

- Frame rate is still the highest in the set, and deliberately so: raising 30 to 60 duplicates frames, loses nothing, and costs double, while lowering 60 to 30 would discard motion that cannot come back.

### An implausible frame rate no longer turns a short merge into a long one

- ffprobe reports `r_frame_rate` as the lowest rate that can express every timestamp in a stream exactly — not the rate the clip actually plays at. One irregular gap is enough to make it report hundreds of frames per second. Because the merge pinned every clip to the highest rate it saw, and encoding time scales with that rate directly, one such file among the inputs could stretch a quick merge into a very long one.

- Rates above 240fps are now treated as the measurement artefact they are and ignored when the target is chosen. 240 covers every high-speed mode a consumer camera offers, and the cap applies only to this choice — nothing is rejected and no clip is refused.

### Requirements

- Windows, .NET 8 desktop runtime
- MPC-HC with the web interface enabled (Options ▸ Player ▸ Web Interface) — the program detects the port itself and says plainly if the interface is off
- ffmpeg and ffprobe are bundled. Your own build dropped beside the executable still takes precedence.

### Verify your download

```powershell
Get-FileHash -Algorithm SHA256 .\MPC-HC.Video.Editor.zip
```

- | File | SHA-256 | | ---- | ------- | | `MPC-HC.Video.Editor.zip` | `DEEE95BEA0EEFA3221EE93E4EABEA9BF801156C4F469CE575FA46FFE81035BE4` | | `MPC-HC Video Editor.exe` | `764F907D2FDE0E7A83B640935E88051D3E9A580112F6B2557F692E691EA6AB69` | | `ffmpeg.exe` | `4A01142006A4E2359293E072957DCDA7760C2003BBEEDE037B4551F2CFC8406F` | | `ffprobe.exe` | `8B5298DA673B85E628FBC98535A88848E939E16DF72E856FC727E01AA667E243` | | `LICENSE` | `3972DC9744F6499F0F9B2DBF76696F2AE7AD8AF9B23DDE66D6AF86C9DFB36986` |

- FFmpeg is unchanged — still 9.0.1, GPL v3, from [BtbN/FFmpeg-Builds](https://github.com/BtbN/FFmpeg-Builds).

## 4.9.2 — 2026-09-14

- Merging clips that do not match now produces a file that plays. The progress counter stops running ahead of its own bar, and the pencil that opens the bookmark file stops disappearing.

### Merging mixed clips produced a file that stalled

- Join a 30fps clip to a 60fps one and playback locked up partway through — usually at the point where the second clip began.

- The merge re-encodes every input before joining them, and that step brought the codec, pixel format and audio into line but left the frame rate alone. The joining step does not re-time anything it is given, so the result was a single file containing two different frame rates, under a container that declared one:

```
r_frame_rate   60/1        the container says 60
avg_frame_rate 8100/181    it is really 44.75, which matches neither clip
frame spacing  33ms for the first clip, 17ms for the second
```

- Some players ride over that. MPC-HC stalls at the discontinuity, which reads as the merge having produced a broken file from that point on.

- Every clip is now brought to one frame rate before the join, and that rate is the **highest** among them. Raising 30 to 60 duplicates frames and loses nothing; lowering 60 to 30 would throw away half of that clip's motion to suit the other one. Broadcast rates are carried exactly — 29.97 is handled as 30000/1001, not as a rounded decimal. This costs no extra work: every clip was already being re-encoded at that point.

### Clips of different sizes were worse

- The same step left frame size alone too. Joining a 640x360 clip to a 1280x720 one gave a file whose container said 640x360 while half its frames were 720p.

- Every clip is now brought to one frame size as well — the size of the largest clip in the set, picked by pixel count so the output matches a real source rather than some computed box none of them has. Smaller or differently shaped clips are **fitted inside it and padded with black**, keeping their geometry: a 4:3 clip joined to a 16:9 one gains bars down the sides rather than being stretched. Pixel aspect ratio is normalised with it, which matters for clips that share a size but declare different pixel shapes — the join used to take only the first and silently stretch the rest.

### Audio drifted at every join

- Found while fixing the above. AAC encodes 1024 samples at a time, so a clip whose video runs exactly 2.000s carries slightly more audio than that — the encoder cannot emit a partial frame. Every clip's audio outlasted its own video, the next clip started before the previous had finished, and the error compounded once per join. On a merge of several clips that is audio sliding out of sync.

- The audio is now made continuous across the joined timeline rather than stitched together from padded pieces. The video is still copied, so the join stays as fast as it was.

### The progress counter ran ahead of the bar

- A merge of five files reached `5/5` at around 67% and then sat there for the rest of the job.

- The counter was reporting the file it was *starting* while the percentage reported work *finished*, so the two disagreed by a step for the whole job — and on a merge by more than that, because the final join is a step the counter had no room for. It now counts what is finished, so it always matches the bar: that same merge reads `4/5` at 67% and reaches `5/5` at the join.

- This was not only merge. Split, convert images, convert video and strip audio all had the same off-by-one. All five now agree with their own progress bars.

### The pencil beside CURRENT BOOKMARKS kept vanishing

- The pencil that opens the bookmark file for editing disappeared whenever the list held only an opening timestamp with no closing one.

- It is hidden when there is nothing to act on, and "something to act on" was read as at least one complete pair. But the bookmark file is created by the *first* timestamp, so a lone opening timestamp is a file that exists and has a row in it — named on screen, with no way to open it. Editing by hand is exactly how a stray timestamp gets fixed, so it went missing at the moment it was most useful.

- It now follows whether the file exists, matching **Delete bookmarks** beside it. **Bookmarks ▸ Edit bookmarks** was greyed out in the same situation and is fixed with it.

### Requirements

- Windows, .NET 8 desktop runtime
- MPC-HC with the web interface enabled (Options ▸ Player ▸ Web Interface) — the program detects the port itself and says plainly if the interface is off
- ffmpeg and ffprobe are bundled. Your own build dropped beside the executable still takes precedence.

### Verify your download

```powershell
Get-FileHash -Algorithm SHA256 .\MPC-HC.Video.Editor.zip
```

- | File | SHA-256 | | ---- | ------- | | `MPC-HC.Video.Editor.zip` | `59EEFE4B243D80AEB29FA84F29842F00594A522A3AB462B1D631E05267B6E87F` | | `MPC-HC Video Editor.exe` | `A119825352A4D0EC792441F3881B4DDB44D683D58F13E40A655E0183022C1711` | | `ffmpeg.exe` | `4A01142006A4E2359293E072957DCDA7760C2003BBEEDE037B4551F2CFC8406F` | | `ffprobe.exe` | `8B5298DA673B85E628FBC98535A88848E939E16DF72E856FC727E01AA667E243` | | `LICENSE` | `3972DC9744F6499F0F9B2DBF76696F2AE7AD8AF9B23DDE66D6AF86C9DFB36986` |

- FFmpeg is unchanged — still 9.0.1, GPL v3, from [BtbN/FFmpeg-Builds](https://github.com/BtbN/FFmpeg-Builds).

## 4.9.1 — 2026-09-14

- A fix for the update notice in 4.9, which could not be dismissed.

### "Not now" did nothing

- The window that appears when a newer version exists had a dead button. Clicking **Not now** left it on screen. Esc did nothing either. The only way out was the close button in the title bar.

- The two buttons were closing the window in different ways, and only one of them worked here. **Open the releases page** closes the window itself, in code. **Not now** relied on `IsCancel`, which closes a window by setting its dialog result — and a dialog result only means anything for a window opened as a modal dialog. This notice is deliberately not modal: it arrives on its own schedule, possibly while the main window is hidden behind the compact overlay or sitting in the notification area, and a modal window owned by something you cannot see is a window you cannot answer. So it is shown as an ordinary window, the dialog result had nowhere to go, and the button quietly did nothing at all.

- Both are now dismissed explicitly, and Esc closes the notice as it always should have.

- Nothing else changed. **Not now** still leaves the update check switched on — only the **Stop checking for new versions** link inside the notice turns it off.

- Everything else in 4.9 is unaffected, and every other dialog in the program was already correct: they are all shown as modal dialogs, where `IsCancel` does close the window.

### If you are on 4.9

- This is worth taking, because the broken button is on the notice that tells you about it. Dismiss that notice with the title bar close button, then download below.

### Requirements

- Windows, .NET 8 desktop runtime
- MPC-HC with the web interface enabled (Options ▸ Player ▸ Web Interface) — the program detects the port itself and says plainly if the interface is off
- ffmpeg and ffprobe are bundled. Your own build dropped beside the executable still takes precedence.

### Verify your download

```powershell
Get-FileHash -Algorithm SHA256 .\MPC-HC.Video.Editor.zip
```

- | File | SHA-256 | | ---- | ------- | | `MPC-HC.Video.Editor.zip` | `E750D63608A7BB241690BADBE99B6F4BE6E3A762369E756932ED93DD2859D19F` | | `MPC-HC Video Editor.exe` | `158B544D83E6D1D028E7E33A41CD5FF94DE217F679C49AF92F2D06F0FDFB68E4` | | `ffmpeg.exe` | `4A01142006A4E2359293E072957DCDA7760C2003BBEEDE037B4551F2CFC8406F` | | `ffprobe.exe` | `8B5298DA673B85E628FBC98535A88848E939E16DF72E856FC727E01AA667E243` | | `LICENSE` | `3972DC9744F6499F0F9B2DBF76696F2AE7AD8AF9B23DDE66D6AF86C9DFB36986` |

- FFmpeg is unchanged — still 9.0.1, GPL v3, from [BtbN/FFmpeg-Builds](https://github.com/BtbN/FFmpeg-Builds).

## 4.9 — 2026-09-14

- Upgrading no longer costs you your settings, the overlay can drive the player, and a bookmark you have not finished is still a bookmark you can tick.

### Replacing the folder used to wipe your settings

- The program is one folder with no installer, so the natural way to upgrade is to delete the old folder and unpack the new one. That took `settings.json` with it. The new copy started up, found nothing, and did what any first run does — it started from defaults. Shortcuts, naming tags, suffixes, the timestamp hotkey, recent files: gone, with nothing on screen to say why.

- Every settings save now also writes a copy to `%APPDATA%\MPC-HC Video Editor\settings.json`. That copy is read in exactly one situation: the program starts, finds no settings beside the executable, and finds the copy. Then it restores from it.

- The program is still portable. It reads and writes its settings beside the executable, as before, and a local file always wins — so a copy of the folder you deliberately made fresh stays fresh, and two installs on one machine do not argue about which is in charge. The backup is a spare key, not a second home.

- If you have ever run a pre-portable version, this is the same place it kept its settings, and the same step carries them over. That has always worked; it is now the general rule rather than a one-time migration.

### The overlay can seek

- **Settings ▸ Overlay ▸ Clicking.** Off by default. Turn it on and the timestamps on the compact overlay seek MPC-HC when clicked, the same as the ones in the full window.

- Read the trade before you turn it on. The overlay is normally click-through: clicks land on the video behind it, so it can sit over a fullscreen player without being in the way. Making the timestamps clickable means the panel catches clicks — that corner of the screen stops pausing the video. The transparent area around the panel is unaffected, and only the panel itself changes behaviour.

- The overlay still never takes focus. Seeking from it leaves MPC-HC in front, so the full window does not come back mid-click.

### A bookmark with one timestamp can be ticked

- Set an opening timestamp and its checkbox was greyed out until you set a closing one. That made a lone opening timestamp the only row in the list you could not mark — and "Delete selected" works on marks, so it was also the only row you could not delete that way. Deleting the thing you just started was the one job the check most needed to do.

- Every row is checkable now. **Select All** includes open bookmarks, and a check survives a bookmark being reopened instead of being silently cleared.

- Nothing acts on a half-finished cut as a result. Merge, split, play selected, flip, rotate and mute read past the check for a real start and end, so an open bookmark is invisible to them however it is ticked.

### The timeline position is blue

- It was amber, drawn over a timeline whose range marks are also amber. The playhead and the cuts were the same colour on the default theme. Blue is the one hue none of the marks use, so the position now reads as separate from them at a glance, in every theme.

### Requirements

- Windows, .NET 8 desktop runtime
- MPC-HC with the web interface enabled (Options ▸ Player ▸ Web Interface) — the program detects the port itself and says plainly if the interface is off
- ffmpeg and ffprobe are bundled. Your own build dropped beside the executable still takes precedence.

### Verify your download

```powershell
Get-FileHash -Algorithm SHA256 .\MPC-HC.Video.Editor.zip
```

- | File | SHA-256 | | ---- | ------- | | `MPC-HC.Video.Editor.zip` | `8439A497649D43CD70133DE00104B18DCAA887C003BCF713746616B92F249D0E` | | `MPC-HC Video Editor.exe` | `4245A4A83873D8EC81CE99684E3F6756C525DBBD79E13C93AFB5162AA647CAA3` | | `ffmpeg.exe` | `4A01142006A4E2359293E072957DCDA7760C2003BBEEDE037B4551F2CFC8406F` | | `ffprobe.exe` | `8B5298DA673B85E628FBC98535A88848E939E16DF72E856FC727E01AA667E243` | | `LICENSE` | `3972DC9744F6499F0F9B2DBF76696F2AE7AD8AF9B23DDE66D6AF86C9DFB36986` |

- FFmpeg is unchanged — still 9.0.1, GPL v3, from [BtbN/FFmpeg-Builds](https://github.com/BtbN/FFmpeg-Builds).

## 4.8 — 2026-09-14

- Every long-running operation now reports itself the same way, and the progress panel says one thing once.

### The panel said the same thing three ways

- A merge of ten files used to show this:

```
Merge files      File 1 of 10                 27%
holiday-footage-part2[done2].mp4
Preparing 4/10
```

- Three rows, two counters, and the prominent one was wrong. **File 1 of 10** was set once before the loop started and never again, so it read `1` for the whole job however many files went by. The counter that did move was the one buried in the step text below it — and it was there because the step had to carry its own count to be useful, the panel's counter being broken.

- There is one counter now, it moves, and the step no longer duplicates it:

```
Merging files · Preparing          4/10      27%
clip-04.mp4
```

- The action and the step share a line because the step alone is rarely a sentence — "Preparing", "Encoding to MP4", "Extracting to MP3" — and reads as one phrase with the action in front of it. That removed a row without removing anything you could read.

- Two step messages were repeating their own action and have been trimmed: `Writing PNG` under **Converting images to PNG** is now `Writing`, and `Extracting audio to MP3` under **Stripping audio** is now `Extracting to MP3`.

### The file name is the file being worked on

- It used to be the output name for the whole job, which on a merge is derived from the first input and so reads exactly like one — a name that never changes while a counter next to it climbs to ten. It now names the input being processed, and switches to the file that was produced when the job finishes.

### The bar has bands

- Black track. Red below 25%, orange to 50, yellow to 75, blue to 99, green at 100. The previous bar interpolated continuously between black, red, orange, yellow and green, which meant the colour at any moment was a shade between two others and told you nothing the percentage did not already say. Five bands are legible at a glance from across the room; a gradient is not.

### Finishing is an event

- A finished job held its full bar for 600ms and vanished — long enough to notice something had happened, not long enough to read what. The bar now lands on solid green and holds for three seconds, with the headline replaced by what actually occurred and the file that was created beneath it:

```
Merged 10 files into              10/10     100%
holiday-footage-part2[done2].mp4
```

- Operations that write many files name the destination folder instead, there being no single file to name.

- The hold runs after the operation releases the UI rather than inside it. The old 600ms pause sat in the middle of the work with the busy flag still set, so it froze the toolbar for its duration; this one does not. Jobs that have follow-up work — the cleanup prompt after a trim or a split — start it immediately and let the bar finish holding behind the prompt. A new job started while a finished bar is still up simply takes the panel over.

- Failures and cancellations still clear the panel at once. Only a job that produced something gets the green.

### Every action, one format

- The panel behaved differently depending on which button you pressed. All seven operations now report identically — the counter, the percentage, the banded bar, the green hold:

- | While running | On completion | | ------------- | ------------- | | `Merging files · Preparing` | `Merged 10 files into` | | `Trimming cut` / `Merging cuts` | `Trimmed cut into` / `Merged 3 cuts into` | | `Splitting clips · Cutting 0:10 → 0:25` | `Split into 8 clip(s) in` | | `Converting images to PNG · Writing` | `Converted 12 image(s) to PNG in` | | `Converting video · Encoding to MP4` | `Converted 4 file(s) to MP4 in` | | `Stripping audio · Extracting to MP3` | `Extracted audio from 4 file(s) to` | | `Writing chapters` | `Wrote 14 chapters into` |

- Action names are present participles throughout — **Strip audio** became **Stripping audio**, **Convert video** became **Converting video**. A panel that only appears while something is running should read as something in progress.

### One progress bar, not two

- The status bar carried a second progress bar at its right end, bound to the same percentage as the panel directly above it. It is gone.

### Requirements

- Windows, .NET 8 desktop runtime
- MPC-HC with the web interface enabled (Options ▸ Player ▸ Web Interface) — the program detects the port itself and says plainly if the interface is off
- ffmpeg and ffprobe are bundled. Your own build dropped beside the executable still takes precedence.

### Verify your download

```powershell
Get-FileHash -Algorithm SHA256 .\MPC-HC.Video.Editor.zip
```

- | File | SHA-256 | | ---- | ------- | | `MPC-HC.Video.Editor.zip` | `71680150E4B592E0614B7672B2CA5A136C51BABA53D6AA27D5D52E5127F482A5` | | `MPC-HC Video Editor.exe` | `694D265C6A30A81194E552CAB3B4AD18772A0C8AFE2C4FFA001D8308F18FCDA9` | | `ffmpeg.exe` | `4A01142006A4E2359293E072957DCDA7760C2003BBEEDE037B4551F2CFC8406F` | | `ffprobe.exe` | `8B5298DA673B85E628FBC98535A88848E939E16DF72E856FC727E01AA667E243` | | `LICENSE` | `3972DC9744F6499F0F9B2DBF76696F2AE7AD8AF9B23DDE66D6AF86C9DFB36986` |

- FFmpeg is unchanged — still 9.0.1, GPL v3, from [BtbN/FFmpeg-Builds](https://github.com/BtbN/FFmpeg-Builds).

## 4.7 — 2026-09-09

- Times read the same way everywhere, and the bookmark row is laid out to be read rather than parsed.

### One rule per time style

- Times had drifted into inconsistent readings — a four-second mark shown as `0:04` in one place and `4s` in another, an hour written `1h` here and `1:00:00` there. There are now three styles and one rule for choosing between them:

- | style | reads | used for | | ----- | ----- | -------- | | spoken | `4s` · `1m 35s` · `4hr 25m 30s` | how long something lasts | | clock | `45s` · `2:05` · `1:22:05` | where you are in the video | | precise | `00:00:05` · `01:15:30` | fixed width, for editing |

- The clock style has one rule about zeros: a zero may follow a figure (`5:00`) or sit between two (`1:00:04`), but a reading never opens with one. So there is no `0:04` and no `0:45` — below a minute there is no minutes figure to lead with, and the reading is simply `4s`. There is no `01:45` either; the leading figure is never padded.

- That rule lives inside the clock formatter rather than in the code that calls it. A caller that forgot it would print `0:04`, and every caller would have to remember it for the program to read consistently.

- Two things were corrected on the way:

- A length now reads `1hr`, not `1h`.
- Hours are counted from the total, not from `TimeSpan.Hours`, which rolls over into days. A 25-hour recording used to report as one hour in.

### The bookmark row

```
[1] < 4s > - < 12s > (8s)     pointer over the row
[1]   4s   -   12s   (8s)     pointer elsewhere
```

- Every gap is one space — a literal space, not a margin in pixels, which would only ever be a guess at how wide a space is in whatever font and size the row ends up using.

- The frame-nudge arrows now flank the timestamp they move, and they are hidden rather than removed when the pointer is elsewhere. They keep their width, so hovering a row reveals them instead of shoving the timestamps sideways to make room. Nothing in the row moves except when a timestamp genuinely changes width — at `59s → 1:00`, and again at `59:59 → 1:00:00`.

- The index no longer sits in a fixed-width column that padded `[1]` out to the width of `[10]` before the timestamps could start.

- The two timestamps are also centered in the row now, which they never were. They were the only elements in it without a vertical alignment, so they filled the row's height and drew their text at the top of it. While every row was exactly one line tall that was invisible. With chapter names turned on the rows are taller than one line, and the two timestamps alone sat raised above the dash and the duration beside them. The chapter name box is shortened to the row's own text height as well, and given the same single space before it that everything else in the row has.

### The file actions moved next to the files

- **Edit bookmarks** was a button across the top of the window, nowhere near anything it referred to. It is now a pencil beside CURRENT BOOKMARKS, next to the name of the file it opens, and it appears only when there are bookmarks to edit.

- CURRENT VIDEO gains a folder icon on the same terms, opening Explorer with the video selected.

- Both icons take their visibility from whether their action is available, so "shown when there is something to act on" and "clickable when there is something to act on" cannot drift apart.

- **Enter time** keeps its Actions menu entry and loses its button. Neither it nor Edit bookmarks was earning a place in the toolbar, which now fits on one row.

### The bookmark count

- The separate "2 items" line is gone; the caption reads **Bookmarks: 2**.

- The figure changed with the move. The old count included a lone opening timestamp still waiting for its close — a row in the list, but not yet a cut — and so promised one more clip than a split would actually produce. The caption counts cuts with both ends.

### Requirements

- Windows, .NET 8 desktop runtime
- MPC-HC with the web interface enabled (Options ▸ Player ▸ Web Interface) — the program detects the port itself and says plainly if the interface is off
- ffmpeg and ffprobe are bundled. Your own build dropped beside the executable still takes precedence.

### Verify your download

```powershell
Get-FileHash -Algorithm SHA256 .\MPC-HC.Video.Editor.zip
```

- | File | SHA-256 | | ---- | ------- | | `MPC-HC.Video.Editor.zip` | `26A1059D1BD343D7A45A53058AD9AD14B60E4481CB5B58427D3BA49E11535A4B` | | `MPC-HC Video Editor.exe` | `3DA67E751B04EF91FDC7AEE8DE09D679A590CC6B954B13AA0EC3CADF34BD568A` | | `ffmpeg.exe` | `4A01142006A4E2359293E072957DCDA7760C2003BBEEDE037B4551F2CFC8406F` | | `ffprobe.exe` | `8B5298DA673B85E628FBC98535A88848E939E16DF72E856FC727E01AA667E243` | | `LICENSE` | `3972DC9744F6499F0F9B2DBF76696F2AE7AD8AF9B23DDE66D6AF86C9DFB36986` |

- FFmpeg is unchanged — still 9.0.1, GPL v3, from [BtbN/FFmpeg-Builds](https://github.com/BtbN/FFmpeg-Builds).

## 4.6 — 2026-09-09

- Each cut can now be turned, silenced and named, either mark can be moved a frame at a time, and every edit to the list can be undone.

### Rotate

- **Rotate** cycles a cut through the four quarter turns, from one button beside Flip. One target is taken for the whole selection rather than advancing each cut from wherever it already was — otherwise a mixed selection stays mixed forever, each press moving every cut one step and never bringing them into agreement. Flip has always worked this way.

- It is applied as `transpose`, before the speed filter, since it swaps width and height.

### Mute

- **Mute** silences a cut with `volume=0` rather than dropping the audio.

- That distinction matters more than it looks. The concat demuxer requires every segment to carry the same streams in the same order, so a segment with its audio stripped cannot be joined to one that still has it. Silencing keeps the stream in place carrying nothing, and a muted cut merges with an unmuted one.

### Chapter names

- **Settings ▸ General ▸ Chapter names**, off by default. Each range is named as it is created, and the name is editable on its row.

- Only names the program assigned are renumbered when the list changes, so a cut you have renamed by hand keeps its name wherever it ends up. Turning the setting off clears every name, hand-typed ones included — that is what the switch means, and the dialog says so before it does it.

- Chapter export titles each chapter with its name when there is one.

### Frame nudging

- Either mark moves by a single frame, using the rate read from the file itself. `r_frame_rate` is rational, so `30000/1001` is divided rather than read as a decimal. The two marks are kept at least one frame apart.

### Undo and redo

- **Ctrl+Z / Ctrl+Y**, covering every edit to the cuts.

- By snapshot rather than by reversible commands. A command per action would have to be threaded through every place the list is mutated, and would still miss anything edited straight through a data binding — which never passes a command at all. Because of that, no existing command needed changing to gain undo.

- Edits within 600 ms coalesce, so one slider drag is one step. Ticking a checkbox is not a step, though the selection travels in the snapshot, so undo puts it back. History resets when a different video or bookmark file is opened.

- *File operations are deliberately excluded.* A finished merge has written an encoded file and may have recycled its source. Undo there would be a promise the program cannot keep.

### Reveal last output

- Opens Explorer with the written file selected. The status bar has named that file since 4.0, but naming a file is not the same as being able to get to it.

### The bookmark CSV carries the name and the per-clip settings

- This was not optional. Speed and flip were held only in memory and lost whenever the list reloaded — which the main window does *every time it is activated* — so a name would not have survived alt-tabbing away.

- The third field held `BookmarkN`, a row number that was discarded on load, and now holds the name; a field still matching the old placeholder reads as no name. Everything after it is optional, so a file written by any earlier build loads unchanged.

### A menu bar reorder, and the bug it exposed

- The menus are now File, Edit, Actions, Hotkey, Bookmarks, Playlist, History, Shortcuts, Options, View, Help, each with a small glyph.

- Adding those glyphs broke four menus, and the fix is the part worth recording. `FindMenuItem` located top-level menus by matching header text exactly, so changing `"_Options"` to `"⚙ _Options"` made the match fail. The method returned null, every caller responded by returning quietly, and the naming tags, Shortcuts, Playlist and quick-save lists silently stopped filling themselves in. Nothing threw and nothing logged. The failure looked exactly like having nothing to show.

- This had already happened once before, recorded in a comment in the file: a menu renamed from `_Suffix` to `_Options` hid newly added tags the same way. So `FindMenuItem` is deleted rather than patched, and those four menus are found by `x:Name` — checked by the compiler, and unable to drift when someone rewords a header.

### Spelling

- American spelling throughout the interface and the source. The historical release notes are left as they were written.

### Requirements

- Windows, .NET 8 desktop runtime
- MPC-HC with the web interface enabled (Options ▸ Player ▸ Web Interface) — the program detects the port itself and says plainly if the interface is off
- ffmpeg and ffprobe are bundled. Your own build dropped beside the executable still takes precedence.

### Verify your download

```powershell
Get-FileHash -Algorithm SHA256 .\MPC-HC.Video.Editor.zip
```

- | File | SHA-256 | | ---- | ------- | | `MPC-HC.Video.Editor.zip` | `9FBD82A71BFAD77F38AEFB54BA5B2A3BDBF3D8A802E0AE523F06E77DF6F19555` | | `MPC-HC Video Editor.exe` | `8C00A57213BBD04E1E9295F155AD5C65CE7C5A309910B4728B1EDB51503D613D` | | `ffmpeg.exe` | `4A01142006A4E2359293E072957DCDA7760C2003BBEEDE037B4551F2CFC8406F` | | `ffprobe.exe` | `8B5298DA673B85E628FBC98535A88848E939E16DF72E856FC727E01AA667E243` | | `LICENSE` | `3972DC9744F6499F0F9B2DBF76696F2AE7AD8AF9B23DDE66D6AF86C9DFB36986` |

- FFmpeg is unchanged — still 9.0.1, GPL v3, from [BtbN/FFmpeg-Builds](https://github.com/BtbN/FFmpeg-Builds).

## 4.5 — 2026-09-07

- The program now tells you when there is a newer version.

### It says when a newer release has been published

- A fix released to a page nobody is watching reaches nobody. Every version until this one had to be found by going and looking.

- Once, as the program starts, it asks GitHub for the latest release and compares the version with the running build. If that version is newer, a notice says so and offers the releases page. If it is not, nothing appears.

- It reads a version number and nothing else. Nothing is downloaded and nothing installs itself — this is a portable folder, and replacing it stays your business. The notice only points you at the page.

- The way to stop the check is in the notice itself, not only in a settings tab you would have to go looking for, and the same line says where to turn it back on:

- > This check runs once each time the program starts. > **Stop checking for new versions** — you can turn it back on at any time in > Settings ▸ General ▸ Updates, or check by hand from Help ▸ Check for updates.

- A machine that is offline when the program opens has not run into a problem worth a dialog, so a check that cannot complete says nothing at all.

### The setting

- **Settings ▸ General ▸ Updates**, on by default — including on an existing install, whose settings file predates the setting and so takes the default. That is deliberate: the people most worth telling about a fix are the ones already running the build it fixes.

- Turn it off and the program never contacts GitHub on its own.

### Checking on demand

- **Help ▸ Check for updates** asks whenever you want it, and answers either way — an up-to-date install says so rather than appearing to do nothing. It works whether or not the startup check is turned on.

### Requirements

- Windows, .NET 8 desktop runtime
- MPC-HC with the web interface enabled (Options ▸ Player ▸ Web Interface) — the program detects the port itself and says plainly if the interface is off
- ffmpeg and ffprobe are bundled. Your own build dropped beside the executable still takes precedence.

### Verify your download

```powershell
Get-FileHash -Algorithm SHA256 .\MPC-HC.Video.Editor.zip
```

- | File | SHA-256 | | ---- | ------- | | `MPC-HC.Video.Editor.zip` | `D57808B567139B0465DA5B3C90DD1ED93C5F33AC6E10710E757C013540F10877` | | `MPC-HC Video Editor.exe` | `74BE6E29D05C8E29AC0A52838E14AD0B4A347C27BAB71898B5DDA2F8DC33EE98` | | `ffmpeg.exe` | `4A01142006A4E2359293E072957DCDA7760C2003BBEEDE037B4551F2CFC8406F` | | `ffprobe.exe` | `8B5298DA673B85E628FBC98535A88848E939E16DF72E856FC727E01AA667E243` | | `LICENSE` | `3972DC9744F6499F0F9B2DBF76696F2AE7AD8AF9B23DDE66D6AF86C9DFB36986` |

- FFmpeg is unchanged — still 9.0.1, GPL v3, from [BtbN/FFmpeg-Builds](https://github.com/BtbN/FFmpeg-Builds).

## 4.4 — 2026-09-07

- The status bar stops hiding the half of the message that explains the result.

### A long message now scrolls instead of being cut off

- After an operation the status bar reports what was produced, and that report carries the output file name:

- > Created `<name>` — The finished file is 0.7s longer than the marked range > (0:26 against 0:26).

- With a long file name, everything after the name ran off the end of the bar and was simply clipped. The measurement added in 4.0 — the whole point of which is to say when a keyframe-aligned cut came out longer than what was marked — was the part that disappeared, because it comes last. The longer the file name, the less of the explanation survived, and file names of the length this program is usually pointed at left nothing of it at all.

- The status text now scrolls when it is wider than the bar. It holds at the start, runs to the end, holds there, and runs back, so both ends come to rest long enough to read. A message that fits does not move at all, and a new message stops the scroll and starts again from the left.

### The play speed column is labelled more briefly

- **PLAY SPEED OF THE SAVED CLIP** is now **PLAY SPEED**. The tooltip on the header already explains what it changes and what it does not.

### Requirements

- Windows, .NET 8 desktop runtime
- MPC-HC with the web interface enabled (Options ▸ Player ▸ Web Interface) — the program detects the port itself and says plainly if the interface is off
- ffmpeg and ffprobe are bundled. Your own build dropped beside the executable still takes precedence.

### Verify your download

```powershell
Get-FileHash -Algorithm SHA256 .\MPC-HC.Video.Editor.zip
```

- | File | SHA-256 | | ---- | ------- | | `MPC-HC.Video.Editor.zip` | `858410CB3286946BCC1215CB2170E10A3216AD10100B70F3C22C07211A2AB7A0` | | `MPC-HC Video Editor.exe` | `3E42EB66A913A4832FDEE824ED04287FDE61373EE2842AD541530C741739AB40` | | `ffmpeg.exe` | `4A01142006A4E2359293E072957DCDA7760C2003BBEEDE037B4551F2CFC8406F` | | `ffprobe.exe` | `8B5298DA673B85E628FBC98535A88848E939E16DF72E856FC727E01AA667E243` | | `LICENSE` | `3972DC9744F6499F0F9B2DBF76696F2AE7AD8AF9B23DDE66D6AF86C9DFB36986` |

- FFmpeg is unchanged — still 9.0.1, GPL v3, from [BtbN/FFmpeg-Builds](https://github.com/BtbN/FFmpeg-Builds).

## 4.3 — 2026-09-01

- A connection bug that made the program look broken on current MPC-HC, and the end of picking the view by hand.

### The program no longer loses the video a few seconds after you open it

- Reported in [#10](https://github.com/gouryellas/MPC-HC-Video-Editor/issues/10) by **Land-Strider**, on MPC-HC 2.8.1.

- Open a video and everything populated correctly — the overlay showed the file and its bookmarks, the full window agreed. Seven or eight seconds later the lot went blank, as though no video were loaded, with the player still running and still playing.

- Nothing was disconnecting. The program identifies what is open by reading MPC-HC's window title, and it insisted the title contain the words *Media Player Classic*. That is what the older builds say. MPC-HC 2.x titles its window `<file> - MPC-HC`, which never matched, so the file was never identified and the program concluded — on the very first check — that nothing was playing.

- The eight seconds were the grace period that holds off the "nothing is playing" conclusion while a newly opened file settles. It was the only thing keeping the session alive, and when it expired the session was cleared. So a total failure to read the title looked like a connection dropping out after a delay.

- The title is now read for the *file* rather than for the player name, which is not something to depend on: the longest leading run of the title that looks like a file name is taken, working right to left over the separators so that `Artist - Track.mp4 - MPC-HC` stays intact. Every arrangement now works — `- MPC-HC`, `- MPC-HC x64`, `- Media Player Classic`, `- Media Player Classic Home Cinema`, `- MPC-BE x64`, a full path with no player name at all, and a bare file name.

- This affected every user of MPC-HC 2.x whose title bar was set to show the file name. It did not affect anyone whose title bar shows the full path, because a path that exists on disk was recognised by a separate route — which is why it went unnoticed here.

### What is playing is now read from the Web Interface

- The window title is no longer the only source. MPC-HC's Web Interface reports the full path of the open file outright, with no player name to strip and no dependence on what the title bar is configured to show, and it is now asked first. The title remains the fallback for players with the interface switched off.

- This also covers the one case the title cannot: with MPC-HC's **Title bar text** set to **Don't display**, there is no file name on screen anywhere, and before this the program had nothing to go on.

- The interface is read on a background task and never on the poll, so a player that stops answering cannot stall the window. An interface that is off, or on a different port, costs nothing beyond falling back to the title.

### One view control, on by default

- **View ▸ Minimal** and **View ▸ Full** are gone. **View ▸ Switch views automatically** is now the whole of it, and is **on by default**.

- Picking the view by hand was a mode with nothing on screen naming it. Choosing Minimal pinned the overlay until X or Full, and the pin outlived the overlay: it survived the overlay hiding itself for an unrelated window, so returning to the player put the overlay back up with nothing anywhere saying why. Worse, the pin was checked ahead of the setting, so **unchecking "switch views automatically" did not stop the view switching** for anyone who had picked Minimal earlier in the session. That whole layer is removed rather than patched.

- What is left is the behaviour both were mostly being used to get: the overlay while MPC-HC is the active window, the full window as soon as it is not.

- Upgrading turns the setting on once. It has been written to the settings file on every run since it existed, so an install upgrading from 4.2 carries an explicit "off" that a changed default would never reach — and with Minimal gone, that would leave no way to the overlay at all. Turn it off after upgrading and it stays off.

### Leaving the player now brings the window to the front

- Clicking away from MPC-HC restores the full window. It was being restored *behind* whatever had just been clicked, which is not a window coming back in any sense you can see.

- It now comes to the front. Briefly, and once — it is not left always-on-top, and clicking back to the player hands the foreground straight over.

### X works from a fullscreen player

- **X** brings the full window back. Against a fullscreen player it appeared to do nothing: the key was arriving and the window was being restored, but a fullscreen player covers the monitor, holds the foreground and sits above the ordinary window order, so the window came back behind it.

- X now takes the player out of fullscreen on the way. Nothing can be put in front of a fullscreen player, so leaving it is part of what showing the window costs. A windowed player is left exactly as it is.

### The overlay says which way out applies

- The overlay's footer said *press X* whichever way the player was presented. With the player in a window, clicking anywhere else already brings the full window back — so a user who did the obvious thing saw the window appear for a reason the overlay had never mentioned.

- It now names whichever applies:

- Fullscreen — `Hitting  X  restores the program`
- Windowed — `Unfocusing MPC-HC restores the program`

- X works either way. The line picks which one to name, not which one exists.

### The timestamp hotkey confirms itself when the overlay is off

- With automatic switching turned off, the full window stays up permanently — and it is behind the player when the hotkey is pressed. The overlay is not there to list the new bookmark either, so the hotkey recorded a timestamp and produced no visible response at all.

- Pressing it now always confirms on screen, with the bookmark number and the time recorded. This appears even with toasts switched off, but only when there is genuinely nothing else to show it — never while the overlay is up or while the full window has focus, because in both of those cases something on screen already says what happened.

### Requirements

- Windows, .NET 8 desktop runtime
- MPC-HC with the web interface enabled (Options ▸ Player ▸ Web Interface) — the program detects the port itself and says plainly if the interface is off
- ffmpeg and ffprobe are bundled. Your own build dropped beside the executable still takes precedence.

### Verify your download

```powershell
Get-FileHash -Algorithm SHA256 .\MPC-HC.Video.Editor.zip
```

- | File | SHA-256 | | ---- | ------- | | `MPC-HC.Video.Editor.zip` | `C10ADD4B0123886812C9DA5894AE0B5CA6CBC0B531D2C9C672563596D23CD270` | | `MPC-HC Video Editor.exe` | `7F148CE06100A32F08DBF2B66E1C557DF509F6DF3B6D53D26270CAFE781EDBAB` | | `ffmpeg.exe` | `4A01142006A4E2359293E072957DCDA7760C2003BBEEDE037B4551F2CFC8406F` | | `ffprobe.exe` | `8B5298DA673B85E628FBC98535A88848E939E16DF72E856FC727E01AA667E243` | | `LICENSE` | `3972DC9744F6499F0F9B2DBF76696F2AE7AD8AF9B23DDE66D6AF86C9DFB36986` |

- FFmpeg is unchanged — still 9.0.1, GPL v3, from [BtbN/FFmpeg-Builds](https://github.com/BtbN/FFmpeg-Builds).

## 4.2 — 2026-08-31

- A bug fix. The compact overlay could take the whole program off the screen with it.

### The overlay no longer leaves the program with nothing on screen

- **View ▸ Minimal** pins the overlay, and putting the overlay up hides the full window — that is the swap it exists to make. While it was pinned, moving focus to some third application — a browser, a file manager, anything that is not MPC-HC — hid the overlay so it would not float over that window, but left the full window hidden behind it.

- The program then had nothing on screen at all: no window, no taskbar button, and the **X** key disarmed along with the overlay it belongs to, because X is deliberately live only while there is an overlay to explain it.

- In notification-area mode the tray icon still brought it back. In application mode there is no tray icon, so the only way back was clicking MPC-HC again — the program was running and, to anyone who had switched to something else, gone.

- The pin now holds the *mode* rather than one particular window. With the overlay pinned:

- MPC-HC has focus → the overlay is up, exactly as before.
- Anything else has focus → the full window comes back, and the pin is kept.
- Returning to the player drops straight back to the overlay.

- So the overlay still stays out of the way of an unrelated window, which is what hiding it was for, and one of the two windows is always on screen.

- **View ▸ Switch views automatically** was not at fault and has not changed. It is worth stating plainly why it looked broken: a pin is checked before the setting, by design, so once the overlay had been pinned by hand the setting had no opportunity to act. That layering is unchanged — but both paths now agree about the full window, so it is no longer something that can strand you.

### Screenshots

- The images on the project page were stale — they still showed the pre-4.1 appearance. They are now the current interface on the Graphite theme, including all five Settings tabs.

### A plainer icon on the program file

- The icon baked into the executable was the graphite camera — one theme's colours, on a file that four themes share and that no theme can repaint. It is now black and white: the same drawing, in no theme's colours, so it cannot disagree with the theme actually in use.

- The icons you see while the program runs are unchanged and still follow the theme.

- > **The archive was reissued after publication** to include this icon, so the > hashes below are not the ones this release first carried. If you downloaded > within the first couple of hours and verified against the original table, > your copy is the same program without the new file icon. Re-download if you > would rather the hashes match.

### Requirements

- Windows, .NET 8 desktop runtime
- MPC-HC with the web interface enabled (Options ▸ Player ▸ Web Interface) — the program detects the port itself and says plainly if the interface is off
- ffmpeg and ffprobe are bundled. Your own build dropped beside the executable still takes precedence.

### Verify your download

```powershell
Get-FileHash -Algorithm SHA256 .\MPC-HC.Video.Editor.zip
```

- | File | SHA-256 | | ---- | ------- | | `MPC-HC.Video.Editor.zip` | `A7E15A2FE21770897D6FB3C020A929AEF176C7F1877DE5F1598A98A5D250A0B4` | | `MPC-HC Video Editor.exe` | `E9331E6781A79129F22698BACDD43F20D85909B2711B8D209B90FA0EFCAD08BC` | | `ffmpeg.exe` | `4A01142006A4E2359293E072957DCDA7760C2003BBEEDE037B4551F2CFC8406F` | | `ffprobe.exe` | `8B5298DA673B85E628FBC98535A88848E939E16DF72E856FC727E01AA667E243` | | `LICENSE` | `3972DC9744F6499F0F9B2DBF76696F2AE7AD8AF9B23DDE66D6AF86C9DFB36986` |

- FFmpeg is unchanged — still 9.0.1, GPL v3, from [BtbN/FFmpeg-Builds](https://github.com/BtbN/FFmpeg-Builds).

## 4.1 — 2026-08-31

- A new look, and the interface's colours are now yours to choose.

### Colour themes

- **Settings ▸ General ▸ Appearance** offers three:

- **Graphite** — warm charcoal with an amber accent. The new default.
- **Midnight** — cool blue-grey with a cyan accent.
- **Daylight** — light surfaces with an indigo accent.

- Picking one applies it immediately, so it can be judged rather than guessed at from a name. Cancel puts the previous one back, including if the dialog is closed with Escape.

- Underneath, every colour in the interface now resolves through a named role rather than being written out where it is used. The application previously spelled out 55 distinct values across twelve files, which is why it had exactly one appearance; a fourth theme is now a single entry in one table.

### A new icon, and it follows the theme

- The application icon is now a film camera, drawn in the colours of whichever theme is active. The window icon, the taskbar icon while the program is running, and the notification-area icon all repaint when the theme changes, and come back in the right colours the next time the program is opened.

- **One exception, stated plainly:** the icon on the program file itself — what Explorer shows, and what a pinned shortcut uses — is fixed when the program is built and cannot follow the theme. Changing it would mean the program rewriting its own executable, which would invalidate the published checksums below and is a thing well-behaved software does not do.

### Filename variables, explained properly

- The naming pattern settings used to describe their variables in a single dense paragraph, with two — `{index}` and `{index2}` — whose difference the names did nothing to convey.

- They are now a table: each variable, a worked example of what it produces, and what it is for. Click one to insert it. Four ready-made patterns sit below, each with its actual output, applied with a click.

- The numbering variables now say what they do:

- | | | | | --- | --- | --- | | `{number}` | `3` | Which clip this is. | | `{number2}` | `03` | The same number to two digits, so ten clips sort 01, 02 … 10 rather than 1, 10, 2. | | `{number3}` | `003` | Three digits, for lists past ninety-nine. |

- The trailing digit is the width, which makes the set read itself.

- A pattern saved under 4.0 using the old names is rewritten automatically. The old names also still expand, so no filename changes as a result of this.

### Curly braces are allowed in filenames

- `{` and `}` may now appear in naming tags and output names. Windows never objected to them; only this program's own rules did.

- A useful side effect: a mistyped variable survives visibly. `{nam}` now produces `{nam}[done].mp4` rather than silently becoming `nam[done].mp4`, so the mistake is one you can see. Characters Windows genuinely rejects — a colon, slash or asterisk — are still removed.

### Requirements

- Windows, .NET 8 desktop runtime
- MPC-HC with the web interface enabled (Options ▸ Player ▸ Web Interface) — the program detects the port itself and says plainly if the interface is off
- ffmpeg and ffprobe are bundled. Your own build dropped beside the executable still takes precedence.

### Verify your download

```powershell
Get-FileHash -Algorithm SHA256 .\MPC-HC.Video.Editor.zip
```

- | File | SHA-256 | | ---- | ------- | | `MPC-HC.Video.Editor.zip` | `BCE30165101A380B48AEBB0EC3AAA6F26BD33597DAFE1205627A2178DC9F0EEC` | | `MPC-HC Video Editor.exe` | `76710B4BA8DD540EC0792AF265F13D8A0F8415FF792C32E23CF34B1C7F6EBB55` | | `ffmpeg.exe` | `4A01142006A4E2359293E072957DCDA7760C2003BBEEDE037B4551F2CFC8406F` | | `ffprobe.exe` | `8B5298DA673B85E628FBC98535A88848E939E16DF72E856FC727E01AA667E243` | | `LICENSE` | `3972DC9744F6499F0F9B2DBF76696F2AE7AD8AF9B23DDE66D6AF86C9DFB36986` |

- FFmpeg is unchanged — still 9.0.1, GPL v3, from [BtbN/FFmpeg-Builds](https://github.com/BtbN/FFmpeg-Builds).

## 4.0 — 2026-08-30

- Nine features, and a new version scheme. Releases are two-part from here — 4.0, 4.1, 4.2 — rather than the three-part 3.0.x numbering that came before.

### Finding clips for you

- **Bookmarks ▸ Find clips automatically…** scans a video and proposes a set of clips. Three ways of looking, for three kinds of material:

- **Silence** — for anything speech-driven, where the gaps are the joins
- **Black frames** — for recordings that fade between segments, and adverts
- **Scene changes** — for edited footage with hard cuts and no audio cue

- Scanning proposes; it never applies. The list comes back with everything ticked and you untick what it got wrong, because detection on real footage is a good first guess and nothing more.

### Playlists that survive a reorganised library

- **Find moved files…** is the counterpart to removing dead entries, and usually the one actually wanted. A file that is not where the playlist says has far more often been moved than deleted. Point it at a folder and it searches for the missing files by name and repoints the playlist at them.

- Entries on a disconnected drive are never touched by it — their files are probably exactly where they always were, and quietly repointing them at a same-named file elsewhere would be worse than doing nothing.

- **`.m3u8` playlists** are now read and written alongside `.pls`, and either can be converted to the other. m3u8 is UTF-8 by definition, which sidesteps the whole class of encoding problem that older `.pls` files carry.

- **Relative playlist entries now resolve against the playlist's own folder** rather than wherever the application happened to be started from. That was simply wrong before — a perfectly good relative playlist read as entirely missing. It also makes a playlist portable: a folder and its `.m3u8` can be copied to a USB stick and still work.

### Knowing what you actually got

- **Output is now checked against what was asked for.** A copied cut can only begin at a keyframe, so a clip can run over a second longer than the range you marked — and nothing used to say so. Now it does:

- > The finished file is 1.1s longer than the marked range (0:04 against 0:03). > Copied cuts can only start at a keyframe, so a clip may begin before its mark. > Turn on Precise in Settings ▸ Encoding ▸ Cut accuracy to cut exactly.

- **Failed jobs explain themselves.** ffmpeg had been reporting its reasons all along; the application was reading that stream only to scrape a progress percentage and throwing the rest away, leaving you with an exit code. The last few lines are now kept and shown, and a GPU encoder is named when one is selected.

### Seeing the audio

- The timeline now draws the video's **waveform** behind the range marks, so where sound actually is can be seen while marking rather than scrubbed for. It renders in the background and simply does not appear for a video with no audio.

### Naming and output

- **Filename templates.** The naming tags cover "put [done] on the end"; a template covers what they cannot:

```
{name}_{index2}_{start}-{end}_{duration}s{suffix}
    → holiday_03_0-01-23-0-01-45_22s[done].mp4
```

- `{name}` `{suffix}` `{index}` `{index2}` `{start}` `{end}` `{duration}` `{date}`, with a live example in Settings. The default reproduces the previous naming exactly, so an untouched installation writes the same filenames it always has — and the result still goes through the project's filename rules, so a template cannot produce a path the filesystem rejects.

- **Export as chapters.** The alternative to cutting: one file with the bookmarks attached as navigable chapter marks, instead of N files on disk. Streams are copied, so it is quick and lossless.

- **Loudness normalisation.** An optional EBU R128 pass so a merge of material recorded at different levels does not jump in volume. Like precise cutting it forces a re-encode, which the setting says outright.

### A silent data-loss bug, fixed

- Bookmark CSVs were read as UTF-8 without checking. A CSV written in the legacy Windows code page — which the AutoHotkey predecessor to this application would have produced — turns every accented character into a replacement character, and the stored path then matches no file on disk. The same defect was fixed for playlists in 3.0.6; the detection is now shared, and both use it.

- If you have CSVs carried over from the older tool, this is the release that reads them correctly.

### Requirements

- Windows, .NET 8 desktop runtime
- MPC-HC with the web interface enabled (Options ▸ Player ▸ Web Interface) — the app detects the port itself and will tell you plainly if the interface is switched off
- ffmpeg and ffprobe are bundled. Your own build dropped beside the executable still takes precedence.

### Verify your download

```powershell
Get-FileHash -Algorithm SHA256 .\MPC-HC.Video.Editor.zip
```

- | File | SHA-256 | | ---- | ------- | | `MPC-HC.Video.Editor.zip` | `BF74619095C5F17CA269B4AAEBF78746F42E2FE8965CF968CAB3388FA66166F2` | | `MPC-HC Video Editor.exe` | `4C42FCF34F3553A26B39631096B0484C5F02E2EBEB77F8399B1880CAD965EB3F` | | `ffmpeg.exe` | `4A01142006A4E2359293E072957DCDA7760C2003BBEEDE037B4551F2CFC8406F` | | `ffprobe.exe` | `8B5298DA673B85E628FBC98535A88848E939E16DF72E856FC727E01AA667E243` | | `LICENSE` | `3972DC9744F6499F0F9B2DBF76696F2AE7AD8AF9B23DDE66D6AF86C9DFB36986` |

- FFmpeg is unchanged from 3.0.6 — still 9.0.1, GPL v3, from [BtbN/FFmpeg-Builds](https://github.com/BtbN/FFmpeg-Builds).

## 3.0.9 — 2026-08-30

- Two bug fixes, both found by going back over what 3.0.7 and 3.0.8 shipped.

### Clip previews could go permanently blank

- Moving through the bookmark list cancels whichever preview is still being rendered — that is deliberate, so the panel never shows a clip you have already moved past. The mistake was what happened next: an interrupted render returned nothing, and that nothing was **cached as the answer**.

- So arrowing quickly past a clip and coming back to it left its two frames blank for the rest of the session, with no way to bring them back short of loading a different video. The faster you moved through the list, the more clips it happened to.

- The cache now keeps only real answers. A frame that genuinely cannot be read — one past the end of the video, say — is still remembered as unavailable, since re-running ffmpeg to fail again helps nobody. An interruption is no longer mistaken for that.

### Failed encodes now say why

- A job that failed reported only its exit code:

```
FFmpeg exited with code -1313558101
```

- ffmpeg had explained itself on its error stream the whole time; the application was reading that stream purely to scrape a progress percentage out of it and discarding everything else.

- Failures now carry the last few lines of what ffmpeg actually said. When a GPU encoder is selected they also name it, because a hardware encoder that passed the settings check can still fail on real content — an unusual resolution, a bit depth the chip does not handle — and it is then the first thing worth changing:

```
FFmpeg exited with code -1313558101.

[enc:h264_amf] Could not open encoder before EOF
Nothing was written into output file...
Conversion failed!

This used the AMD (AMF) encoder. If it keeps failing, switch the H.264
encoder back to Software in Settings ▸ Encoding — it works on any machine.
```

- This applies to every failure, not only hardware encoding.

### Smaller fixes

- The preview's cancellation token was being disposed while a render still held it, which turned an ordinary cancellation into a different error inside a catch that ignored everything — one of the routes into the blank-frame bug above.

- A previewed clip that had its closing timestamp removed left its old frames on screen. It now clears.

### Unchanged

- Everything from 3.0.7 and 3.0.8 stands: cut accuracy, hardware encoding, web interface detection, the clip preview, and the playlist work. FFmpeg is still 9.0.1, GPL v3, from [BtbN/FFmpeg-Builds](https://github.com/BtbN/FFmpeg-Builds).

### Requirements

- Windows, .NET 8 desktop runtime
- MPC-HC with the web interface enabled (Options ▸ Player ▸ Web Interface) — the app will tell you plainly if it is not
- ffmpeg and ffprobe are bundled. Your own build dropped beside the executable still takes precedence.

### Verify your download

```powershell
Get-FileHash -Algorithm SHA256 .\MPC-HC.Video.Editor.zip
```

- | File | SHA-256 | | ---- | ------- | | `MPC-HC.Video.Editor.zip` | `D1C1F027BBE7177256A89F1C71ED96984F5BD43E1630F4F7D7133BEA88AD729A` | | `MPC-HC Video Editor.exe` | `6474936B5E6BD46E7973EB0A7410957A699B4CB118194F67B16F16033D4D9465` | | `ffmpeg.exe` | `4A01142006A4E2359293E072957DCDA7760C2003BBEEDE037B4551F2CFC8406F` | | `ffprobe.exe` | `8B5298DA673B85E628FBC98535A88848E939E16DF72E856FC727E01AA667E243` | | `LICENSE` | `3972DC9744F6499F0F9B2DBF76696F2AE7AD8AF9B23DDE66D6AF86C9DFB36986` |

## 3.0.8 — 2026-08-24

- Fixes the clip preview added in 3.0.7, which was effectively invisible.

### The clip preview now actually appears

- 3.0.7 introduced a panel showing the first and last frame of a clip. It only ever drew once a bookmark row had been *selected* — and nothing anywhere said so. With bookmarks in the list and none clicked, the entire feature was hidden with no hint that it existed.

- The natural thing to click on a row made it worse: clicking a timestamp seeks the player, so the most obvious gesture revealed nothing.

- **It no longer needs a selection.** With a video open and at least one complete bookmark, the panel is simply there. Clicking a row still switches which clip it shows.

- Three related gaps closed at the same time. The panel now updates when bookmarks are added or removed, when a bookmark gains its closing timestamp, and when the loaded video changes — previously only changing the selection redrew it, so it could sit showing a clip from a video no longer open.

### Smaller changes

- The heading reads **CLIP [1]** rather than "SELECTED CLIP". With nothing selected the panel shows the first clip, and calling that "selected" would be a small untruth printed every time the app opened.

- Underneath it now says what the clip will run to, and — when there is more than one — that clicking a row previews another. The interaction is stated instead of assumed.

### Unchanged

- Everything else from 3.0.7 stands: cut accuracy, hardware encoding, web interface detection, and the playlist work. FFmpeg is still 9.0.1, GPL v3, from [BtbN/FFmpeg-Builds](https://github.com/BtbN/FFmpeg-Builds).

### Requirements

- Windows, .NET 8 desktop runtime
- MPC-HC with the web interface enabled (Options ▸ Player ▸ Web Interface) — the app will tell you plainly if it is not
- ffmpeg and ffprobe are bundled. Your own build dropped beside the executable still takes precedence.

### Verify your download

```powershell
Get-FileHash -Algorithm SHA256 .\MPC-HC.Video.Editor.zip
```

- | File | SHA-256 | | ---- | ------- | | `MPC-HC.Video.Editor.zip` | `4DCB6C4FE24B109F53F2B6F048B64AAE2BD7BD87378B5324D8C8D85DC0589F86` | | `MPC-HC Video Editor.exe` | `9ECD2CCB413BD0CB3F086DDB5534044530B66AC62D5AD55356A550F235088298` | | `ffmpeg.exe` | `4A01142006A4E2359293E072957DCDA7760C2003BBEEDE037B4551F2CFC8406F` | | `ffprobe.exe` | `8B5298DA673B85E628FBC98535A88848E939E16DF72E856FC727E01AA667E243` | | `LICENSE` | `3972DC9744F6499F0F9B2DBF76696F2AE7AD8AF9B23DDE66D6AF86C9DFB36986` |

## 3.0.7 — 2026-08-23

- Four features, and a fix for a cut that has quietly been wrong all along.

### Cuts can now land where you actually marked them

- A copied cut cannot begin anywhere but a keyframe. Every version until now asked ffmpeg for your timestamp, got back the keyframe at or before it, and wrote that — with nothing in the output to say so. On a file with keyframes five seconds apart, marking 3.00s produced a clip starting at 0.

- Measured on exactly that file, cutting 3s to 6s:

- | | asked for | got | drift | | --- | --- | --- | --- | | Fast (copy) | 3.00s | 4.16s | **+1.16s** | | Precise (re-encode) | 3.00s | 3.02s | +0.02s |

- **Settings ▸ Encoding ▸ Cut accuracy** now chooses. Fast stays the default, because a copied cut is lossless and instant; Precise re-encodes each segment to land on the marked frame, costing time and one generation of quality on cuts that would otherwise have been a straight copy. Neither is simply better, which is why it is a choice rather than a change.

### Hardware encoding

- Where video is re-encoded, it can now go through the GPU: **NVIDIA NVENC**, **Intel Quick Sync** or **AMD AMF**. Typically several times faster than x264, and larger at the same visual quality — the usual trade.

- Being listed is not the same as working. The GPU encoders are compiled into ffmpeg whether or not the hardware exists, so `h264_nvenc` is advertised on a machine with no NVIDIA card in it. Opening the Settings dialog therefore runs a real test encode for each one and greys out whatever this machine cannot do. A saved choice that stops working — a driver change, a moved disk — falls back to software rather than failing the next job.

- Only affects MP4, MKV and MOV. The legacy containers carry their own codecs and have nothing to choose.

### MPC-HC's web interface port is detected

- The port used to be a number you had to keep in step by hand with one buried in MPC-HC's options, and getting it wrong meant seeking silently fell back to clicking the seek bar. The player already knows the answer, so the app now reads it — from a portable install's `.ini` first, then the registry.

- More useful than the port itself: when seeking fails, the message now says what is actually wrong. "MPC-HC's own settings say the Web Interface is turned off — that is the problem, not the port" is a far better answer than generic advice about a number that was never wrong.

- On by default. The manual port stays as the fallback for an install this cannot read, so nothing gets worse.

### In and out thumbnails

- Selecting a clip now shows its first and last frame in the side panel, with the saved length underneath. A pair of timestamps tells you a range is three minutes long; it does not tell you whether it is the right three minutes.

- Only the selected clip is rendered, not every row — two frames per selection is free, two per bookmark across a long list is hundreds of ffmpeg launches for pictures that are mostly scrolled past. Frames are piped straight out of ffmpeg rather than written to disk, so nothing accumulates beside the executable.

### Requirements

- Windows, .NET 8 desktop runtime
- MPC-HC with the web interface enabled (Options ▸ Player ▸ Web Interface) — the app will now tell you plainly if it is not
- ffmpeg and ffprobe are bundled. Your own build dropped beside the executable still takes precedence.

### Verify your download

```powershell
Get-FileHash -Algorithm SHA256 .\MPC-HC.Video.Editor.zip
```

- | File | SHA-256 | | ---- | ------- | | `MPC-HC.Video.Editor.zip` | `C3D5452219C0ACEF02E46F55D33899E61291186CF61D10730CA9CA588667C409` | | `MPC-HC Video Editor.exe` | `9328798996ED987C865FCF6033C0435C0FF7754058BB757879B64E36B719C486` | | `ffmpeg.exe` | `4A01142006A4E2359293E072957DCDA7760C2003BBEEDE037B4551F2CFC8406F` | | `ffprobe.exe` | `8B5298DA673B85E628FBC98535A88848E939E16DF72E856FC727E01AA667E243` | | `LICENSE` | `3972DC9744F6499F0F9B2DBF76696F2AE7AD8AF9B23DDE66D6AF86C9DFB36986` |

- FFmpeg is unchanged from 3.0.6 — still 9.0.1, GPL v3, from [BtbN/FFmpeg-Builds](https://github.com/BtbN/FFmpeg-Builds).

## 3.0.6 — 2026-08-21

- A security release. No behaviour in the application itself has changed — what changed is the FFmpeg it hands your files to.

### The reason to update

- **Every release up to 3.0.5 bundled FFmpeg 4.2.1, built in August 2019.** That is five major versions behind, on a branch long past upstream end-of-life, and it carries publicly documented vulnerabilities that upstream has since fixed.

- This matters more here than it would in most applications, because feeding media files to FFmpeg is the whole point of this one. Opening a maliciously crafted video is the most plausible route to code execution against anyone running it. If your files come from your own camera and recordings the practical risk was small; if any of them came from elsewhere, it was not.

- **3.0.6 bundles FFmpeg 9.0.1** — a GPL v3 build from [BtbN/FFmpeg-Builds](https://github.com/BtbN/FFmpeg-Builds), one of the two Windows sources linked from ffmpeg.org.

### Verified, not assumed

- Jumping five major versions can break command-line arguments, so every FFmpeg invocation the application makes was run against the new build before this was packaged: trimming by stream copy, trimming with the flip and speed filters re-encoded, the `atempo` chain used for slow motion, all seven output formats (MP4, MKV, MOV, AVI, WMV, MPG, WEBM), MP3 audio extraction, the segment-and- concat path behind bulk merge, and the `ffprobe` duration query. All pass.

### Also in this release

- **The LICENSE file is now in the archive.** It never had been, so Help ▸ About told every user "no licence file is installed alongside this application" — and shipping GPL-licensed binaries without the licence text is a compliance gap on its own. Both are fixed.

- **The About dialog's third-party section was wrong about FFmpeg.** It said FFmpeg "is not distributed as part of this application", which has never been true of these releases — it is in the archive. It now names the bundled version, states the GPL v3 terms it is licensed under, and points at the corresponding source.

### Two things worth knowing

- **The download is larger — 169 MB, up from 108 MB.** That is entirely the new FFmpeg binaries, which are roughly twice the size of the 2019 pair. Nothing was added to the application itself.

- **The binaries are still unsigned**, so SmartScreen will warn on first run. There is no way around that without a code-signing certificate. Verify what you downloaded instead:

```powershell
Get-FileHash -Algorithm SHA256 .\MPC-HC.Video.Editor.zip
```

- | File | SHA-256 | | ---- | ------- | | `MPC-HC.Video.Editor.zip` | `32F501568406B287B9CA481CF6CC9932CB47D52DD2449DD810D65527251C1870` | | `MPC-HC Video Editor.exe` | `AB727A11674D6CA1F072936CB101D0B2C8241EC8E48E4250889965EC10A261A0` | | `ffmpeg.exe` | `4A01142006A4E2359293E072957DCDA7760C2003BBEEDE037B4551F2CFC8406F` | | `ffprobe.exe` | `8B5298DA673B85E628FBC98535A88848E939E16DF72E856FC727E01AA667E243` | | `LICENSE` | `3972DC9744F6499F0F9B2DBF76696F2AE7AD8AF9B23DDE66D6AF86C9DFB36986` |

- These are the first release hashes this project has published. Earlier releases have none, and there is no way to verify those downloads after the fact.

### Requirements

- Windows, .NET 8 desktop runtime
- MPC-HC with the web interface enabled (Options ▸ Player ▸ Web Interface)
- ffmpeg and ffprobe are bundled — no separate install. Your own build dropped beside the executable still takes precedence if you prefer one.

## 3.0.5 — 2026-08-21

- Two deletion fixes, found by auditing every path that removes a file after "delete files to the Recycle Bin" became a setting in 3.0.3.

### Fixes

- **The confirmations said the wrong thing when the Recycle Bin was turned off.** Every prompt and status message claimed the files were going to the Recycle Bin, whatever the setting said — so with it off, the dialog being clicked Yes on described a permanent, unrecoverable deletion as recoverable. That covered the source-video prompt, the bookmark-file prompt, the converted-images prompt and the status line afterwards.

- The wording now follows the setting: with the bin off it says the files will be deleted permanently and cannot be recovered, and reports "Permanently deleted…" when it is done.

- **Deleting a playlist destroyed it outright.** It called the unrecoverable delete directly rather than going through the Recycle Bin, so a `.pls` was gone for good even in the default state where every other deletion in the app can be undone. Playlists now follow the same setting as everything else.

### Unchanged, and confirmed working

- The cleanup settings themselves were audited at the same time and behave as described: **Keep** never deletes and never asks, **Ask** prompts and honours No, and **Delete without asking** deletes silently. The prompt defaults to No, so Enter cannot delete anything.

- Converting images still always asks, whatever the cleanup setting says. That is deliberate — those settings apply to merge, trim, split and video convert.

### Requirements

- Windows, .NET 8 desktop runtime
- MPC-HC with the web interface enabled (Options ▸ Player ▸ Web Interface)
- ffmpeg and ffprobe, beside the executable or on PATH. WEBM output needs a build with libvpx and libopus.

## 3.0.4 — 2026-08-18

- Two fixes about output landing where it was asked to, and a prompt staying reachable while it waits. Plus one thing from 3.0.3 taken back out.

### Fixes

- **Split made a folder nobody asked for.** It created `<name>_clips` next to the save-to folder and wrote the clips in there, so Split was the one operation whose output did not appear where the destination pointed — and a single cut left a folder behind for one file. Clips now go straight into **Save to**, like merge, convert and strip audio.

- Nothing is lost with the folder gone: the clips are named `[done]`, `[done2]`, `[done3]`… by index, so they cannot collide with each other, and a name already taken by something else goes through the same collision prompt as everything else.

- **The view switched away from a prompt that was waiting for an answer.** A modal loop still pumps messages, so the poll behind "switch views automatically" kept running while "this file already exists" sat unanswered. Clicking the player was then enough to drop to the overlay, which hid the main window and the prompt with it — and because the prompt is modal, the app accepted input nowhere and there was nothing left on screen to answer. Alt+Tab was the only way back.

- The view no longer moves while any prompt is open: whatever is showing when one appears is what stays until it is answered, whether the overlay was pinned or the setting was on. The "file exists" and rename dialogs are also on top of other windows now, so clicking away no longer buries them.

### Changed

- **Merge is called Merge again.** 3.0.3 renamed it to Extract with a single cut. That was wrong: the command is always available precisely because it does more than join cuts — with no video and no bookmarks it opens a file picker and joins whatever is chosen — so a label driven by the current cut count described only one of the three things it does. With a single pair, Split and Merge both process it, and either is a reasonable thing to click.

### Requirements

- Windows, .NET 8 desktop runtime
- MPC-HC with the web interface enabled (Options ▸ Player ▸ Web Interface)
- ffmpeg and ffprobe, beside the executable or on PATH. WEBM output needs a build with libvpx and libopus.

## 3.0.3 — 2026-08-16

- A small release about telling the truth: one button that described an operation the user was not performing, and one panel that promised something the user could not change.

### Changed

- **A single cut is an extract, not a merge.** Joining implies more than one thing to join. With one cut there is nothing to join — the result is that span written out on its own — so calling it a merge described an operation nobody was performing. The label now follows the number of cuts the command would actually act on:

- | Cuts | Label | | --- | --- | | None loaded, or two or more with none checked | Merge | | Two or more checked | Merge selected | | One, unchecked | Extract | | One, checked | Extract selected |

- The toolbar button and the Actions menu item read from the same value, so they cannot disagree, and it updates as cuts are checked. Progress and failure messages use the same wording, so the button pressed and the status reported agree. The colour does not change with the label: it is the same command doing the same kind of work, and a button that changed colour as cuts were ticked would read as a different one.

- **Split is deliberately untouched.** With a single pair it does much the same thing as an extract. Both work, and either is a reasonable thing to click.

### New

- **Delete files to the Recycle Bin** (Settings ▸ Cleanup) is now a setting rather than a fixed behaviour. It is on by default, so nothing changes unless you turn it off, and it covers every deletion the app makes — including the post-operation cleanup that runs without asking, which is what made those settings safe to turn on in the first place.

- Turned off, deletions are permanent and unrecoverable. The warning saying so appears only while the setting is off, because a standing warning next to a setting that is behaving itself is noise, and noise is what stops it being read in the state that matters.

- On a volume with no Recycle Bin — a network share, most removable media — files are removed outright either way, because leaving one behind would be worse than the deletion that was asked for.

### Also

- The README now opens with a screenshot of the main window, and shows the overlay, the naming tags menu and Settings.

### Requirements

- Windows, .NET 8 desktop runtime
- MPC-HC with the web interface enabled (Options ▸ Player ▸ Web Interface)
- ffmpeg and ffprobe, beside the executable or on PATH. WEBM output needs a build with libvpx and libopus.

## 3.0.2 — 2026-08-14

- A maintenance release about things firing when they should not. The two global hotkeys and the minimal overlay all acted on state that was true somewhere in the program rather than in front of the user, and the fixes tie each one to what is actually on screen.

### Fixes

- **The window said 3.0 in the 3.0.1 release.** The caption was a literal in the XAML, so bumping the version in the project file had no reason to make anyone think of it. Both the title bar and Help ▸ About now read the version out of the assembly, which the project file is the only source of.

- **The overlay showed nine bookmarks and scrolled the rest.** Its height was fixed at 300 px — deliberately, because a layered window that resizes does not reliably repaint what it vacated, which is what caused the 3.0.1 flicker. The window is now a fixed pane the height of the screen and the card inside it grows with the list, so the layer is still never resized. Roughly forty pairs are visible on a 1080p display before anything scrolls.

- **The timestamp hotkey fired everywhere.** The hook is global, which it has to be to work while the player has focus, but nothing checked where the press came from — with the default middle-mouse binding, opening a link in a new tab set a bookmark against whatever MPC-HC last had loaded. It now fires only when MPC-HC is the active window and has a video open, and the press is passed along untouched everywhere else.

- **The X restore key fired on any "x" typed anywhere.** It was armed for as long as the overlay existed. It is now armed only while the overlay is actually visible, so it belongs to something the user can see.

- **"Switch views automatically" turned itself off.** The automatic switch shared a code path with the menu item, and that path ends manual control by clearing the setting — so the first time the setting did its job, it disabled and saved itself. The Settings checkbox came back unchecked and the next launch started with switching off. The two paths are now separate, and nothing writes the setting except the user.

- **View ▸ Minimal gave way on the next click.** A hand-picked overlay followed focus like an automatic one, so clicking anything else took it back down. It is now pinned until X or View ▸ Full, and picking it makes MPC-HC the active window so the result does not depend on which window happened to be behind this one.

### New

- **One instance, or several** (File ▸ Settings ▸ General). One instance is the default: starting the program again brings the running copy to the front instead of opening a second one. Scoped to the install folder, so a portable copy elsewhere is unaffected either way.

### Changed

- **"Naming style" is now "naming tag"** throughout the Options menu. The line showing the active tag is no longer greyed out.
- **View ▸ Minimal is disabled when there are no bookmarks.** The overlay is the bookmark list; an empty one has nothing to show. It also comes down on its own if the list empties — opening another video in the player clears it — and returns with the next bookmark.
- **A pinned overlay hides while another application is in front** and comes back for this window or MPC-HC. It is not closed and keeps its place.

### Requirements

- Windows, .NET 8 desktop runtime
- MPC-HC with the web interface enabled (Options ▸ Player ▸ Web Interface)
- ffmpeg and ffprobe, beside the executable or on PATH. WEBM output needs a build with libvpx and libopus.

## 3.0.1 — 2026-08-12

- A maintenance release. Several long-standing bugs turned out to share one cause — the same fact stored in more than one place — and the fixes remove the duplicate state rather than adding more of it.

### New

- **Settings** (File ▸ Settings), in five tabs: output container, encoding quality, filename-collision policy, post-operation cleanup, MPC-HC web interface port, ffmpeg folder, poll rate, recent-history size, toasts, overlay corner and opacity.
- **Run in system tray** — minimises to the notification area and survives the window being closed; exit from the tray menu. The alternative, **run as application**, is the default and behaves conventionally.
- **Output formats** beyond MP4: MKV, MOV, AVI, WMV, MPG, MPEG, WEBM. MP4, MKV and MOV are written by copying the cut segments, so they are lossless and fast; the others cannot hold H.264 and re-encode with codecs appropriate to the container.
- **Post-operation cleanup** — optionally delete the source video and/or the bookmark file after merge, trim, split or convert, either on confirmation or silently. Everything goes to the Recycle Bin.
- **Help menu** with About, licensing and links to the repository.
- **Application and tray icons.**
- **Spaces in filenames are converted to dashes automatically** instead of prompting. Other disallowed characters still ask.

### Changed

- View switching follows **focus** rather than whether the player is fullscreen. Leaving MPC-HC always restores the main window; returning to it drops to the overlay when following is on.
- **View ▸ Minimal is never disabled,** and choosing it turns automatic switching off.
- "Switch views automatically" moved from Options to the View menu.
- Deletions throughout go to the Recycle Bin rather than being unrecoverable.

### Requirements

- Windows, .NET 8 desktop runtime
- MPC-HC with the web interface enabled (Options ▸ Player ▸ Web Interface)
- ffmpeg and ffprobe, beside the executable or on PATH. WEBM output needs a build with libvpx and libopus.

## 3.0 — 2026-08-02

- Completely converted to WPF via XAML and C#.

## 2.1 — 2025-08-26

- -lots of improvements -fixed hotkey assignment -fixed stats -added drag files window -fixed portability -add shortcuts to save to menu -fixed conditional bugs -hardcoded paths removed and dynamically created -added history menu -added timeline -added ability to flip timestamp vertically -added slider to increase or decrease play speed -added play all or play selected

- -abandoned MPC-BE due to lack of development

## 2 — 2025-07-31

- Complete rewrite of menus

- I. Save to -select save to directory

- II.Load/Unload -shows currently loaded video and/or csv file -load new video or csv file -edit csv file -delete video or csv file

- III. Actions -Merge or split -clear bookmarks -erase last bookmark -reset program

- IV. Hotkey -change hotkey being used

- V. Stats -on or off for basic stats -advanced for variables (for troubleshooting)

## 1.6 — 2025-06-27

- -fixed bookmark seeking problem

## 1.5 — 2025-06-21

- -you can now change the hotkey

## 1.4 — 2025-06-20

- -fixed a lot of bugs

## 1.3 — 2025-06-17

- -UI rework -Removed images and replaced with buttons -Added load csv feature -You can now merge and split from the main program -Error checks -The executable is now portable

## 1.2 — 2025-06-10

- -Split function now works correctly. Before, it was only processing 2 bookmark timestamps

## 1.1 — 2025-06-09

- This is the latest release.

### What's Changed
- Added 32bit version
- Added native ffmpeg processing
- Images now show in the gui
- Fix csv line deletion by @gouryellas in https://github.com/gouryellas/mpcbe-video-editor/pull/1
- Fix fuchsia spelling by @gouryellas in https://github.com/gouryellas/mpcbe-video-editor/pull/3
- Fix fuchsia spelling by @gouryellas in https://github.com/gouryellas/mpcbe-video-editor/pull/4

### New Contributors
- @gouryellas made their first contribution in https://github.com/gouryellas/mpcbe-video-editor/pull/1

- **Full Changelog**: https://github.com/gouryellas/mpcbe-video-editor/compare/release...v1.1

## release — 2025-06-08

- **Full Changelog**: https://github.com/gouryellas/mpcbe-video-editor/commits/release
