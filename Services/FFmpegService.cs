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

    /// <summary>
    /// x264 preset and CRF applied wherever this service re-encodes.
    /// </summary>
    /// <remarks>
    /// Settable rather than constructor-injected so a change in the Settings
    /// dialog takes effect on the next operation without rebuilding the
    /// service — and with it the ffmpeg path resolution, which shells out and
    /// is not worth repeating.
    ///
    /// Defaults to what the two re-encode paths used to hardcode, so a caller
    /// that never sets it behaves exactly as before.
    /// </remarks>
    public string QualityArgs { get; set; } = "-preset faster -crf 20";

    /// <summary>
    /// Which H.264 encoder the re-encode paths use.
    /// </summary>
    /// <remarks>
    /// Paired with <see cref="QualityArgs"/>, which must already match it — the
    /// two are set together from Settings, because a CRF handed to NVENC is not
    /// merely suboptimal, it is rejected.
    /// </remarks>
    public VideoEncoder Encoder { get; set; } = VideoEncoder.Software;

    /// <summary>The ffmpeg encoder name for <see cref="Encoder"/>.</summary>
    private string VideoCodec => VideoEncoders.CodecFor(Encoder);

    /// <summary>
    /// Cut exactly where asked, at the cost of re-encoding.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A stream-copied cut cannot start anywhere but a keyframe. ffmpeg is
    /// given the requested time, copies from the keyframe at or before it, and
    /// the clip therefore opens early — by a frame or two on a densely-keyed
    /// file, by several seconds on a sparsely-keyed one. Nothing about the
    /// output says this happened.
    /// </para>
    /// <para>
    /// Setting this decodes and re-encodes the segment so the first frame is
    /// the one asked for. It costs encode time and a generation of quality on
    /// every cut, including the ones that would otherwise have been a lossless
    /// copy — which is why it is off by default rather than simply better.
    /// </para>
    /// </remarks>
    public bool PreciseCuts { get; set; }

    /// <summary>
    /// Every ffmpeg this service has started and not yet seen exit.
    /// </summary>
    /// <remarks>
    /// Exists so <see cref="KillAll"/> can clean up on shutdown. An encode
    /// outlives the window that started it otherwise: nothing cancels the
    /// operation when the app closes, so ffmpeg carries on writing to a file
    /// nobody is waiting for, holding a handle on it.
    /// </remarks>
    private readonly List<Process> _running = new();

    /// <summary>
    /// Kills any ffmpeg still running. Called when the app is shutting down.
    /// </summary>
    /// <remarks>
    /// <c>entireProcessTree</c> because ffmpeg can spawn helpers of its own.
    /// Every failure is swallowed: this runs during teardown, where the
    /// process is about to disappear and there is nobody left to tell.
    /// </remarks>
    public void KillAll()
    {
        List<Process> snapshot;
        lock (_running) snapshot = _running.ToList();

        foreach (var p in snapshot)
        {
            try { if (!p.HasExited) p.Kill(entireProcessTree: true); }
            catch { /* already gone, or never ours to kill */ }
        }
    }

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

    /// <summary>Results of <see cref="CanEncodeAsync"/>, which never change within a run.</summary>
    private readonly Dictionary<VideoEncoder, bool> _encoderProbes = new();

    /// <summary>
    /// Whether this machine can actually encode with the given encoder.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Asking ffmpeg for its encoder list proves nothing: the GPU encoders are
    /// compiled in unconditionally, so <c>h264_nvenc</c> is listed on a machine
    /// with no NVIDIA card in it at all. The failure only appears when one is
    /// opened, and by then it is in the middle of a job the user asked for.
    /// </para>
    /// <para>
    /// So this encodes a fraction of a second of generated video to the null
    /// muxer and reports whether ffmpeg got through it. That costs a second or
    /// so the first time and is cached thereafter — hardware does not appear
    /// or vanish mid-session.
    /// </para>
    /// </remarks>
    public async Task<bool> CanEncodeAsync(VideoEncoder encoder, CancellationToken ct = default)
    {
        // x264 ships inside the binary; if ffmpeg runs at all, it works.
        if (encoder == VideoEncoder.Software) return true;

        lock (_encoderProbes)
            if (_encoderProbes.TryGetValue(encoder, out var cached)) return cached;

        var codec = VideoEncoders.CodecFor(encoder);
        var args = "-hide_banner -loglevel error -f lavfi " +
                   "-i testsrc=size=320x240:rate=25:duration=0.2 " +
                   $"-c:v {codec} {VideoEncoders.QualityArgsFor(encoder, EncodingQuality.Fast)} -f null -";

        bool ok;
        try
        {
            var psi = new ProcessStartInfo(_ffmpegPath, args)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var p = Process.Start(psi);
            if (p == null) return false;

            // Drain both pipes so a full buffer cannot deadlock the wait.
            var outTask = p.StandardOutput.ReadToEndAsync(ct);
            var errTask = p.StandardError.ReadToEndAsync(ct);
            await Task.WhenAll(outTask, errTask);

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(TimeSpan.FromSeconds(20));
            await p.WaitForExitAsync(timeout.Token);

            ok = p.ExitCode == 0;
        }
        catch
        {
            // A probe that cannot even be run is a "no", not a crash.
            ok = false;
        }

        lock (_encoderProbes) _encoderProbes[encoder] = ok;
        return ok;
    }

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
    // Convert single file → the configured container
    // ------------------------------------------------------------------

    /// <summary>
    /// Re-encodes <paramref name="inputPath"/> into <paramref name="format"/>.
    /// </summary>
    /// <param name="format">
    /// Target container. Null means MP4, which is what this method did
    /// unconditionally before the output format became configurable.
    /// </param>
    public async Task ConvertVideoAsync(string inputPath, string? outputPath = null,
        VideoFormats.Format? format = null,
        IProgress<FFmpegProgressEventArgs>? progress = null, CancellationToken ct = default)
    {
        format ??= VideoFormats.Default;
        outputPath ??= Path.ChangeExtension(inputPath, format.Extension);

        var args = $"-hide_banner -y -fflags +igndts -i \"{inputPath}\" " +
                   $"{VideoFormats.ApplyEncoder(format, VideoCodec, QualityArgs)} \"{outputPath}\"";

        // Probe first so the progress bar has something to divide by.
        await RunAsync(args, progress, ct, await GetDurationAsync(inputPath));
    }

    /// <summary>Converts to MP4. Retained for callers that want MP4 regardless of settings.</summary>
    public Task ConvertToMp4Async(string inputPath, string? outputPath = null,
        IProgress<FFmpegProgressEventArgs>? progress = null, CancellationToken ct = default) =>
        ConvertVideoAsync(inputPath, outputPath, VideoFormats.Default, progress, ct);

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
    /// <param name="format">
    /// Container to write. Segments are always cut as H.264/AAC MP4 — that is
    /// what the cutter produces and what copies fastest — so a format whose
    /// mux accepts H.264 concatenates by stream copy, and one that does not
    /// re-encodes once at the concat step. Null means MP4.
    /// </param>
    public async Task MergeBookmarksAsync(
        string inputVideo,
        string outputPath,
        IList<Bookmark> bookmarks,
        IProgress<FFmpegProgressEventArgs>? progress = null,
        CancellationToken ct = default,
        VideoFormats.Format? format = null)
    {
        if (bookmarks.Count == 0)
            throw new ArgumentException("No bookmarks provided");

        format ??= VideoFormats.Default;

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
                Message = format.CanCopyH264 ? "Concatenating segments…" : $"Encoding to {format.Key.ToUpperInvariant()}…",
                Current = total,
                Total = total
            });

            // Segments are already H.264/AAC, whether they were copied or
            // re-encoded for a flip or speed change. A container that accepts
            // those streams therefore needs no second encode; one that does
            // not — WebM, MPEG-2, ASF, AVI — pays for it here, once, rather
            // than per segment.
            var outputArgs = format.CanCopyH264
                ? "-c copy"
                : VideoFormats.ApplyEncoder(format, VideoCodec, QualityArgs);

            var concatArgs = $"-hide_banner -y -f concat -safe 0 -i \"{concatList}\" " +
                             $"{outputArgs} \"{outputPath}\"";

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

        // A stream copy can only begin at a keyframe, so an exact cut has to be
        // re-encoded whether or not any filter asked for it.
        bool reencode = PreciseCuts;

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
            // Segments stay H.264/AAC whatever the final container is — the
            // concat step converts once at the end if it has to, which is
            // cheaper than encoding every segment into the target codec.
            sb.Append($"-c:v {VideoCodec} {QualityArgs} -pix_fmt yuv420p ");
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
                // Deliberately the Fast profile whatever Settings says: these
                // are throwaway intermediates that the concat step copies, so
                // effort spent here buys nothing. Routed through the chosen
                // encoder all the same, since x264's presets mean nothing to a
                // GPU encoder.
                var args = $"-hide_banner -y -i \"{files[i]}\" " +
                           $"-c:v {VideoCodec} {VideoEncoders.QualityArgsFor(Encoder, EncodingQuality.Fast)} " +
                           $"-pix_fmt yuv420p -c:a aac -ar 48000 -ac 2 \"{seg}\"";
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
    /// <summary>
    /// Turns a failed ffmpeg run into something a person can act on.
    /// </summary>
    /// <remarks>
    /// Names the encoder when a GPU one is selected. A hardware encoder that
    /// opened successfully during the settings probe can still fail on real
    /// content — an unusual resolution, a bit depth the chip does not do — and
    /// the encoder is then the first thing worth changing. Nothing else in the
    /// message would point at it.
    /// </remarks>
    private string DescribeFailure(int exitCode, Queue<string> recentErrors)
    {
        string[] lines;
        lock (recentErrors) lines = recentErrors.ToArray();

        var detail = lines.Length > 0
            ? Environment.NewLine + Environment.NewLine + string.Join(Environment.NewLine, lines)
            : string.Empty;

        var hint = Encoder != VideoEncoder.Software
            ? Environment.NewLine + Environment.NewLine +
              $"This used the {VideoEncoders.DisplayName(Encoder)} encoder. If it keeps failing, " +
              "switch the H.264 encoder back to Software in Settings ▸ Encoding — it works on any machine."
            : string.Empty;

        return $"FFmpeg exited with code {exitCode}.{detail}{hint}";
    }

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

        // ffmpeg says why it failed on stderr, and this used to read that
        // stream purely to scrape a progress percentage out of it — so a
        // failed job reported nothing but its exit code. "FFmpeg exited with
        // code -1313558101" is not something anyone can act on. Keeping the
        // last few lines costs nothing and turns that into an actual reason.
        var recentErrors = new Queue<string>();

        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data == null) return;
            LogReceived?.Invoke(this, e.Data);

            if (e.Data.Trim().Length > 0)
            {
                lock (recentErrors)
                {
                    recentErrors.Enqueue(e.Data.Trim());
                    while (recentErrors.Count > 6) recentErrors.Dequeue();
                }
            }

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

        // Registered after a successful start and removed in the finally
        // below, so the list only ever holds processes that are actually ours
        // and actually running.
        lock (_running) _running.Add(process);

        try
        {
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            await using var reg = ct.Register(() =>
            {
                try { if (!process.HasExited) process.Kill(true); } catch { }
                tcs.TrySetCanceled(ct);
            });

            var exitCode = await tcs.Task;

            // The Exited event fires before the redirected output readers have
            // drained and before the handles ffmpeg held on its input and
            // output files are necessarily released. The parameterless
            // WaitForExit is the documented way to block for exactly that —
            // without it, deleting the source immediately after an operation
            // could fail with a sharing violation on a process that had, as
            // far as the event was concerned, already finished.
            try { process.WaitForExit(); } catch { /* already reaped */ }

            if (exitCode != 0)
                throw new Exception(DescribeFailure(exitCode, recentErrors));
        }
        finally
        {
            lock (_running) _running.Remove(process);
        }
    }

    /// <summary>
    /// Grabs a single frame at <paramref name="seconds"/> as PNG bytes, scaled
    /// to <paramref name="height"/> pixels tall. Returns <c>null</c> if the
    /// frame could not be read.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Piped out of ffmpeg's stdout rather than written to a file: a thumbnail
    /// is a throwaway, and a portable app that scatters temp images beside
    /// itself — or leaves them behind when it is killed — is worse than one
    /// that keeps them in memory for as long as they are on screen.
    /// </para>
    /// <para>
    /// <c>-ss</c> before <c>-i</c> so ffmpeg seeks rather than decoding from
    /// the start; because the frame is then re-encoded to PNG it still lands on
    /// the exact timestamp, unlike a stream copy.
    /// </para>
    /// </remarks>
    public async Task<byte[]?> ExtractFrameAsync(
        string videoPath, double seconds, int height = 76, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(videoPath) || !File.Exists(videoPath)) return null;
        if (seconds < 0) seconds = 0;

        var timestamp = seconds.ToString("0.###", CultureInfo.InvariantCulture);
        var args = $"-hide_banner -loglevel error -ss {timestamp} -i \"{videoPath}\" " +
                   $"-frames:v 1 -vf scale=-2:{height} -f image2pipe -c:v png -";

        try
        {
            var psi = new ProcessStartInfo(_ffmpegPath, args)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);
            if (process == null) return null;

            using var buffer = new MemoryStream();
            // Both pipes drained together — leaving stderr unread deadlocks the
            // moment ffmpeg says anything longer than the pipe buffer.
            var copy = process.StandardOutput.BaseStream.CopyToAsync(buffer, ct);
            var errors = process.StandardError.ReadToEndAsync(ct);
            await Task.WhenAll(copy, errors).ConfigureAwait(false);

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(TimeSpan.FromSeconds(20));
            await process.WaitForExitAsync(timeout.Token).ConfigureAwait(false);

            if (process.ExitCode != 0 || buffer.Length == 0) return null;
            return buffer.ToArray();
        }
        catch (OperationCanceledException)
        {
            // Deliberately not folded into the catch below. "You cancelled me"
            // and "this frame cannot be read" are different answers, and a
            // caller that caches results must not record the first as the
            // second — see ThumbnailService.GetAsync.
            throw;
        }
        catch
        {
            // A thumbnail is a nicety. Failing to make one must never surface
            // as an error, let alone interrupt an edit.
            return null;
        }
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
