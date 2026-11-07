using System.IO;
using System.Diagnostics;
using System.Globalization;

namespace MpcHcVideoEditor.Tests.Fixtures;

/// <summary>
/// Short videos built on demand by ffmpeg, each shaped to break a particular
/// assumption.
/// </summary>
/// <remarks>
/// Generated rather than committed. A handful of two-second clips is a few
/// megabytes of binary that would have to be regenerated anyway the moment a
/// test needed a shape nobody had thought of — and the recipe is the useful
/// part, since it says what each file is <em>for</em>.
///
/// Cached under the temp folder and reused, so a full run builds them once.
/// Delete the folder to start again.
///
/// Every frame's brightness is its own index times five, so a test can take a
/// single pixel and say which frame came back. That is the whole trick: it
/// turns "is this the right picture" into an integer comparison, where the
/// alternative is a human looking at two stills and deciding.
/// </remarks>
public static class TestVideos
{
    private static readonly string Root =
        Path.Combine(Path.GetTempPath(), "mve-test-fixtures");

    /// <summary>25fps, evenly spaced, starting at zero. The ordinary case.</summary>
    public static string ConstantRate => Build("constant.mp4",
        "-f lavfi -i \"color=c=black:s=128x128:r=25:d=2\" " +
        "-vf \"geq=lum='N*5':cb=128:cr=128\" " +
        "-c:v libx264 -pix_fmt yuv420p -g 25");

    /// <summary>
    /// Frames at irregular intervals, while the container still claims 25fps.
    /// </summary>
    /// <remarks>
    /// The shape that broke a frame-accuracy fix built on the frame rate:
    /// r_frame_rate is the lowest rate that can represent every timestamp
    /// exactly, not a rate anything plays at, so a grid derived from it put
    /// the frame a quarter of a second into the future.
    /// </remarks>
    public static string VariableRate => Build("variable.mp4",
        "-f lavfi -i \"color=c=black:s=64x64:r=25:d=2\" " +
        "-vf \"geq=lum='N*5',setpts='PTS*(1+0.5*sin(N))'\" " +
        "-c:v libx264 -pix_fmt yuv420p");

    /// <summary>A stream whose clock starts at ten seconds, not zero.</summary>
    /// <remarks>
    /// A capture can open part-way into a timeline. Comparing the player's
    /// clock against the file's directly then finds no frame at all, and the
    /// code that does so silently falls back to its old behaviour.
    /// </remarks>
    public static string OffsetStart => Build("offset.mp4",
        "-f lavfi -i \"color=c=black:s=64x64:r=25:d=2\" " +
        "-vf \"geq=lum='N*5'\" " +
        "-c:v libx264 -pix_fmt yuv420p -output_ts_offset 10");

    /// <summary>
    /// Carries an edit list beginning half a second in, so the presentation
    /// and the media disagree about what time it is.
    /// </summary>
    /// <remarks>
    /// ffmpeg honours an edit list and MPC-HC ignores it. On a real file whose
    /// list began 7.5 seconds in, that disagreement put every saved frame and
    /// every cut clip seven and a half seconds away from what was on screen.
    /// Half a second here keeps the fixture small; the arithmetic is the same.
    /// </remarks>
    public static string WithEditList => Build("editlist.mp4",
        "-f lavfi -i \"color=c=black:s=64x64:r=25:d=2\" " +
        "-vf \"geq=lum='N*5'\" " +
        "-c:v libx264 -pix_fmt yuv420p -ss 0.5 -movflags +faststart",
        viaEditList: true);

    /// <summary>
    /// The clip <see cref="WithEditList"/> was trimmed from, with no edit
    /// list of its own.
    /// </summary>
    /// <remarks>
    /// The reference for "what should the player be showing". Both files hold
    /// the same frames; only one of them carries a note claiming the
    /// presentation starts late.
    /// </remarks>
    public static string WithEditListSource
    {
        get
        {
            _ = WithEditList;                       // builds the pair
            return Path.Combine(Root, "editlist-source.mp4");
        }
    }

    /// <summary>Where ffmpeg and ffprobe live for the tests.</summary>
    public static string FfmpegFolder => FindTools();

    public static string Ffmpeg => Path.Combine(FfmpegFolder, "ffmpeg.exe");
    public static string Ffprobe => Path.Combine(FfmpegFolder, "ffprobe.exe");

    /// <summary>
    /// The frame index a still came from, by the one pixel that encodes it.
    /// −1 when the picture is not from this clip.
    /// </summary>
    /// <remarks>
    /// Looked up against the clip rather than calculated from the pixel.
    /// The brightness written into each frame does not survive encoding
    /// unchanged — a limited-range Y plane expanded on the way back out puts
    /// frame 10 at 40 rather than the 50 it was given — and the exact
    /// transform depends on the pixel format, the range flags, and what the
    /// decoder decides to do about them. Measuring the clip once sidesteps
    /// all of that, and cannot drift when any of it changes.
    /// </remarks>
    public static int FrameIndexOf(string video, byte[] png)
    {
        var lum = FirstPixelLuminance(png);
        if (lum < 0) return -1;

        var table = Brightnesses(video);
        var best = -1;
        var closest = int.MaxValue;

        for (var i = 0; i < table.Count; i++)
        {
            var distance = Math.Abs(table[i] - lum);

            if (distance < closest)
            {
                closest = distance;
                best = i;
            }
        }

        // Frames at the dark end are indistinguishable: everything the clip
        // wrote below the bottom of the legal range comes back as zero. A tie
        // means the answer is not knowable, so say so rather than guess.
        var ties = table.Count(v => Math.Abs(v - lum) == closest);
        return ties > 1 ? -1 : best;
    }

