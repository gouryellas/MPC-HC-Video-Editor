# MPC-HC Video Editor (C# / WPF)

<p align="center"><img src="Assets/logo.png" alt="MVE" width="200"></p>

Create bookmarks with a hotkey to merge, split, and trim video clips via ffmpeg with MPC-HC. There are also a lot of other features that allow you to: bulk image convert, bulk image resize, strip audio, convert video, crop video, apply video effects (flip, mute, fade, rotate), extract frame thumbnails, extract image metadata, export clips as gifs, create custom anchors for filename tags, create explorer shortcuts, create and manage playlists, customize the look and view, and more.

![The main window showing three bookmarked clips as rows, with their times, lengths and speed sliders, the waveform timeline with a bracket at each end of every cut, and the first and last frame of the selected clip](Assets/screenshots/view-list.png)

**Portable.** No installer. Some backup data is stored in appdata, but it still retains its portability.

## Multiple Views

![The storyboard in the cards layout: each cut is a card with its frame, the checkbox and number over the top left of the picture, the chapter name along the foot of it, and the times and length centred underneath](Assets/screenshots/view-cards.png)

![The storyboard in the contact sheet layout: a row of small frames, each with the checkbox and number over its top left and the times and length along a bar at its foot](Assets/screenshots/view-contact-sheet.png)

![The storyboard in the detail layout: one cut per full-width row, the frame on the left and the number, name, times, length and speed beside it](Assets/screenshots/view-detail.png)

![The storyboard in the filmstrip layout: each cut shows its opening and closing frame next to each other, with the start time, length and end time underneath](Assets/screenshots/view-filmstrip.png)

## Customization

Right-click the seekbar for its own settings, or Options for the rest.

![The Customize seekbar window, offering four ways to mark a cut — classic brackets, plain lines, chevrons, or nothing — with a transparency slider and a switch for the filled position bar](Assets/screenshots/customize-seekbar.png)

![The Seekbar colors window, with a live preview at the top and separate palettes for the position bar, the border, the background, the brackets and the waveform, each able to follow the theme or take a colour of its own](Assets/screenshots/seekbar-colors.png)

![The Save thumbnails window, asking how often to take a frame and how wide each should be, whether to write one image per frame or a single tiled sheet, and where they should go](Assets/screenshots/save-thumbnails.png)

![The Convert images window, with the output format chosen from a list and a choice of writing each file beside its original, into the Save to folder, or somewhere else](Assets/screenshots/convert-images.png)

![The Export as animation window, offering GIF or animated WebP with the trade-offs of each spelled out, and fields for the frame rate and width](Assets/screenshots/export-animation.png)

![The Resize images window, with standard sizes from a list, a choice of fitting inside, filling and cropping, or stretching, and where the resized copies should be written](Assets/screenshots/resize-images.png)

![The Scan for cuts window, with silence, black frames and scene changes as the three things that can mark the end of a clip, the thresholds for each, and a list of proposed clips to review before accepting](Assets/screenshots/find-clips.png)

![The Crop window, showing a frame from the video with nothing drawn on it yet and the instruction to drag a box on the picture, a row of shape presets, and a choice between cropping the marked cuts or the whole video](Assets/screenshots/crop.png)

