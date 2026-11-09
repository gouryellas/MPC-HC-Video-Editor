using System.IO;
using System.Security.Cryptography;
using MpcHcVideoEditor.Helpers;

namespace MpcHcVideoEditor.Services;

/// <summary>How a group of files was found to match.</summary>
public enum DuplicateKind
{
    /// <summary>Byte for byte. A match is a fact, not a judgement.</summary>
    Identical,

    /// <summary>
    /// The same footage encoded differently — a different size, bitrate or
    /// container. Judged from sampled frames, so it is a strong suggestion
    /// rather than a certainty.
    /// </summary>
    SameFootage
}

/// <summary>One file a scan found.</summary>
public sealed class DuplicateFile
{
    public DuplicateFile(string path, long bytes, double seconds)
    {
        Path = path;
        Bytes = bytes;
        Seconds = seconds;
    }

    public string Path { get; }
    public string Name => System.IO.Path.GetFileName(Path);
    public string Folder => System.IO.Path.GetDirectoryName(Path) ?? "";
    public long Bytes { get; }

    /// <summary>Length in seconds; zero when it could not be read.</summary>
    public double Seconds { get; }

    public string SizeDisplay => Bytes >= 1024L * 1024 * 1024
        ? $"{Bytes / 1024.0 / 1024 / 1024:0.##} GB"
        : $"{Bytes / 1024.0 / 1024:0.#} MB";
}

/// <summary>A set of files that match each other.</summary>
public sealed class DuplicateGroup
{
    public DuplicateGroup(DuplicateKind kind, IReadOnlyList<DuplicateFile> files, int confidence)
    {
        Kind = kind;
        Files = files;
        Confidence = confidence;
    }

    public DuplicateKind Kind { get; }
    public IReadOnlyList<DuplicateFile> Files { get; }

    /// <summary>
    /// How alike the sampled frames were, as a percentage. Always 100 for an
    /// identical group, where it is not an estimate.
    /// </summary>
    public int Confidence { get; }

    /// <summary>
    /// What could be freed by keeping one of them — every file but the
    /// largest, which is the one a "keep the best copy" rule would keep.
    /// </summary>
    public long RecoverableBytes =>
        Files.Count < 2 ? 0 : Files.Sum(f => f.Bytes) - Files.Max(f => f.Bytes);
}

/// <summary>Where a scan has got to.</summary>
/// <param name="Stage">What it is doing, for the label.</param>
/// <param name="Done">Files finished.</param>
/// <param name="Total">Files to do, or 0 while still counting.</param>
public sealed record DuplicateProgress(string Stage, int Done, int Total)
{
    public double Percent => Total > 0 ? Math.Min(100, (double)Done / Total * 100) : 0;
}

/// <summary>
/// Finds files in a folder that are copies of each other.
/// </summary>
/// <remarks>
/// <para>
/// Two passes, because "duplicate" means two different things and only one of
/// them can be answered for certain. The first finds files that are byte for
/// byte the same — a fact. The second looks for the same footage encoded
/// differently, which can only ever be a strong suggestion, and is therefore
/// reported separately and never acted on without being looked at.
/// </para>
/// <para>
/// Nothing here deletes anything. The scan reports; what to do about it is
/// the caller's business, and the user's.
/// </para>
/// </remarks>
public sealed class DuplicateScanner
{
    private readonly FFmpegService _ffmpeg;

    public DuplicateScanner(FFmpegService ffmpeg) => _ffmpeg = ffmpeg;

    /// <summary>
    /// How much of each end of a file is read before committing to hashing
    /// all of it. Two videos of exactly the same length that differ at all
    /// almost always differ in their header or their tail, so this settles
    /// most non-matches for 128 KB instead of gigabytes.
    /// </summary>
    private const int EdgeBytes = 64 * 1024;

    /// <summary>
    /// Frames sampled from each video for the second pass. Four is enough to
    /// separate different footage of the same length, and each one costs a
    /// seek and a decode.
    /// </summary>
    private static readonly double[] SamplePoints = { 0.15, 0.35, 0.55, 0.75 };

