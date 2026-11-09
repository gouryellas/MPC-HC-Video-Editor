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

**Regenerate `CHANGELOG.md` after publishing the GitHub release.** Help ▸ About
lists every version from that file, which is compiled into the assembly — so a
build made before the release is published has no notes for it. The file is
written from the published releases:

```powershell
gh api --paginate "repos/gouryellas/MPC-HC-Video-Editor/releases" > releases.json
```

Then one entry per release, newest first, under a `## <version> — <date>`
heading, with each release body's own `##` sections demoted to `###`. That
demotion is what lets the parser treat `## ` as "a version starts here" and
nothing else; a release section called "## Playlists" would otherwise be read
as a version named Playlists. `ChangelogTests` holds both ends of that.

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

## Tests

```
dotnet test Tests
```

Fifty-eight of them, in a few seconds. They need `ffmpeg.exe` and
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
