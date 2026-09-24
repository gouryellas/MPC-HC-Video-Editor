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

    /// <summary>
    /// Name of the file this step is working on, when the operation knows it.
    /// Optional — most callers already display a file name of their own.
    /// </summary>
    public string? File { get; init; }
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
    /// Even out loudness across the clips being written.
    /// </summary>
    /// <remarks>
    /// Forces a re-encode of anything it applies to, exactly as
    /// <see cref="PreciseCuts"/> does — audio cannot be normalized while being
    /// copied. Stated in the settings hint rather than left as a surprise.
    /// </remarks>
    public bool NormalizeAudio { get; set; }

    /// <summary>
    /// EBU R128 target. The broadcast-ish default: quiet enough to leave
    /// headroom, loud enough to match most streaming material.
    /// </summary>
    private const string LoudnormFilter = "loudnorm=I=-16:TP=-1.5:LRA=11";

    /// <summary>
    /// Set after an operation when the file produced does not match what was
    /// asked for, or null when it does.
    /// </summary>
    /// <remarks>
    /// Exists because a keyframe-aligned cut can run over a second long with
    /// nothing in the output saying so — the app knew what it asked for and
    /// never checked what it got. One ffprobe at the end of a job turns a
    /// silent discrepancy into a stated one.
    /// </remarks>
    public string? LastOutputWarning { get; private set; }

    /// <summary>
    /// Compares the finished file against the length that was asked for.
    /// </summary>
    /// <remarks>
    /// The tolerance is deliberately loose. Container overhead, audio frame
    /// alignment and rounding all move the duration slightly, and a warning
    /// that fires on every job is one nobody reads. Half a second, or 2% for
    /// longer edits, only trips on something a person would notice.
    /// </remarks>
    private async Task VerifyOutputAsync(string outputPath, double expectedSeconds, CancellationToken ct)
    {
        LastOutputWarning = null;
        if (expectedSeconds <= 0 || !File.Exists(outputPath)) return;

        var actual = await GetDurationAsync(outputPath);
        if (actual <= 0) return;

        var drift = actual - expectedSeconds;
        var tolerance = Math.Max(0.5, expectedSeconds * 0.02);
        if (Math.Abs(drift) <= tolerance) return;

        var longer = drift > 0;
        var amount = $"{Math.Abs(drift):0.0}s {(longer ? "longer" : "shorter")}";

        LastOutputWarning = PreciseCuts
            ? $"The finished file is {amount} than the marked range " +
              $"({Bookmark.FormatTime(actual)} against {Bookmark.FormatTime(expectedSeconds)}). " +
              "Precise cutting is on, so this is worth looking at — the source may have a broken timeline."
            : $"The finished file is {amount} than the marked range " +
              $"({Bookmark.FormatTime(actual)} against {Bookmark.FormatTime(expectedSeconds)}). " +
              "Copied cuts can only start at a keyframe, so a clip may begin before its mark. " +
              "Turn on Precise in Settings ▸ Encoding ▸ Cut accuracy to cut exactly.";
    }

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

    /// <summary>
    /// Ends one process if it is still running. Safe to call on a process that
    /// has already exited, which is the normal case.
    /// </summary>
    /// <remarks>
    /// For the short-lived helpers — thumbnails, encoder probes —
    /// that are not registered in <see cref="_running"/>. Disposing a
    /// <see cref="Process"/> only releases the handle, so a run abandoned by a
    /// cancellation or a timeout would otherwise stay alive, blocked on a pipe
    /// with nothing left to drain it and holding its input file open.
    ///
    /// <c>entireProcessTree</c> for the same reason as <see cref="KillAll"/>,
    /// and one more: ffmpeg on PATH is often a shim that runs the real binary
    /// as a child, so killing what we started is not enough on its own.
    /// </remarks>
    private static void EndProcess(Process process)
    {
        try { if (!process.HasExited) process.Kill(entireProcessTree: true); }
        catch { /* exited between the check and the kill, or already gone */ }
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

            try
            {
                // Drain both pipes so a full buffer cannot deadlock the wait.
                var outTask = p.StandardOutput.ReadToEndAsync(ct);
                var errTask = p.StandardError.ReadToEndAsync(ct);
                await Task.WhenAll(outTask, errTask);

                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
                timeout.CancelAfter(TimeSpan.FromSeconds(20));
                await p.WaitForExitAsync(timeout.Token);

                ok = p.ExitCode == 0;
            }
            finally
            {
                EndProcess(p);
            }
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

        // Convert is a re-encode whatever happens, so normalizing costs nothing
        // extra here — unlike a cut, where it is the thing that rules out the
        // stream copy. A folder of files gathered from different sources is
        // exactly the problem the setting exists for, and this operation was
        // not applying it: the same checkbox evened out a merge and did nothing
        // at all to a batch convert.
        //
        // Safe for every format offered, because all of them re-encode the
        // audio — there is no "-c:a copy" here for a filter to conflict with.
        var normalize = NormalizeAudio ? $"-af \"{LoudnormFilter}\" " : string.Empty;

        var args = $"-hide_banner -y -fflags +igndts -i \"{inputPath}\" {normalize}" +
                   $"{VideoFormats.ApplyEncoder(format, VideoCodec, QualityArgs)} \"{outputPath}\"";

        // Probe first so the progress bar has something to divide by.
        await RunAsync(args, progress, ct, await GetDurationAsync(inputPath));
    }

    /// <summary>Converts to MP4. Retained for callers that want MP4 regardless of settings.</summary>
    public Task ConvertToMp4Async(string inputPath, string? outputPath = null,
        IProgress<FFmpegProgressEventArgs>? progress = null, CancellationToken ct = default) =>
        ConvertVideoAsync(inputPath, outputPath, VideoFormats.Default, progress, ct);

    // ------------------------------------------------------------------
    // Export a cut as an animation
    // ------------------------------------------------------------------

    /// <summary>
    /// Writes one cut as an animated GIF or WebP, with no sound.
    /// </summary>
    /// <param name="webp">
    /// WebP rather than GIF. Both are offered because they are not
    /// interchangeable: GIF goes anywhere at all, WebP is a fraction of the size
    /// and has real alpha and more than 256 colors.
    /// </param>
    /// <param name="fps">
    /// Frames a second. The single biggest lever on the size of the result,
    /// which is why it is asked rather than assumed.
    /// </param>
    /// <param name="width">
    /// Pixels wide; the height follows the aspect ratio. Zero leaves the frame
    /// at its own width.
    /// </param>
    /// <remarks>
    /// The cut's own flip, rotation, speed and fades are applied, so an
    /// animation of a cut matches the clip of it.
    ///
    /// GIF gets a palette generated from the clip itself in the same pass, via
    /// <c>split</c> — one stream feeds <c>palettegen</c> and the other waits for
    /// the result. GIF has 256 colors to spend and the default web palette
    /// spends them badly on real footage; <c>stats_mode=diff</c> weights them
    /// towards what actually moves, which is what the eye is on.
    ///
    /// The two passes this normally takes would mean a palette file beside the
    /// executable, which a portable app should not scatter — and one pass cannot
    /// be cancelled halfway leaving the other behind.
    /// </remarks>
    public async Task ExportAnimationAsync(string inputPath, string outputPath, Bookmark b,
        bool webp, int fps, int width,
        IProgress<FFmpegProgressEventArgs>? progress = null, CancellationToken ct = default)
    {
        var start = TimeSpan.FromSeconds(b.StartSeconds).ToString(@"hh\:mm\:ss\.fff", CultureInfo.InvariantCulture);
        var end = TimeSpan.FromSeconds(b.EndSeconds).ToString(@"hh\:mm\:ss\.fff", CultureInfo.InvariantCulture);

        // The cut's own filters first, then the ones this format needs.
        var vf = PictureFilters(b);
        vf.Add($"fps={fps}");
        if (width > 0) vf.Add($"scale={width}:-1:flags=lanczos");

        string args;
        if (webp)
        {
            args = $"-hide_banner -y -fflags +igndts -ss {start} -to {end} -i \"{inputPath}\" " +
                   $"-an -vf \"{string.Join(",", vf)}\" " +
                   $"-c:v libwebp_anim -lossless 0 -q:v 75 -compression_level 5 -loop 0 " +
                   $"\"{outputPath}\"";
        }
        else
        {
            // Appended to the filter chain rather than given as a separate
            // filter_complex, so the cut's own filters run before the palette is
            // measured — a palette taken before a fade would be built from
            // colors the output never shows.
            var chain = string.Join(",", vf) +
                        ",split[s0][s1];[s0]palettegen=stats_mode=diff[p];" +
                        "[s1][p]paletteuse=dither=bayer:bayer_scale=5:diff_mode=rectangle";

            args = $"-hide_banner -y -fflags +igndts -ss {start} -to {end} -i \"{inputPath}\" " +
                   $"-an -vf \"{chain}\" -loop 0 \"{outputPath}\"";
        }

        await RunAsync(args, progress, ct, b.DurationSeconds / b.Speed);
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

    /// <summary>
    /// Writes a copy of the video with its audio removed. Keeps the container
    /// and every other stream as they are.
    /// </summary>
    /// <remarks>
    /// <c>-c copy</c>, so nothing is re-encoded: the picture is the same
    /// bitstream it was, and the operation runs at disk speed rather than
    /// encoder speed. Dropping a stream needs no decoding, which is the whole
    /// reason this is not a Convert with the audio turned off.
    ///
    /// <c>-map 0 -map -0:a</c> rather than a bare <c>-an</c>: the default
    /// mapping takes one stream per type and would quietly drop subtitles and
    /// any second video track. This takes everything and then removes the audio
    /// from what it took.
    /// </remarks>
    public async Task RemoveAudioAsync(string inputPath, string? outputPath = null,
        IProgress<FFmpegProgressEventArgs>? progress = null, CancellationToken ct = default)
    {
        outputPath ??= Path.Combine(
            Path.GetDirectoryName(inputPath) ?? "",
            Path.GetFileNameWithoutExtension(inputPath) + "-silent" + Path.GetExtension(inputPath));

        var args = $"-hide_banner -y -fflags +igndts -i \"{inputPath}\" " +
                   $"-map 0 -map -0:a -c copy \"{outputPath}\"";

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

            // What was asked for, at the speed each clip will actually play.
            var expected = bookmarks
                .Where(b => b.IsValid)
                .Sum(b => (b.EndSeconds - b.StartSeconds) / (Math.Abs(b.Speed) < 0.01 ? 1.0 : b.Speed));
            await VerifyOutputAsync(outputPath, expected, ct);
        }
        finally
        {
            try { Directory.Delete(tempDir, true); } catch { /* ignore */ }
        }
    }

    /// <summary>
    /// The picture filters a cut asks for, in the order they have to run.
    /// </summary>
    /// <remarks>
    /// Shared by the cut path and the animated-export path so the two cannot
    /// drift: a GIF of a cut that is flipped, turned, sped up and faded looks
    /// like the clip of it.
    ///
    /// Order is load-bearing. Transpose swaps width and height, so it goes
    /// before anything that cares about the frame's shape. The fades go last
    /// because <c>fade</c> takes timestamps and <c>setpts</c> has already
    /// rewritten them — a one-second fade on a half-speed cut then means one
    /// second of the clip as written, which is what the row says.
    /// </remarks>
    private static List<string> PictureFilters(Bookmark b)
    {
        var vf = new List<string>();

        if (b.IsFlipped) vf.Add("vflip");

        if (b.Rotation != Rotation.None)
            vf.Add(b.Rotation switch
            {
                Rotation.Clockwise => "transpose=1",
                Rotation.Counterclockwise => "transpose=2",
                // No single transpose for a half turn; two quarter turns the
                // same way is the standard spelling of it.
                _ => "transpose=1,transpose=1"
            });

        if (Math.Abs(b.Speed - 1.0) > 0.01)
            vf.Add($"setpts=PTS/{b.Speed.ToString(CultureInfo.InvariantCulture)}");

        foreach (var fade in FadeFilters(b, video: true)) vf.Add(fade);

        return vf;
    }

    /// <summary>
    /// The audio filters a cut asks for, in order. Mute, then tempo, then
    /// loudness, then the fades.
    /// </summary>
    /// <remarks>
    /// Loudness after the tempo change, so it measures what will actually be
    /// heard rather than what was there before. The fades after loudness,
    /// because a fade is the shape of the ending rather than part of the
    /// material being levelled — normalizing afterwards would read the silent
    /// tail as programme and lift the whole clip to compensate.
    /// </remarks>
    private List<string> SoundFilters(Bookmark b)
    {
        var af = new List<string>();

        // Silenced rather than dropped. "-an" would leave this segment with no
        // audio stream while its neighbours kept theirs, and the concat demuxer
        // requires every segment to have the same streams in the same order —
        // a merge of a muted clip and an unmuted one would fail outright, or
        // produce a file that loses audio from the first mute onward.
        if (b.IsMuted) af.Add("volume=0");

        if (Math.Abs(b.Speed - 1.0) > 0.01)
        {
            // atempo only accepts 0.5–2.0, so anything outside that is reached
            // by chaining it.
            var speed = b.Speed;
            while (speed > 2.0) { af.Add("atempo=2.0"); speed /= 2.0; }
            while (speed < 0.5) { af.Add("atempo=0.5"); speed /= 0.5; }
            af.Add($"atempo={speed.ToString(CultureInfo.InvariantCulture)}");
        }

        if (NormalizeAudio) af.Add(LoudnormFilter);

        foreach (var fade in FadeFilters(b, video: false)) af.Add(fade);

        return af;
    }

    /// <summary>
    /// The fade filters for one chain, or nothing when the cut does not fade.
    /// </summary>
    /// <remarks>
    /// Capped at half the clip here rather than on the bookmark: a bookmark's
    /// times move, and a value clamped where it was entered could not grow back
    /// when the cut was lengthened again. Half at each end means a fade in and a
    /// fade out can meet in the middle but never cross — crossing makes the tail
    /// brighten as it ends.
    /// </remarks>
    private static IEnumerable<string> FadeFilters(Bookmark b, bool video)
    {
        if (!b.HasFade) yield break;

        // The clip as written, which is what the fades are measured in.
        var length = b.DurationSeconds / b.Speed;
        var name = video ? "fade" : "afade";

        var fadeIn = Math.Min(b.FadeInSeconds, length / 2);
        if (fadeIn > 0) yield return $"{name}=t=in:st=0:d={Fmt(fadeIn)}";

        var fadeOut = Math.Min(b.FadeOutSeconds, length / 2);
        if (fadeOut > 0)
            yield return $"{name}=t=out:st={Fmt(Math.Max(0, length - fadeOut))}:d={Fmt(fadeOut)}";
    }

    private async Task CreateSegmentAsync(string input, string output, Bookmark b, CancellationToken ct)
    {
        var start = TimeSpan.FromSeconds(b.StartSeconds).ToString(@"hh\:mm\:ss\.fff", CultureInfo.InvariantCulture);
        var end   = TimeSpan.FromSeconds(b.EndSeconds).ToString(@"hh\:mm\:ss\.fff", CultureInfo.InvariantCulture);

        var vf = PictureFilters(b);
        var af = SoundFilters(b);

        // A stream copy can only begin at a keyframe, so an exact cut has to be
        // re-encoded whether or not any filter asked for it. Normalizing
        // loudness means touching the audio, which rules out a copy for the same
        // reason — as does muting, which is the one thing that puts a filter on
        // the audio chain without putting one on the picture.
        bool reencode = PreciseCuts || NormalizeAudio || b.IsMuted || vf.Count > 0;

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

        // Segments are brought to one frame rate and one frame size before the
        // join, because the concat demuxer neither re-times nor rescales what
        // it is given: mixing 30fps with 60fps produced one file holding both,
        // under a container declaring a single rate, and MPC-HC stalls at that
        // discontinuity. Mixing sizes was worse — the container reported the
        // first clip's dimensions while later frames were a different size.
        //
        // Both are skipped when the inputs already agree, which is the common
        // case and was costing real time for nothing: normalising is a re-scale
        // and a re-time per clip, and a merge of matching clips needs neither.
        var rates = new List<(string Text, double Value)>();
        var sizes = new List<(int Width, int Height)>();
        foreach (var file in files)
        {
            rates.Add(await GetFrameRateRationalAsync(file));
            sizes.Add(await GetVideoSizeAsync(file));
        }

        // The highest rate among the inputs — deliberately not the most common
        // one, which is how the frame size below is chosen.
        //
        // The two are picked differently because the trade differs. Raising a
        // clip's rate duplicates frames and loses nothing, while lowering one
        // discards motion that cannot come back, and the cost of choosing the
        // highest is mild: rate scales encoding time linearly, so 30 to 60 is
        // twice the work. Size scales with pixel count, so 640x360 to 1920x1080
        // is nine times, and the upscale invents no detail in exchange. Paying
        // double to keep every clip's motion is worth it; paying ninefold to
        // enlarge a clip that has no more detail to show is not.
        //
        // Capped, because the rate this is read from can be a fiction.
        // ffprobe's r_frame_rate is the lowest rate that can represent every
        // timestamp exactly, not the rate the clip plays at — a file with one
        // irregular timestamp can report hundreds. Encoding cost is linear in
        // the rate, so an unchecked outlier turns a one-minute merge into a
        // twenty-minute one. Anything above the cap is not a frame rate anybody
        // shot at, and is ignored.
        var usableRates = rates.Where(r => r.Value > 0 && r.Value <= MaxPlausibleFrameRate).ToList();
        var targetRate = usableRates.Count > 0
            ? usableRates.OrderByDescending(r => r.Value).First()
            : ("", 0.0);

        // Nothing to do when every clip already runs at that rate.
        var ratesAgree = rates.All(r => r.Value > 0 && Math.Abs(r.Value - targetRate.Item2) < 0.01);

        // -fps_mode cfr is the half that actually removes the discontinuity:
        // -r alone sets the nominal rate while leaving source timestamps in
        // place, and it is the timestamps the player trips over.
        var rateArgs = string.IsNullOrEmpty(targetRate.Item1) || ratesAgree
            ? string.Empty
            : $"-r {targetRate.Item1} -fps_mode cfr ";

        // The size most of the clips already are, falling back to the largest
        // when no size has a majority. See the frame rate above for why this
        // one votes and that one does not.
        //
        // Targeting the largest meant a single high-resolution clip dragged
        // every other clip up to meet it, at nine times the encoding work for a
        // 640x360 clip going to 1920x1080 — and the upscale invents no detail
        // the source did not have. Letting the majority win converts the odd
        // clip instead of converting everything to accommodate it.
        //
        // Always an actual input's dimensions, never a computed box: taking the
        // widest width with the tallest height would turn a landscape clip
        // merged with a portrait one into a square that neither clip is.
        var (targetWidth, targetHeight) = sizes
            .Where(s => s.Width > 0 && s.Height > 0)
            .GroupBy(s => s)
            .OrderByDescending(g => g.Count())
            .ThenByDescending(g => (long)g.Key.Width * g.Key.Height)
            .Select(g => g.Key)
            .FirstOrDefault();

        // An unreadable size counts as disagreement: normalising a clip that
        // might already match is a wasted rescale, but skipping one that does
        // not is the broken output this exists to prevent.
        var sizesAgree = sizes.All(s => s.Width == targetWidth && s.Height == targetHeight);

        // Fit inside the target and fill the remainder with black, rather than
        // stretching to it: a 4:3 clip joined to a 16:9 one keeps its geometry
        // and gains bars, which is the conventional and reversible answer.
        // Distorting faces to avoid black edges is not a trade worth making.
        //
        // setsar=1 is part of the fix, not decoration. Two files can share a
        // pixel size and still declare different sample aspect ratios, and the
        // concat would carry only the first — silently stretching the rest. So
        // this is skipped only when every clip is already the same size, not
        // merely when scaling would be a no-op.
        var scaleArgs = targetWidth > 0 && targetHeight > 0 && !sizesAgree
            ? $"-vf \"scale={targetWidth}:{targetHeight}:force_original_aspect_ratio=decrease," +
              $"pad={targetWidth}:{targetHeight}:(ow-iw)/2:(oh-ih)/2,setsar=1\" "
            : string.Empty;

        try
        {
            // Re-encode each to a common format first (safer for mixed sources)
            var segments = new List<string>();
            for (int i = 0; i < files.Count; i++)
            {
                ct.ThrowIfCancellationRequested();
                // No count in the message — the panel shows "4/10" of its own,
                // and repeating it here is what made the old display redundant.
                progress?.Report(new FFmpegProgressEventArgs
                {
                    Message = "Preparing",
                    Current = i,
                    Total = files.Count + 1,
                    File = Path.GetFileName(files[i])
                });

                var seg = Path.Combine(tempDir, $"s{i:D3}.mp4");
                // Deliberately the Fast profile whatever Settings says: these
                // are throwaway intermediates that the concat step copies, so
                // effort spent here buys nothing. Routed through the chosen
                // encoder all the same, since x264's presets mean nothing to a
                // GPU encoder.
                var args = $"-hide_banner -y -i \"{files[i]}\" " +
                           $"-c:v {VideoCodec} {VideoEncoders.QualityArgsFor(Encoder, EncodingQuality.Fast)} " +
                           $"{scaleArgs}{rateArgs}-pix_fmt yuv420p -c:a aac -ar 48000 -ac 2 \"{seg}\"";
                await RunAsync(args, null, ct);
                segments.Add(seg);
            }

            var listFile = Path.Combine(tempDir, "list.txt");
            await File.WriteAllLinesAsync(listFile,
                segments.Select(s => $"file '{s.Replace("'", "'\\''")}'"), ct);

            progress?.Report(new FFmpegProgressEventArgs
            {
                Message = "Joining",
                Current = files.Count,
                Total = files.Count + 1,
                File = Path.GetFileName(outputPath)
            });

            // Video is copied; audio is re-encoded across the joined timeline.
            //
            // Copying both left a timestamp overlap at every seam. AAC codes
            // 1024 samples at a time, so a segment whose video runs exactly
            // 2.000s carries 96256 samples of audio rather than 96000 — the
            // encoder cannot emit a partial frame. Each segment's audio
            // therefore outlasts its own video, the next one starts before it
            // has finished, and ffmpeg reported non-monotonic DTS once per
            // join. Re-encoding removes the per-segment padding by producing
            // one continuous stream, and the warnings with it.
            //
            // Only the audio: the video is already normalised and stream-copied,
            // which is what keeps the join cheap. Audio is a rounding error
            // beside the per-input video encodes that have already run.
            var concatArgs = $"-hide_banner -y -f concat -safe 0 -i \"{listFile}\" " +
                             $"-c:v copy -c:a aac -ar 48000 -ac 2 \"{outputPath}\"";
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
    /// Scans a video and proposes clip ranges from silence, black frames or
    /// scene cuts.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Turns marking from "find every boundary yourself" into "correct the
    /// ones it found". The proposals are never applied here — the caller shows
    /// them and the user decides, because detection on real footage is a good
    /// first guess and nothing more.
    /// </para>
    /// <para>
    /// A full decode is unavoidable: these filters have to see every frame or
    /// sample. On a long video that takes a while, so it reports progress and
    /// honours cancellation.
    /// </para>
    /// </remarks>
    public async Task<List<DetectedRange>> DetectRangesAsync(
        string inputPath, DetectionSettings settings,
        IProgress<FFmpegProgressEventArgs>? progress = null, CancellationToken ct = default)
    {
        if (!File.Exists(inputPath)) return new List<DetectedRange>();

        var duration = await GetDurationAsync(inputPath);

        // -an / -vn where the other stream is irrelevant: decoding video for a
        // silence scan is most of the cost for none of the answer.
        var filter = settings.Mode switch
        {
            DetectionMode.BlackFrames =>
                $"-vn:__none__ -an -vf blackdetect=d={Fmt(settings.MinBoundarySeconds)}:pic_th={Fmt(settings.Threshold)}",
            DetectionMode.SceneChanges =>
                $"-an -vf select='gt(scene\\,{Fmt(settings.Threshold)})',showinfo",
            _ =>
                $"-vn -af silencedetect=noise={Fmt(settings.Threshold)}dB:d={Fmt(settings.MinBoundarySeconds)}"
        };

        // blackdetect needs the video; the placeholder above only existed to
        // keep the switch arms parallel.
        filter = filter.Replace("-vn:__none__ ", string.Empty);

        var args = $"-hide_banner -nostats -i \"{inputPath}\" {filter} -f null -";
        var log = await RunCaptureAsync(args, duration, progress, ct);

        var ranges = settings.Mode switch
        {
            DetectionMode.SceneChanges => FromCutPoints(log, duration),
            DetectionMode.BlackFrames  => Complement(ParsePairs(log, @"black_start:([\d.]+)\s+black_end:([\d.]+)"), duration),
            _                          => Complement(ParseSilences(log), duration)
        };

        return ranges
            .Where(r => r.Duration >= settings.MinClipSeconds)
            .ToList();
    }

    private static string Fmt(double value) => value.ToString("0.###", CultureInfo.InvariantCulture);

    /// <summary>
    /// Runs ffmpeg purely to read its log, returning everything it wrote to
    /// stderr. Unlike <see cref="RunAsync"/> a non-zero exit is not fatal —
    /// a detection pass that ends early still yields usable findings.
    /// </summary>
    private async Task<string> RunCaptureAsync(
        string arguments, double totalSeconds,
        IProgress<FFmpegProgressEventArgs>? progress, CancellationToken ct)
    {
        var psi = new ProcessStartInfo
        {
            FileName = _ffmpegPath,
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardErrorEncoding = Encoding.UTF8
        };

        using var process = new Process { StartInfo = psi, EnableRaisingEvents = true };
        var log = new StringBuilder();
        var tcs = new TaskCompletionSource<int>();

        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data == null) return;
            lock (log) log.AppendLine(e.Data);

            if (progress == null || totalSeconds <= 0) return;
            var m = Regex.Match(e.Data, @"time=(\d+):(\d{2}):(\d{2}(?:\.\d+)?)");
            if (!m.Success) return;

            var done = int.Parse(m.Groups[1].Value) * 3600
                     + int.Parse(m.Groups[2].Value) * 60
                     + double.Parse(m.Groups[3].Value, CultureInfo.InvariantCulture);
            progress.Report(new FFmpegProgressEventArgs
            {
                Message = $"Scanning {Bookmark.FormatTime(done)} of {Bookmark.FormatTime(totalSeconds)}",
                Current = (int)Math.Clamp(done / totalSeconds * 100, 0, 100),
                Total = 100
            });
        };

        process.Exited += (_, _) => tcs.TrySetResult(process.ExitCode);
        if (!process.Start()) return string.Empty;
        lock (_running) _running.Add(process);

        try
        {
            process.BeginErrorReadLine();
            await using var reg = ct.Register(() =>
            {
                try { if (!process.HasExited) process.Kill(true); } catch { }
                tcs.TrySetCanceled(ct);
            });
            await tcs.Task;
            try { process.WaitForExit(); } catch { }
        }
        finally
        {
            lock (_running) _running.Remove(process);
        }

        lock (log) return log.ToString();
    }

    /// <summary>Parses silencedetect's two-line start/end reporting.</summary>
    private static List<(double Start, double End)> ParseSilences(string log)
    {
        var gaps = new List<(double, double)>();
        double? open = null;

        foreach (Match m in Regex.Matches(log, @"silence_(start|end):\s*(-?[\d.]+)"))
        {
            var value = double.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture);
            if (m.Groups[1].Value == "start") open = value;
            else if (open.HasValue) { gaps.Add((open.Value, value)); open = null; }
        }

        return gaps;
    }

    private static List<(double Start, double End)> ParsePairs(string log, string pattern)
        => Regex.Matches(log, pattern)
            .Select(m => (double.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture),
                          double.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture)))
            .ToList();

    /// <summary>
    /// Inverts a list of gaps into the stretches between them — the content, as
    /// opposed to the joins.
    /// </summary>
    private static List<DetectedRange> Complement(List<(double Start, double End)> gaps, double duration)
    {
        var result = new List<DetectedRange>();
        var cursor = 0.0;

        foreach (var (start, end) in gaps.OrderBy(g => g.Start))
        {
            if (start > cursor) result.Add(new DetectedRange(cursor, Math.Min(start, duration > 0 ? duration : start)));
            cursor = Math.Max(cursor, end);
        }

        if (duration > cursor) result.Add(new DetectedRange(cursor, duration));
        return result;
    }

    /// <summary>Turns scene-cut timestamps into the spans between them.</summary>
    private static List<DetectedRange> FromCutPoints(string log, double duration)
    {
        var cuts = Regex.Matches(log, @"pts_time:([\d.]+)")
            .Select(m => double.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture))
            .Distinct()
            .OrderBy(t => t)
            .ToList();

        var result = new List<DetectedRange>();
        var cursor = 0.0;
        foreach (var cut in cuts)
        {
            if (cut > cursor) result.Add(new DetectedRange(cursor, cut));
            cursor = cut;
        }
        if (duration > cursor) result.Add(new DetectedRange(cursor, duration));
        return result;
    }

    /// <summary>
    /// Writes the video unchanged, with the bookmarks attached as chapters.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The alternative to cutting: one file that a player can navigate, rather
    /// than N files on disk. The bookmarks are already timestamp pairs, so
    /// nothing about the model changes — only what is done with it.
    /// </para>
    /// <para>
    /// The video and audio are stream-copied, so this is quick and lossless
    /// regardless of the cut-accuracy setting; only the metadata is rewritten.
    /// MKV and MP4 both carry chapters, though some players read MKV's more
    /// reliably.
    /// </para>
    /// </remarks>
    public async Task ExportChaptersAsync(
        string inputPath, string outputPath, IList<Bookmark> bookmarks,
        IProgress<FFmpegProgressEventArgs>? progress = null, CancellationToken ct = default)
    {
        // In time order, not list order. A bookmark set from the player is
        // appended and only sorted when the file is written, so the list can hold
        // them out of sequence — and chapter metadata has to ascend.
        var usable = bookmarks.Where(b => b.IsValid).OrderBy(b => b.StartSeconds).ToList();
        if (usable.Count == 0) throw new ArgumentException("No complete bookmarks to write as chapters");

        var metaPath = Path.Combine(Path.GetTempPath(), "mpc-chapters-" + Guid.NewGuid().ToString("N")[..8] + ".txt");

        try
        {
            var sb = new StringBuilder();
            sb.AppendLine(";FFMETADATA1");

            int number = 1;
            foreach (var b in usable)
            {
                // Milliseconds, which is what TIMEBASE declares below.
                var startMs = (long)Math.Round(b.StartSeconds * 1000);
                var endMs = (long)Math.Round(b.EndSeconds * 1000);
                if (endMs <= startMs) continue;

                sb.AppendLine("[CHAPTER]");
                sb.AppendLine("TIMEBASE=1/1000");
                sb.AppendLine($"START={startMs}");
                sb.AppendLine($"END={endMs}");

                // The clip's own name when it has one. Falls back to the number
                // and start time, which is all that was available before names
                // existed and is still what an unnamed list produces — with
                // "use chapter names" off, every clip is unnamed by design.
                sb.AppendLine(b.HasLabel
                    ? $"title={b.Label}"
                    : $"title=Chapter {number} ({Bookmark.FormatTime(b.StartSeconds)})");
                number++;
            }

            await File.WriteAllTextAsync(metaPath, sb.ToString(), new UTF8Encoding(false), ct);

            progress?.Report(new FFmpegProgressEventArgs { Message = $"Writing {number - 1} chapters…" });

            var args = $"-hide_banner -y -i \"{inputPath}\" -i \"{metaPath}\" " +
                       $"-map_metadata 1 -map_chapters 1 -c copy \"{outputPath}\"";
            await RunAsync(args, progress, ct);
        }
        finally
        {
            try { File.Delete(metaPath); } catch { /* temp file */ }
        }
    }

    /// <summary>
    /// Grabs a single frame at <paramref name="seconds"/> as PNG bytes, scaled
    /// to <paramref name="height"/> pixels tall. Returns <c>null</c> if the
    /// frame could not be read.
    /// </summary>
    /// <param name="height">
    /// Pixels tall, or zero or less for the frame at its own size. Saving a
    /// still wants the whole frame; the panel's thumbnails want 76 pixels of
    /// it, and asking for that is what the default is for.
    /// </param>
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
        var scale = height > 0 ? $"-vf scale=-2:{height} " : string.Empty;
        var args = $"-hide_banner -loglevel error -ss {timestamp} -i \"{videoPath}\" " +
                   $"-frames:v 1 {scale}-f image2pipe -c:v png -";

        return await PipePngAsync(args, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Draws the whole audio track as a single waveform image, as PNG bytes.
    /// </summary>
    /// <param name="videoPath">The video to read the audio from.</param>
    /// <param name="width">
    /// Pixels wide. Rendered well past the width of the bar it is drawn in, so
    /// the picture survives the window being widened without being generated
    /// again.
    /// </param>
    /// <remarks>
    /// Mid grey rather than a theme color, and one image for all of them. The
    /// color is baked into the pixels, so taking it from the palette would mean
    /// decoding the audio again every time the theme changed; a mid grey reads
    /// against both the near-black track of the dark themes and the pale one of
    /// the light themes, and the opacity it is drawn at does the rest.
    ///
    /// <c>split_channels=0</c> — one trace for the lot. Two channels stacked in
    /// twenty pixels is a pattern rather than a shape.
    ///
    /// This decodes the entire audio track, so it is slow on a long file and
    /// belongs off the UI thread and behind a cache. It is also why it went away
    /// in 5.1: nothing was drawing the result.
    /// </remarks>
    public async Task<byte[]?> RenderWaveformAsync(
        string videoPath, int width = 1600, int height = 60, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(videoPath) || !File.Exists(videoPath)) return null;

        var args = $"-hide_banner -loglevel error -nostats -i \"{videoPath}\" " +
                   $"-filter_complex \"showwavespic=s={width}x{height}:colors=0x8C8C8C:split_channels=0\" " +
                   $"-frames:v 1 -f image2pipe -c:v png -";

        return await PipePngAsync(args, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Runs ffmpeg and returns what it wrote to stdout, or null if it wrote
    /// nothing usable.
    /// </summary>
    /// <remarks>
    /// Shared by the frame grab and the waveform: both pipe a single PNG out
    /// rather than writing a file, because both are throwaways and a portable
    /// app should not scatter images beside itself — or leave them behind when
    /// it is killed.
    /// </remarks>
    private async Task<byte[]?> PipePngAsync(string args, CancellationToken ct)
    {
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

            // Disposing a Process closes a handle; it does not end the program.
            // Every path out of here that is not a clean exit — a cancellation,
            // the timeout below, a decode failure — has to end ffmpeg by hand,
            // or it sits forever blocked on a pipe nobody is draining and keeps
            // the source video open. Renders are cancelled constantly as the
            // selection moves down the list, so the strays accumulate.
            try
            {
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
            finally
            {
                EndProcess(process);
            }
        }
        catch (OperationCanceledException)
        {
            // Deliberately not folded into the catch below. "You canceled me"
            // and "this frame cannot be read" are different answers, and a
            // caller that caches results must not record the first as the
            // second — see ThumbnailService.GetAsync.
            throw;
        }
        catch
        {
            // A thumbnail and a waveform are both niceties. Failing to make one
            // must never surface as an error, let alone interrupt an edit — and a
            // file with no audio at all reaches this path, which is an ordinary
            // thing for a file to be rather than a fault.
            return null;
        }
    }

    /// <summary>
    /// Above this, a reported frame rate is treated as a measurement artefact
    /// rather than a rate anything was shot at.
    /// </summary>
    /// <remarks>
    /// 240 covers every real high-speed mode a consumer camera offers. The
    /// point is not to reject 300fps footage on principle — it is that
    /// <c>r_frame_rate</c> is the lowest rate that can express a stream's
    /// timestamps exactly, so a single irregular gap makes it report a number
    /// with no relation to playback. Encoding cost scales with it directly.
    /// </remarks>
    private const double MaxPlausibleFrameRate = 240.0;

    /// <summary>
    /// Pixel dimensions of the first video stream, or <c>(0, 0)</c> when they
    /// cannot be read.
    /// </summary>
    /// <remarks>
    /// Both values are forced even. H.264 with 4:2:0 chroma cannot encode an
    /// odd width or height, so an odd source used as the target size would fail
    /// every segment rather than the one file it came from.
    /// </remarks>
    public async Task<(int Width, int Height)> GetVideoSizeAsync(string filePath)
    {
        try
        {
            var args = "-v error -select_streams v:0 -show_entries stream=width,height " +
                       $"-of csv=p=0 \"{filePath}\"";
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
            if (p == null) return (0, 0);

            var outputTask = p.StandardOutput.ReadToEndAsync();
            var errorTask = p.StandardError.ReadToEndAsync();
            await Task.WhenAll(outputTask, errorTask);
            await p.WaitForExitAsync();

            if (p.ExitCode != 0) return (0, 0);

            var parts = outputTask.Result.Trim().Split(',');
            if (parts.Length < 2) return (0, 0);

            return int.TryParse(parts[0].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var w)
                   && int.TryParse(parts[1].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var h)
                   && w > 0 && h > 0
                ? (w - (w % 2), h - (h % 2))
                : (0, 0);
        }
        catch
        {
            return (0, 0);
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

    /// <summary>
    /// The video stream's nominal frame rate, as both the exact rational
    /// ffprobe reports and its decimal value. <c>("", 0)</c> when unreadable.
    /// </summary>
    /// <remarks>
    /// The rational is kept because the decimal cannot represent the broadcast
    /// rates: 30000/1001 is 29.97002997…, and handing ffmpeg a rounded "29.97"
    /// asks for a rate that no source actually has. Comparisons use the value;
    /// whatever is passed back to ffmpeg uses the text.
    /// </remarks>
    public async Task<(string Text, double Value)> GetFrameRateRationalAsync(string filePath)
    {
        try
        {
            var args = $"-v error -select_streams v:0 -show_entries stream=r_frame_rate " +
                       $"-of default=noprint_wrappers=1:nokey=1 \"{filePath}\"";
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
            if (p == null) return ("", 0);

            var outputTask = p.StandardOutput.ReadToEndAsync();
            var errorTask = p.StandardError.ReadToEndAsync();
            await Task.WhenAll(outputTask, errorTask);
            await p.WaitForExitAsync();

            if (p.ExitCode != 0) return ("", 0);

            var text = outputTask.Result.Trim();
            if (string.IsNullOrEmpty(text) || text == "0/0") return ("", 0);

            var slash = text.IndexOf('/');
            if (slash < 0)
                return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var plain) && plain > 0
                    ? (text, plain)
                    : ("", 0);

            return double.TryParse(text[..slash], NumberStyles.Float, CultureInfo.InvariantCulture, out var num)
                   && double.TryParse(text[(slash + 1)..], NumberStyles.Float, CultureInfo.InvariantCulture, out var den)
                   && den > 0 && num > 0
                ? (text, num / den)
                : ("", 0);
        }
        catch
        {
            return ("", 0);
        }
    }
}
