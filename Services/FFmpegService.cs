using System.Collections.Generic;
using System.IO;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using MpcHcVideoEditor.Models;
using MpcHcVideoEditor.Helpers;

namespace MpcHcVideoEditor.Services;

public class FFmpegProgressEventArgs : EventArgs
{
    public string Message { get; init; } = "";
    public int Current { get; init; }
    public int Total { get; init; }
    public double Percent => Total > 0 ? (double)Current / Total * 100 : 0;
}

public class FFmpegService
{
    private readonly string _ffmpegPath;
    private readonly string _ffprobePath;

    [System.Runtime.InteropServices.DllImport("kernel32.dll")]
    private static extern uint SetErrorMode(uint mode);

    static FFmpegService()
    {
        // SEM_FAILCRITICALERRORS | SEM_NOGPFAULTERRORBOX. A child process that
        // fails to load — the broken ffprobe.exe described on Resolve — makes
        // Windows raise a modal hard-error dialog owned by no visible window.
        // It cannot be focused or closed, and it appears before any managed
        // exception we could catch. Child processes inherit this mode, so the
        // loader fails quietly and we handle the non-zero exit ourselves.
        SetErrorMode(0x0001 | 0x0002);
    }

    public event EventHandler<string>? LogReceived;

    public FFmpegService(string? ffmpegDir = null)
    {
        // Search order:
        // 1. Explicit directory if provided
        // 2. Beside the executable — where a portable install keeps them
        // 3. A "ffmpeg" subfolder of the install
        // 4. PATH (where.exe)
        var searchDirs = new List<string>();
        if (!string.IsNullOrWhiteSpace(ffmpegDir))
            searchDirs.Add(ffmpegDir);

        searchDirs.Add(PortablePaths.AppFolder);
        searchDirs.Add(Path.Combine(PortablePaths.AppFolder, "ffmpeg"));
        searchDirs.Add(AppDomain.CurrentDomain.BaseDirectory);

        _ffmpegPath = Resolve("ffmpeg.exe", searchDirs) ?? "ffmpeg";
        _ffprobePath = Resolve("ffprobe.exe", searchDirs) ?? "ffprobe";
    }

    /// <summary>
    /// Picks the first candidate that actually runs, rather than the first one
    /// that merely exists.
    /// </summary>
    /// <remarks>
    /// Existence is not enough: a copy of ffprobe.exe that fails to load with
    /// STATUS_ENTRYPOINT_NOT_FOUND sits on this machine, and because
    /// <see cref="Which"/> shells out to <c>where</c> — which searches the
    /// <em>current directory</em> before PATH — it got picked up whenever the
    /// app happened to start in that folder. Every duration then silently read
    /// as zero, and Windows raised a hard-error dialog behind the app that
    /// could not be focused or dismissed.
    /// </remarks>
    private static string? Resolve(string name, List<string> searchDirs)
    {
        // Ordered by preference; nulls and duplicates are skipped below.
        // A bundled copy beside the executable wins, so a portable install is
        // self-sufficient and does not depend on what is on the machine's PATH.
        var candidates = new List<string?>
        {
            FindBinary(name, searchDirs),
            Which(name)
        };

        // Then every PATH entry explicitly, so a bad match earlier does not
        // shadow a working install further down.
        foreach (var dir in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(';'))
            if (!string.IsNullOrWhiteSpace(dir))
                candidates.Add(Path.Combine(dir.Trim(), name));

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var candidate in candidates)
        {
            if (string.IsNullOrWhiteSpace(candidate)) continue;
            if (!seen.Add(candidate)) continue;
            if (!File.Exists(candidate)) continue;
            if (CanRun(candidate)) return candidate;
        }