    /// <summary>
    /// How far apart two lengths may be and still be compared. Re-encoding
    /// can shift a duration by a frame or two; anything beyond a second is a
    /// different cut, not a different encode.
    /// </summary>
    private const double DurationToleranceSeconds = 1.0;

    /// <summary>
    /// How alike two fingerprints must be to be reported, as a percentage of
    /// matching bits.
    /// </summary>
    /// <remarks>
    /// Deliberately high. The cost of a missed pair is that the user keeps a
    /// file they might have deleted; the cost of a false pair is that they
    /// are invited to delete something that is not a copy. Those are not the
    /// same mistake.
    /// </remarks>
    private const int SimilarityThreshold = 92;

    /// <summary>
    /// Finds files that are byte for byte the same.
    /// </summary>
    /// <remarks>
    /// Three narrowing steps, each more expensive than the last and run on
    /// fewer files: group by length, then by the two ends, then by a full
    /// hash. A folder where nothing matches never reads a whole file.
    /// </remarks>
    public async Task<IReadOnlyList<DuplicateGroup>> FindIdenticalAsync(
        IReadOnlyList<string> files,
        IProgress<DuplicateProgress>? progress = null,
        CancellationToken ct = default)
    {
        var bySize = files
            .Select(TryDescribe)
            .Where(f => f is not null && f!.Bytes > 0)
            .GroupBy(f => f!.Bytes)
            .Where(g => g.Count() > 1)
            .ToList();

        var candidates = bySize.SelectMany(g => g).ToList();
        var groups = new List<DuplicateGroup>();
        var done = 0;

        foreach (var sameSize in bySize)
        {
            ct.ThrowIfCancellationRequested();

            // Same length is not the same file; the ends usually settle it.
            var byEdges = new Dictionary<string, List<DuplicateFile>>();

            foreach (var file in sameSize)
            {
                ct.ThrowIfCancellationRequested();
                progress?.Report(new DuplicateProgress("Comparing files", ++done, candidates.Count));

                var key = await Task.Run(() => EdgeHash(file!.Path), ct);
                if (key is null) continue;

                if (!byEdges.TryGetValue(key, out var bucket))
                    byEdges[key] = bucket = new List<DuplicateFile>();

                bucket.Add(file!);
            }

            foreach (var bucket in byEdges.Values.Where(b => b.Count > 1))
            {
                ct.ThrowIfCancellationRequested();

                // Only now is reading everything worth it.
                var byContent = new Dictionary<string, List<DuplicateFile>>();

                foreach (var file in bucket)
                {
                    var hash = await Task.Run(() => FullHash(file.Path), ct);
                    if (hash is null) continue;

                    if (!byContent.TryGetValue(hash, out var exact))
                        byContent[hash] = exact = new List<DuplicateFile>();

                    exact.Add(file);
                }

                groups.AddRange(byContent.Values
                    .Where(g => g.Count > 1)
                    .Select(g => new DuplicateGroup(DuplicateKind.Identical, Ordered(g), 100)));
            }
        }

        return groups;
    }

