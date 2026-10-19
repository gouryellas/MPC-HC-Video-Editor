# Building

Produces the portable build in `publish\portable`: one self-contained .exe
that needs no installer and no .NET runtime on the target machine.

```
dotnet publish -c Release -r win-x64 --self-contained true ^
  -p:PublishSingleFile=true ^
  -p:IncludeNativeLibrariesForSelfExtract=true ^
  -p:EnableCompressionInSingleFile=true ^
  -p:DebugType=none ^
  -o publish\portable
```

Then copy a working `ffmpeg.exe` and `ffprobe.exe` next to the produced .exe.
The app looks for them beside itself, then in an `ffmpeg\` subfolder, and only
then on PATH — so a bundled pair makes the folder self-sufficient rather than
depending on what the host machine happens to have installed.

`publish\portable\settings.json` is the portable build's own configuration and
is meant to stay there. Republishing does not touch it — `dotnet publish` only
overwrites what it produces, the same reason the two ffmpeg binaries survive —
so seed it once and it persists from then on. To carry a dev build's
configuration over, copy that build's file across before the first run:

```
copy bin\Release\net8.0-windows\settings.json publish\portable\settings.json
```

Do that *before* first launching the portable build, not after: with no local
file present the first run seeds one (from `%APPDATA%`, if a pre-portable
install left one there), and once the app has written its own, copying over it
discards whatever it saved.

## Notes

**Do not add `PublishTrimmed`.** It cuts size substantially but breaks WPF:
XAML resolves types by reflection and the trimmer removes them, so you get
runtime crashes rather than build errors.

**Do not nest a copy of these sources inside the project folder.** The SDK
globs `**/*.cs` and excludes only `bin` and `obj`, so a copy under, say,
`publish\source` gets compiled a second time and every type collides.

**`dotnet build` can delete `publish\`.** An incremental build after source
files are removed will clean stale outputs, so republish after any such change.

**Everything is portable.** Settings, the stall log and any other data live
beside the executable. The app reads and writes `settings.json` there and
nowhere else.

**One exception, and it is a backup.** Every settings save also drops a copy in
`%APPDATA%\MPC-HC Video Editor\settings.json`. The app reads that copy only when
it starts with no settings beside the executable — the state a folder replaced
during an upgrade is in, and also the state left by a pre-portable install. A
local file always wins, so a deliberately fresh copy of the folder stays fresh.
Worth knowing while testing: deleting `publish\portable\settings.json` no longer
gives you a first-run install on a machine that has ever saved settings. Clear
the `%APPDATA%` copy too.

**Bookmarks work the same way, as a folder.** They live in `Bookmarks\` beside
the executable, mirrored to `%APPDATA%\MPC-HC Video Editor\Bookmarks\`. An empty
`Bookmarks\` folder is restored from the mirror at startup, which is what makes
replacing the program folder safe. The same testing note applies, doubled: to
get a clean slate you have to empty both folders.

**`bookmarks.index` is what makes a deletion stick.** It lists the bookmark
files this install has. A name in it that is no longer in `Bookmarks\` was
deleted while the program was closed, so the mirror copy is dropped too —
without it, deleting your last bookmark in Explorer would see it restored on the
next launch, an empty folder being exactly what a replaced one looks like. It is
never mirrored, which is the point: a replaced folder loses it and reads as
"never had any", while a hand-deleted file leaves it behind and reads as "had
it, lost it". Delete it by hand only together with `Bookmarks\`.

## Packaging a release

**Never ship the folder you have been running.** `publish\portable` is both the
build output and the working directory of the portable build, so it accumulates
your own data as you test. Two things must be out of the zip:

- `settings.json` — holds every local path you have configured
- `Bookmarks\` — holds the filename and cut timings of every video you have
  bookmarked
- `bookmarks.index` — lists those filenames again

None of them is produced by `dotnet publish`, so none comes back if you delete
it, and `dotnet publish` will not overwrite them either — they persist until
removed by hand. Check for all three before zipping:

```powershell
Get-ChildItem publish\portable -Force | Where-Object { $_.Name -in 'settings.json','Bookmarks','bookmarks.index' }
```

Anything that command prints is yours and does not belong in a public download.