        return null;
    }

    /// <summary>True if the binary launches and reports its version.</summary>
    private static bool CanRun(string exePath)
    {
        try
        {
            var psi = new ProcessStartInfo(exePath, "-version")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var p = Process.Start(psi);
            if (p == null) return false;

            // Drain stdout so a full pipe buffer cannot deadlock the wait.
            p.StandardOutput.ReadToEnd();

            if (!p.WaitForExit(5000))
            {
                try { p.Kill(entireProcessTree: true); } catch { /* best effort */ }
                return false;
            }

            return p.ExitCode == 0;
        }
        catch { return false; }
    }

    private static string? FindBinary(string name, IEnumerable<string> dirs)
    {
        foreach (var dir in dirs)
        {
            if (string.IsNullOrWhiteSpace(dir)) continue;
            var candidate = Path.Combine(dir, name);
            if (File.Exists(candidate)) return candidate;
        }
        return null;
    }

    public bool IsAvailable => File.Exists(_ffmpegPath) || !string.IsNullOrEmpty(Which("ffmpeg"));

    private static string? Which(string cmd)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "where",
                Arguments = cmd,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var p = Process.Start(psi);
            var output = p?.StandardOutput.ReadToEnd().Trim();
            p?.WaitForExit();
            return string.IsNullOrWhiteSpace(output) ? null : output.Split('\n')[0].Trim();
        }
        catch { return null; }
    }

    // ------------------------------------------------------------------
    // Convert single file → MP4
    // ------------------------------------------------------------------
    public async Task ConvertToMp4Async(string inputPath, string? outputPath = null, 
        IProgress<FFmpegProgressEventArgs>? progress = null, CancellationToken ct = default)
    {
        outputPath ??= Path.ChangeExtension(inputPath, ".mp4");

        var args = $"-hide_banner -y -fflags +igndts -i \"{inputPath}\" " +
                   $"-c:v libx264 -preset veryfast -pix_fmt yuv420p " +
                   $"-c:a aac -b:a 192k -ar 48000 -ac 2 \"{outputPath}\"";

        // Probe first so the progress bar has something to divide by.
        await RunAsync(args, progress, ct, await GetDurationAsync(inputPath));
    }

    // ------------------------------------------------------------------
    // Strip audio → MP3
    // ------------------------------------------------------------------
    public async Task StripAudioAsync(string inputPath, string? outputPath = null,
        IProgress<FFmpegProgressEventArgs>? progress = null, CancellationToken ct = default)
    {
        outputPath ??= Path.ChangeExtension(inputPath, ".mp3");

        var args = $"-hide_banner -y -fflags +igndts -i \"{inputPath}\" " +
                   $"-vn -c:a libmp3lame -q:a 0 \"{outputPath}\"";

        await RunAsync(args, progress, ct, await GetDurationAsync(inputPath));
    }

    // ------------------------------------------------------------------
    // Merge selected bookmarks (with optional flip + speed)
    // ------------------------------------------------------------------
    public async Task MergeBookmarksAsync(
        string inputVideo,
        string outputPath,
        IList<Bookmark> bookmarks,
        IProgress<FFmpegProgressEventArgs>? progress = null,
        CancellationToken ct = default)
    {
        if (bookmarks.Count == 0)
            throw new ArgumentException("No bookmarks provided");

        var tempDir = Path.Combine(Path.GetTempPath(), "mpc-editor-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(tempDir);

        try
        {
            var segmentFiles = new List<string>();
            var total = bookmarks.Count;

            for (int i = 0; i < bookmarks.Count; i++)
            {
                ct.ThrowIfCancellationRequested();
                var b = bookmarks[i];
                if (!b.IsValid) continue;

                progress?.Report(new FFmpegProgressEventArgs
                {
                    Message = $"Processing segment {i + 1}/{total}",
                    Current = i,
                    Total = total
                });

                var segmentPath = Path.Combine(tempDir, $"segment{i + 1:D3}.mp4");
                await CreateSegmentAsync(inputVideo, segmentPath, b, ct);
                segmentFiles.Add(segmentPath);
            }

            // Write concat list
            var concatList = Path.Combine(tempDir, "concat.txt");
            await File.WriteAllLinesAsync(concatList,
                segmentFiles.Select(f => $"file '{f.Replace("'", "'\\''")}'"), ct);

            progress?.Report(new FFmpegProgressEventArgs
            {
                Message = "Concatenating segments…",
                Current = total,
                Total = total
            });

            // Final concat (copy when possible)
            var concatArgs = $"-hide_banner -y -f concat -safe 0 -i \"{concatList}\" " +
                             $"-c copy \"{outputPath}\"";

            // If any segment used filters (flip/speed) we already re-encoded,
            // so -c copy is safe. If pure copy failed we could fall back, but
            // for now we keep it simple.
            await RunAsync(concatArgs, null, ct);
        }
        finally
        {
            try { Directory.Delete(tempDir, true); } catch { /* ignore */ }
        }
    }

    private async Task CreateSegmentAsync(string input, string output, Bookmark b, CancellationToken ct)
    {
        var start = TimeSpan.FromSeconds(b.StartSeconds).ToString(@"hh\:mm\:ss\.fff", CultureInfo.InvariantCulture);
        var end   = TimeSpan.FromSeconds(b.EndSeconds).ToString(@"hh\:mm\:ss\.fff", CultureInfo.InvariantCulture);

        var vf = new List<string>();
        var af = new List<string>();
        bool reencode = false;

        if (b.IsFlipped)
        {
            vf.Add("vflip");
            reencode = true;
        }

        if (Math.Abs(b.Speed - 1.0) > 0.01)
        {
            // setpts for video, atempo for audio (atempo limited to 0.5-2.0)
            vf.Add($"setpts=PTS/{b.Speed.ToString(CultureInfo.InvariantCulture)}");
            // chain atempo if needed
            var speed = b.Speed;
            while (speed > 2.0) { af.Add("atempo=2.0"); speed /= 2.0; }
            while (speed < 0.5) { af.Add("atempo=0.5"); speed /= 0.5; }
            af.Add($"atempo={speed.ToString(CultureInfo.InvariantCulture)}");
            reencode = true;
        }

        var sb = new StringBuilder();
        sb.Append($"-hide_banner -y -fflags +igndts -ss {start} -to {end} -i \"{input}\" ");

        if (vf.Count > 0)
            sb.Append($"-vf \"{string.Join(",", vf)}\" ");
        if (af.Count > 0)
            sb.Append($"-af \"{string.Join(",", af)}\" ");

        if (reencode)
        {
            sb.Append("-c:v libx264 -preset veryfast -crf 18 -pix_fmt yuv420p ");
            sb.Append("-c:a aac -b:a 192k -ar 48000 -ac 2 ");
        }
        else
        {
            sb.Append("-c copy ");
        }

        sb.Append($"\"{output}\"");

        await RunAsync(sb.ToString(), null, ct);
    }

    // ------------------------------------------------------------------
    // Simple multi-file concat (bulk merge)
    // ------------------------------------------------------------------
    public async Task ConcatFilesAsync(IEnumerable<string> inputFiles, string outputPath,
        IProgress<FFmpegProgressEventArgs>? progress = null, CancellationToken ct = default)
    {
        var files = inputFiles.ToList();
        if (files.Count < 2)
            throw new ArgumentException("Need at least two files");

        var tempDir = Path.Combine(Path.GetTempPath(), "mpc-bulk-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(tempDir);

        try
        {
            // Re-encode each to a common format first (safer for mixed sources)
            var segments = new List<string>();
            for (int i = 0; i < files.Count; i++)
            {
                ct.ThrowIfCancellationRequested();
                progress?.Report(new FFmpegProgressEventArgs
                {
                    Message = $"Preparing {i + 1}/{files.Count}",
                    Current = i,
                    Total = files.Count + 1
                });

                var seg = Path.Combine(tempDir, $"s{i:D3}.mp4");
                var args = $"-hide_banner -y -i \"{files[i]}\" " +
                           $"-c:v libx264 -preset veryfast -pix_fmt yuv420p " +
                           $"-c:a aac -ar 48000 -ac 2 \"{seg}\"";
                await RunAsync(args, null, ct);
                segments.Add(seg);
            }

            var listFile = Path.Combine(tempDir, "list.txt");
            await File.WriteAllLinesAsync(listFile,
                segments.Select(s => $"file '{s.Replace("'", "'\\''")}'"), ct);

            progress?.Report(new FFmpegProgressEventArgs
            {
                Message = "Final concat…",
                Current = files.Count,
                Total = files.Count + 1
            });

            var concatArgs = $"-hide_banner -y -f concat -safe 0 -i \"{listFile}\" -c copy \"{outputPath}\"";
            await RunAsync(concatArgs, null, ct);
        }
        finally
        {
            try { Directory.Delete(tempDir, true); } catch { }
        }
    }

    // ------------------------------------------------------------------
    // Core process runner
    // ------------------------------------------------------------------
    /// <param name="totalSeconds">
    /// Duration of the material being written, used to turn ffmpeg's
    /// <c>time=</c> output into a real percentage. Pass 0 when unknown — the
    /// caller then gets progress messages with no percentage rather than a
    /// bar that sits at 0% for the whole job.
    /// </param>
    private async Task RunAsync(string arguments, IProgress<FFmpegProgressEventArgs>? progress,
                                CancellationToken ct, double totalSeconds = 0)
    {
        var psi = new ProcessStartInfo
        {
            FileName = _ffmpegPath,
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };

        using var process = new Process { StartInfo = psi, EnableRaisingEvents = true };

        var tcs = new TaskCompletionSource<int>();

        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data != null) LogReceived?.Invoke(this, e.Data);
        };

        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data == null) return;
            LogReceived?.Invoke(this, e.Data);

            if (progress == null) return;

            // ffmpeg reports "time=HH:MM:SS.ss" on stderr as it encodes.
            // Against a known total that is a real percentage; without one
            // there is nothing to divide by, which is why the bar used to
            // stay at 0% for an entire convert.
            var m = Regex.Match(e.Data, @"time=(\d+):(\d{2}):(\d{2}(?:\.\d+)?)");
            if (!m.Success) return;

            var done = int.Parse(m.Groups[1].Value) * 3600
                     + int.Parse(m.Groups[2].Value) * 60
                     + double.Parse(m.Groups[3].Value, CultureInfo.InvariantCulture);

            if (totalSeconds > 0)
            {
                // Report in whole percent — Current/Total drive Percent.
                var pct = (int)Math.Clamp(done / totalSeconds * 100, 0, 100);
                progress.Report(new FFmpegProgressEventArgs
                {
                    Message = $"Encoding {Bookmark.FormatTime(done)} of {Bookmark.FormatTime(totalSeconds)}",
                    Current = pct,
                    Total = 100
                });
            }
            else
            {
                progress.Report(new FFmpegProgressEventArgs
                {
                    Message = $"Encoding {Bookmark.FormatTime(done)}"
                });
            }
        };

        process.Exited += (_, _) => tcs.TrySetResult(process.ExitCode);

        if (!process.Start())
            throw new InvalidOperationException("Failed to start FFmpeg");

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        await using var reg = ct.Register(() =>
        {
            try { if (!process.HasExited) process.Kill(true); } catch { }
            tcs.TrySetCanceled(ct);
        });

        var exitCode = await tcs.Task;
        if (exitCode != 0)
            throw new Exception($"FFmpeg exited with code {exitCode}");
    }

    public async Task<double> GetDurationAsync(string filePath)
    {
        // Never let a broken ffprobe crash the app with a system dialog.
        try
        {
            var args = $"-v error -show_entries format=duration -of default=noprint_wrappers=1:nokey=1 \"{filePath}\"";
            var psi = new ProcessStartInfo
            {
                FileName = _ffprobePath,
                Arguments = args,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var p = Process.Start(psi);
            if (p == null) return 0;

            var outputTask = p.StandardOutput.ReadToEndAsync();
            var errorTask = p.StandardError.ReadToEndAsync();
            await Task.WhenAll(outputTask, errorTask);
            await p.WaitForExitAsync();

            if (p.ExitCode != 0) return 0;

            return double.TryParse(outputTask.Result.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var d)
                ? d
                : 0;
        }
        catch
        {
            return 0;
        }
    }
}