    /// <summary>
    /// Finds videos that look like the same footage encoded differently.
    /// </summary>
    /// <param name="files">
    /// What to examine. The caller is expected to have removed anything the
    /// exact pass already accounted for — there is no sense fingerprinting a
    /// file that is already known to be a copy.
    /// </param>
    /// <remarks>
    /// Only files of near enough the same length are compared, which is both
    /// the obvious filter and the one that keeps this from being quadratic
    /// over the whole folder.
    /// </remarks>
    public async Task<IReadOnlyList<DuplicateGroup>> FindSameFootageAsync(
        IReadOnlyList<string> files,
        IProgress<DuplicateProgress>? progress = null,
        CancellationToken ct = default)
    {
        var described = new List<DuplicateFile>();
        var done = 0;

        foreach (var path in files)
        {
            ct.ThrowIfCancellationRequested();
            progress?.Report(new DuplicateProgress("Reading lengths", ++done, files.Count));

            var info = TryDescribe(path);
            if (info is null) continue;

            double seconds;
            try { seconds = await _ffmpeg.GetDurationAsync(path); }
            catch (Exception ex) { Diagnostics.Swallowed("DuplicateScan.Duration", ex); continue; }

            if (seconds > 0) described.Add(new DuplicateFile(path, info.Bytes, seconds));
        }

        // Files of a comparable length, in runs. Anything alone in its run
        // cannot match anything and is never decoded.
        var buckets = new List<List<DuplicateFile>>();
        foreach (var file in described.OrderBy(f => f.Seconds))
        {
            var last = buckets.LastOrDefault();
            if (last is not null && Math.Abs(last[^1].Seconds - file.Seconds) <= DurationToleranceSeconds)
                last.Add(file);
            else
                buckets.Add(new List<DuplicateFile> { file });
        }

        var comparable = buckets.Where(b => b.Count > 1).ToList();
        var toFingerprint = comparable.Sum(b => b.Count);
        var groups = new List<DuplicateGroup>();
        done = 0;

        foreach (var bucket in comparable)
        {
            ct.ThrowIfCancellationRequested();

            var prints = new List<(DuplicateFile File, ulong[] Print)>();

            foreach (var file in bucket)
            {
                ct.ThrowIfCancellationRequested();
                progress?.Report(new DuplicateProgress("Comparing footage", ++done, toFingerprint));

                var print = await FingerprintAsync(file, ct);
                if (print is not null) prints.Add((file, print));
            }

            // Simple agglomeration: each file joins the first group it is
            // close enough to. Good enough for copies of one original, which
            // is what this is looking for.
            var open = new List<(List<DuplicateFile> Files, ulong[] Print, List<int> Scores)>();

            foreach (var (file, print) in prints)
            {
                var home = open.FirstOrDefault(g => Similarity(g.Print, print) >= SimilarityThreshold);

                if (home.Files is null)
                    open.Add((new List<DuplicateFile> { file }, print, new List<int>()));
                else
                {
                    home.Scores.Add(Similarity(home.Print, print));
                    home.Files.Add(file);
                }
            }

            groups.AddRange(open
                .Where(g => g.Files.Count > 1)
                .Select(g => new DuplicateGroup(
                    DuplicateKind.SameFootage,
                    Ordered(g.Files),
                    g.Scores.Count > 0 ? g.Scores.Min() : SimilarityThreshold)));
        }

        return groups;
    }

    /// <summary>
    /// Largest first, so the one a "keep the best" rule keeps is at the top
    /// and the rest read as the candidates for removal.
    /// </summary>
    private static IReadOnlyList<DuplicateFile> Ordered(IEnumerable<DuplicateFile> files) =>
        files.OrderByDescending(f => f.Bytes).ThenBy(f => f.Path).ToList();

    private static DuplicateFile? TryDescribe(string path)
    {
        try
        {
            var info = new FileInfo(path);
            return info.Exists ? new DuplicateFile(path, info.Length, 0) : null;
        }
        catch (Exception ex)
        {
            Diagnostics.Swallowed("DuplicateScan.Describe", ex);
            return null;
        }
    }

    /// <summary>Both ends of a file, which is what tells copies apart cheaply.</summary>
    private static string? EdgeHash(string path)
    {
        try
        {
            using var stream = File.OpenRead(path);
            using var sha = SHA256.Create();

            var head = new byte[(int)Math.Min(EdgeBytes, stream.Length)];
            stream.ReadExactly(head);
            sha.TransformBlock(head, 0, head.Length, null, 0);

            if (stream.Length > EdgeBytes * 2)
            {
                stream.Seek(-EdgeBytes, SeekOrigin.End);
                var tail = new byte[EdgeBytes];
                stream.ReadExactly(tail);
                sha.TransformBlock(tail, 0, tail.Length, null, 0);
            }

            sha.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
            return Convert.ToHexString(sha.Hash!);
        }
        catch (Exception ex)
        {
            Diagnostics.Swallowed("DuplicateScan.EdgeHash", ex);
            return null;
        }
    }