    private static readonly Dictionary<string, IReadOnlyList<int>> BrightnessCache = new();

    /// <summary>What each frame of a clip actually reads as, in order.</summary>
    private static IReadOnlyList<int> Brightnesses(string video)
    {
        lock (BrightnessCache)
        {
            if (BrightnessCache.TryGetValue(video, out var cached)) return cached;

            // One decode of the whole clip, one byte per frame. The fixtures
            // are two seconds long, so this costs nothing and is done once.
            var psi = new ProcessStartInfo(Ffmpeg,
                $"-hide_banner -loglevel error -i \"{video}\" -vf scale=1:1 " +
                $"-f rawvideo -pix_fmt gray -")
            {
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var p = Process.Start(psi)
                ?? throw new InvalidOperationException("Could not start ffmpeg");

            using var memory = new MemoryStream();
            p.StandardOutput.BaseStream.CopyTo(memory);
            p.WaitForExit();

            var values = memory.ToArray().Select(b => (int)b).ToList();
            BrightnessCache[video] = values;
            return values;
        }
    }

    /// <summary>Every frame's own timestamp, in order, as the file reports them.</summary>
    public static IReadOnlyList<double> FrameTimes(string video, bool ignoreEditList = false)
    {
        var flag = ignoreEditList ? "-ignore_editlist 1 " : string.Empty;
        var text = Run(Ffprobe,
            $"-v error {flag}-select_streams v:0 " +
            $"-show_entries frame=best_effort_timestamp_time -of csv=p=0 \"{video}\"");

        var times = new List<double>();

        foreach (var token in text.Replace(",", " ").Split((char[]?)null,
                     StringSplitOptions.RemoveEmptyEntries))
        {
            if (double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out var t))
                times.Add(t);
        }

        times.Sort();
        return times;
    }

    // ------------------------------------------------------------------

    private static string Build(string name, string recipe, bool viaEditList = false)
    {
        Directory.CreateDirectory(Root);
        var path = Path.Combine(Root, name);

        if (File.Exists(path) && new FileInfo(path).Length > 0) return path;

        // An edit list cannot be asked for directly. Writing a file and then
        // trimming its front with a stream copy is what produces one: the
        // frames stay where they are and the container gains a note saying
        // where the presentation begins.
        if (viaEditList)
        {
            var whole = Path.Combine(Root, "editlist-source.mp4");

            if (!File.Exists(whole))
            {
                Run(Ffmpeg, $"-hide_banner -loglevel error -y " +
                            $"-f lavfi -i \"color=c=black:s=64x64:r=25:d=2\" " +
                            $"-vf \"geq=lum='N*5'\" -c:v libx264 -pix_fmt yuv420p \"{whole}\"");
            }

            Run(Ffmpeg, $"-hide_banner -loglevel error -y -ss 0.5 -i \"{whole}\" -c copy \"{path}\"");
            return path;
        }

        Run(Ffmpeg, $"-hide_banner -loglevel error -y {recipe} \"{path}\"");
        return path;
    }

    /// <summary>
    /// The luminance of the top-left pixel, decoded by ffmpeg rather than by
    /// WPF — the tests should not need a UI thread to read a picture.
    /// </summary>
    private static int FirstPixelLuminance(byte[] png)
    {
        var temp = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".png");

        try
        {
            File.WriteAllBytes(temp, png);

            var psi = new ProcessStartInfo(Ffmpeg,
                $"-hide_banner -loglevel error -i \"{temp}\" -frames:v 1 -f rawvideo -pix_fmt gray -")
            {
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var p = Process.Start(psi);
            if (p is null) return -1;

            var first = p.StandardOutput.BaseStream.ReadByte();
            p.StandardOutput.BaseStream.CopyTo(Stream.Null);
            p.WaitForExit();

            return first;
        }
        catch
        {
            return -1;
        }
        finally
        {
            try { File.Delete(temp); } catch { /* a temp file that outlives the test is harmless */ }
        }
    }

    private static string Run(string exe, string args)
    {
        var psi = new ProcessStartInfo(exe, args)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var p = Process.Start(psi)
            ?? throw new InvalidOperationException($"Could not start {exe}");

        var output = p.StandardOutput.ReadToEnd();
        var error = p.StandardError.ReadToEnd();
        p.WaitForExit();

        if (p.ExitCode != 0 && output.Length == 0)
            throw new InvalidOperationException($"{Path.GetFileName(exe)} failed: {error}");

        return output;
    }

    /// <summary>
    /// Finds the ffmpeg pair, searching the same places the application does
    /// plus the portable build, so a checkout that has published once has
    /// everything the tests need.
    /// </summary>
    private static string FindTools()
    {
        var here = AppContext.BaseDirectory;

        for (var dir = new DirectoryInfo(here); dir is not null; dir = dir.Parent)
        {
            foreach (var candidate in new[]
                     {
                         dir.FullName,
                         Path.Combine(dir.FullName, "publish", "portable"),
                         Path.Combine(dir.FullName, "ffmpeg")
                     })
            {
                if (File.Exists(Path.Combine(candidate, "ffmpeg.exe")) &&
                    File.Exists(Path.Combine(candidate, "ffprobe.exe")))
                    return candidate;
            }
        }

        throw new InvalidOperationException(
            "ffmpeg.exe and ffprobe.exe were not found. The tests look beside the " +
            "test assembly, in publish\\portable, and in an ffmpeg subfolder of any " +
            "parent directory.");
    }
}
