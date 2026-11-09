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

**Top up `CHANGELOG.md` after publishing the GitHub release, then build.**
Help ▸ About lists every version from that file, which is compiled into the
assembly — so a build made before the release is published has no notes for
it.

```powershell
powershell -ExecutionPolicy Bypass -File tools\update-changelog.ps1
```

Only the missing releases are fetched: the script reads the versions already
in the file and asks for one page. Add `-Full` to rebuild from every release,
which is needed only if the file is lost or an old release's notes were edited
on GitHub. Both paths produce byte-identical output, so a full rebuild is a
safe way to check the file has not drifted.

Each release body is written with its own `##` sections demoted to `###`, so
`## ` marks a version and nothing else — a release section called
"## Playlists" would otherwise be read back as a version named Playlists.
`ChangelogTests` and `ChangelogCacheTests` hold both ends of that.

A build that is already out in the world fills its own gap: the update check
downloads the newest release's notes anyway, and keeps them in
`changelog.local.md` beside `settings.json`, which About merges over the
compiled-in file. That file is a cache — it holds only versions newer than
the build and empties itself once the build catches up. It is written at run
time, so it must never be in a release zip; it is not produced by
`dotnet publish`, so it only appears in a folder that has been run.

**Never ship the folder you have been running.** `publish\portable` is both the
build output and the working directory of the portable build, so it accumulates
your own data as you test. These must be out of the zip:

- `settings.json` — holds every local path you have configured
- `changelog.local.md` — the update check's note cache, written at run time
- `Bookmarks\` — holds the filename and cut timings of every video you have
  bookmarked
- `bookmarks.index` — lists those filenames again

None of them is produced by `dotnet publish`, so none comes back if you delete
it, and `dotnet publish` will not overwrite them either — they persist until
removed by hand. Check for all of them before zipping:

```powershell
Get-ChildItem publish\portable -Force | Where-Object { $_.Name -in 'settings.json','changelog.local.md','Bookmarks','bookmarks.index' }
```

Anything that command prints is yours and does not belong in a public download.

## Tests

```
dotnet test Tests
```

A hundred and ninety-five of them, in a few seconds. They need `ffmpeg.exe` and
`ffprobe.exe`, which are found beside the test assembly, in
`publish\portable`, or in an `ffmpeg` subfolder of any parent directory — so a
checkout that has published once already has what they need.

The fixture videos are built by ffmpeg on the first run and cached under
`%TEMP%\mve-test-fixtures`. Delete that folder to rebuild them. Each one exists
to break a particular assumption: evenly spaced frames, frames at irregular
intervals behind a container claiming otherwise, a stream whose clock starts
late, and one carrying an edit list. Every frame's brightness encodes its own
index, so a test can say which frame came back rather than a person comparing
two stills.

**The test project is excluded from the application's compile** by
`<Compile Remove="Tests\**" />` — the SDK globs `**/*.cs` and would otherwise
build the tests into the .exe, the same trap described above for a nested copy
of the sources.
