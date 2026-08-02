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
beside the executable, not under `%APPDATA%`. On first run the app copies an
existing `%APPDATA%\MPC-HC Video Editor\settings.json` in, if one is there, so
an earlier non-portable install's configuration carries over.