**Upgrading keeps your settings.** A spare `settings.json` is kept in
`%APPDATA%\MPC-HC Video Editor\` and restored only when none is found beside the
executable — so delete-and-unpack upgrades carry over, and a deliberately fresh
copy stays fresh.

<details>
<summary>Settings, in five tabs</summary>

<table>
<tr>
<td width="50%"><strong>General</strong> — theme, run mode, instances</td>
<td width="50%"><strong>Output</strong> — format, quality, encoder</td>
</tr>
<tr>
<td><img src="Assets/screenshots/settings-general.png" alt="The Settings window on the General tab, showing the color theme picker, the run mode and the single-instance options" width="100%"></td>
<td><img src="Assets/screenshots/settings-output.png" alt="The Output tab, showing the default output format, the encoding quality and the H.264 encoder choice with unavailable GPU encoders grayed out" width="100%"></td>
</tr>
<tr>
<td><strong>Cleanup</strong> — what is deleted, and where it goes</td>
<td><strong>Player</strong> — web interface port, polling, ffmpeg</td>
</tr>
<tr>
<td><img src="Assets/screenshots/settings-cleanup.png" alt="The Cleanup tab, showing what happens to the original video and the bookmarks file after an operation, and the Recycle Bin option" width="100%"></td>
<td><img src="Assets/screenshots/settings-player.png" alt="The Player tab, showing the detected MPC-HC web interface port, the polling speed and the ffmpeg folder" width="100%"></td>
</tr>
<tr>
<td colspan="2" align="center"><strong>Overlay</strong> — corner, opacity, click-to-seek</td>
</tr>
<tr>
<td colspan="2" align="center"><img src="Assets/screenshots/settings-overlay.png" alt="The Overlay tab, showing the corner the compact overlay parks in and its background opacity" width="50%"></td>
</tr>
</table>

</details>

---

## Bookmarks

A bookmark is a **timestamp pair**: the hotkey opens it, the hotkey closes it.

- **Cuts may touch but not overlap**, and new marks must land at or after the
  latest one. Refused marks are not added, and the reason appears over the
  player.
- Closing a bookmark at or before its start is refused; the bookmark stays open.
- **Undo last bookmark** removes the last single timestamp, not the pair.
- **Enter time / range…** accepts `90`, `1:30`, `1:02:03`, `22s`, `5m`, `1h`,
  `1m30s` and ranges like `1:00 - 2:30`.
- **Edit bookmarks** opens the CSV in a text editor; changes are picked up when
  the window regains focus.
- **Scan for cuts…** scans for silence, black frames or scene changes
  and proposes cuts to review before accepting — appended or replacing.
- **Play all** / **Play selected** seek the player to each cut and play it for
  its effective duration.
- Per cut: a **speed slider** (0.25x–2.0x), and **Flip**, **Rotate**, **Mute**,
  **Fade** and **Crop**. These act on the checked cuts, or on the highlighted row
  when nothing is checked.
- Only complete pairs can be checked. Clicking a timestamp seeks the player.
- **Ctrl+Z / Ctrl+Y** step back and forward through changes to the cuts.
- The **timeline** carries position, length, a bracket at each end of every cut
  and the audio waveform behind it. Click to seek.

The CSV format matches the original AutoHotkey version, is saved next to the
video, and is never created empty.

---

## Actions

Output filenames use the active **naming tag** — `vacation.mp4` →
`vacation[done].mp4` — or the source's own name when the tag is **None**.

**Video**

- **Merge** — the checked cuts, all of them, or files you pick when nothing is
  loaded. Always available.
- **Split** — one clip per cut.
- **Crop** — drag the area to keep on a still frame, per cut or across a whole
  video.
- **Convert files to MP4…** — several files at once.
- **Export as chapters…** — one file with the cuts as chapter marks, streams
  copied, so it is lossless.

**Audio**

- **Strip audio…** — an MP3, a silent copy of the video, or both. The silent
  copy is not re-encoded. The source is untouched.

**Images**

- **Save the frame on screen…** — the current frame as a full-size PNG.
- **Save thumbnails…** — a frame every *n* seconds, as separate images or one
  tiled sheet.
- **Export cuts as GIF / WebP…** — each checked cut as an animation, no sound.
  Frame rate and width decide the size.
- **Image conversion** — PNG, JPG, JPEG, BMP, GIF, TIFF, ICO.
- **Resize images…** — standard sizes from VGA to 4K, or your own, fitting
  inside, filling and cropping, or stretching.
- **Image info…** — dimensions, camera and EXIF, plus PNG text chunks and the
  generation settings written by ComfyUI and A1111.

Convert and Resize ask where to write: beside each source, the Save-to folder,
or somewhere else.

### Toolbar

Drag a button onto another to reorder the row; the order is saved. A press that
does not move is still a click.

**Right-click the toolbar ▸ Customize toolbar** (also under Options) opens the
editor: your toolbar above, every available action below, drag between the two
or double-click. Spacers, an expander and row breaks sit with the actions. A
button added in a later release waits there rather than appearing on a toolbar
you have arranged. **Reset toolbar order** puts it back.

**Change button colors** paints each family — video, audio, images, everything
else — a colour, the theme's colour, or none. **Small button icons** (Settings)
drops the labels.

### Filenames

When a name is taken you are asked to **Overwrite**, **Increment** (`[done]` →
`[done2]`) or **Rename**, repeating until the name is free.

Names the app writes allow letters, numbers, dash, underscore, brackets and
parentheses — no spaces, no doubled punctuation. A name that breaks the rules
prompts for a replacement with a correction pre-filled. Source files are never
renamed.

---

## Views

**View ▸ Switch views automatically** is the whole of the view control, and is on
by default. A compact overlay lists the bookmarks while MPC-HC is active; the
full window returns the moment it is not. The overlay never takes focus and
appears on the monitor the player is on.

- **X** over the overlay brings the full window back, leaving a fullscreen
  player's fullscreen on the way.
- With the setting off the full window stays up, and the hotkey confirms each
  press with a brief on-screen message.
- **Settings ▸ Overlay** sets the corner, the background opacity, and whether
  the overlay's timestamps seek — on, at the cost of that corner no longer
  passing clicks through to the video.

Running an action shows progress above the status bar: file *n* of *m*, the
current step, percentage, elapsed and remaining. Routine messages go to the
status bar; dialogs are kept for destructive actions, real failures and input.

---

## Files and folders

- **Save to:** — where merge, split, convert and strip audio write. Follows the
  video's folder until you pick one, then holds for the session.
- **Quick save:** — used only by the one-click save button on each row.
  Persisted.
- **Quick save shortcuts** (File menu) point Quick save at a folder.
  **Shortcuts** (own menu) just open folders in Explorer.
- **History** lists recently played videos. Dead entries are pruned when clicked.
- **Drag and drop** a video, `.csv` or playlist onto the window to load it. With
  nothing loaded, each heading in the right-hand panel offers an open icon.

---

## Playlists

Reads `.pls`, `.m3u8` and `.m3u` from the playlist folder.

- **Load playlist…** starts the first entry that still exists and hands the whole
  list to the player. An empty playlist, or one whose files are all gone, is not
  loaded.
- Each playlist expands to its videos. **View playlist content** opens the file
  in a text editor.
- **Add current video** appends to the loaded playlist, or asks which one.
- **Find moved files…** repoints entries whose files have moved; **Remove missing
  entries** drops the ones that are gone. Entries on a disconnected drive are
  left alone either way.

---

## Options and settings

- **Naming tags** — click one to activate it (marked ✓), or **None**. Add and
  manage them here; **Settings ▸ Output** also has a filename template with
  variables for the clip number, start, end and length.
- **Themes** — five, light and dark, applied as you pick them.
- **Output** — container, quality, H.264 encoder, fade length, and loudness
  evening-out across clips (EBU R128).
- **Cleanup** — what happens to the source and the bookmark file afterwards.
  **Delete files to the Recycle Bin** is on by default and covers every deletion,
  including the automatic ones. Volumes without a Recycle Bin delete outright.
- **Player** — web interface port, polling speed, ffmpeg folder.
- **Running** — as an ordinary window or in the notification area, and whether a
  second launch opens a second copy.
- **Help ▸ Check for updates** asks GitHub whether a newer release exists.

## Hotkey

**Set timestamp hotkey…** captures any key, combination, or the middle and side
mouse buttons. The binding persists.

It is global, but fires only while **MPC-HC or this program is active, with a
video open** — everywhere else the press is passed along untouched, so the
default middle-mouse binding still opens links in a browser. With the seekbar's
hover preview, a cut can be marked from either window without switching focus
between them.

---

## Requirements

- **.NET 8 SDK** to build. The published build is self-contained.
- **`ffmpeg.exe` and `ffprobe.exe`** beside the executable, in an `ffmpeg\`
  subfolder, or on `PATH` — searched in that order, and validated by running
  them rather than merely found.
- **MPC-HC** for anything that reads or drives the player.

## Building

See [BUILD.md](BUILD.md) for the portable publish command.

```bash
dotnet restore
dotnet build -c Release
```

## Project layout

```
Dialogs/     Settings, CustomizeToolbar, ButtonColors, Crop, Thumbnails,
             ResizeImages, ConvertImages, ImageInfo, ExportAnimation,
             StripAudio, DetectBookmarks, RenameFile, Conflict, and the rest
Helpers/     Converters, FileNameRules, NameTemplate, PortablePaths,
             RecycleBin, TimelineRanges, ToolbarRowPanel, UndoHistory
Models/      Bookmark, EditSession, HotkeyBinding, ShortcutEntry, SuffixEntry,
             ToolbarItem, WindowPlacement
Services/    Bookmark, BookmarkDetection, ButtonColors, Ffmpeg, Hotkey,
             ImageConversion, ImageMetadata, MpcHc, Playlist, ScreenFit,
             Settings, Theme, Thumbnail, Toast, TrayIcon, UpdateCheck
ViewModels/  MainViewModel — all commands and menu enablement
Views/       MainWindow, MinimalWindow, ToastWindow
```