    private static string? FullHash(string path)
    {
        try
        {
            using var stream = File.OpenRead(path);
            using var sha = SHA256.Create();
            return Convert.ToHexString(sha.ComputeHash(stream));
        }
        catch (Exception ex)
        {
            Diagnostics.Swallowed("DuplicateScan.FullHash", ex);
            return null;
        }
    }

    /// <summary>
    /// A short signature built from a few frames, one 64-bit average hash per
    /// sample.
    /// </summary>
    /// <remarks>
    /// Each frame is reduced to 8x8 greyscale and turned into a bit per
    /// pixel: set if the pixel is brighter than the frame's own mean. That
    /// survives a change of resolution, bitrate and container — which is the
    /// whole point — while still differing completely between unrelated
    /// shots.
    /// </remarks>
    private async Task<ulong[]?> FingerprintAsync(DuplicateFile file, CancellationToken ct)
    {
        try
        {
            var print = new ulong[SamplePoints.Length];

            for (int i = 0; i < SamplePoints.Length; i++)
            {
                var at = file.Seconds * SamplePoints[i];
                var pixels = await _ffmpeg.GreyThumbnailAsync(file.Path, at, 8, ct);

                if (pixels is null || pixels.Length < 64) return null;
                print[i] = AverageHash(pixels);
            }

            return print;
        }
        catch (Exception ex)
        {
            Diagnostics.Swallowed("DuplicateScan.Fingerprint", ex);
            return null;
        }
    }

    /// <summary>One bit per pixel: brighter than the frame's mean, or not.</summary>
    internal static ulong AverageHash(byte[] pixels)
    {
        var mean = pixels.Take(64).Select(p => (int)p).Sum() / 64.0;
        ulong bits = 0;

        for (int i = 0; i < 64; i++)
            if (pixels[i] > mean) bits |= 1UL << i;

        return bits;
    }

    /// <summary>
    /// How alike two signatures are, as a percentage of matching bits.
    /// </summary>
    internal static int Similarity(ulong[] a, ulong[] b)
    {
        if (a.Length == 0 || a.Length != b.Length) return 0;

        var same = 0;
        for (int i = 0; i < a.Length; i++)
            same += 64 - System.Numerics.BitOperations.PopCount(a[i] ^ b[i]);

        return (int)Math.Round(same * 100.0 / (a.Length * 64));
    }

    /// <summary>
    /// Every video under a folder, in a stable order.
    /// </summary>
    /// <remarks>
    /// Folders that cannot be read are stepped over rather than ending the
    /// walk — one permission-denied directory should not cost the scan the
    /// rest of the tree.
    /// </remarks>
    public static IReadOnlyList<string> FindVideos(string folder, bool recursive, CancellationToken ct = default)
    {
        var found = new List<string>();
        var pending = new Stack<string>();
        pending.Push(folder);

        while (pending.Count > 0)
        {
            ct.ThrowIfCancellationRequested();
            var dir = pending.Pop();

            try
            {
                foreach (var file in Directory.EnumerateFiles(dir))
                    if (IsVideo(file)) found.Add(file);

                if (recursive)
                    foreach (var sub in Directory.EnumerateDirectories(dir))
                        pending.Push(sub);
            }
            catch (Exception ex)
            {
                Diagnostics.Swallowed("DuplicateScan.Walk", ex);
            }
        }

        found.Sort(StringComparer.OrdinalIgnoreCase);
        return found;
    }

    private static readonly string[] VideoExtensions =
        { ".mp4", ".mkv", ".avi", ".mov", ".wmv", ".webm", ".mpeg", ".mpg", ".ts", ".m4v", ".flv", ".m2ts" };

    private static bool IsVideo(string path) =>
        VideoExtensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase);
}
