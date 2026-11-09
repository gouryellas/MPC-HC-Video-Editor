using System.IO;
using System.Linq;
using MpcHcVideoEditor.Services;
using Xunit;

namespace MpcHcVideoEditor.Tests;

/// <summary>
/// The duplicate scanner, over real files and real video.
/// </summary>
/// <remarks>
/// The exact pass is tested against files written here; the footage pass
/// against the generated fixtures, re-encoded so that the thing being
/// asserted — that a different encode of the same footage still matches — is
/// actually what is on disk.
/// </remarks>
public class DuplicateScannerTests
{
    private static string Scratch()
    {
        var dir = Path.Combine(Path.GetTempPath(), "mve-dupes-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static string Write(string dir, string name, byte[] bytes)
    {
        var path = Path.Combine(dir, name);
        File.WriteAllBytes(path, bytes);
        return path;
    }

    private static byte[] Filler(int length, byte seed)
    {
        var bytes = new byte[length];
        for (int i = 0; i < length; i++) bytes[i] = (byte)(seed + i % 251);
        return bytes;
    }

    private static DuplicateScanner Scanner() => new(new FFmpegService(null));

    [Fact]
    public async Task Finds_two_files_with_the_same_bytes()
    {
        var dir = Scratch();
        try
        {
            var bytes = Filler(300_000, 7);
            var a = Write(dir, "a.mp4", bytes);
            var b = Write(dir, "copy-of-a.mp4", bytes);
            Write(dir, "different.mp4", Filler(300_000, 9));

            var groups = await Scanner().FindIdenticalAsync(new[] { a, b, Path.Combine(dir, "different.mp4") });

            var group = Assert.Single(groups);
            Assert.Equal(DuplicateKind.Identical, group.Kind);
            Assert.Equal(100, group.Confidence);
            Assert.Equal(new[] { "a.mp4", "copy-of-a.mp4" },
                         group.Files.Select(f => f.Name).OrderBy(n => n));
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public async Task Same_length_but_different_content_is_not_a_duplicate()
    {
        // The case the cheap size check gets wrong on its own, and the whole
        // reason the ends are read before anything is reported.
        var dir = Scratch();
        try
        {
            var a = Write(dir, "a.mp4", Filler(200_000, 1));
            var b = Write(dir, "b.mp4", Filler(200_000, 2));

            Assert.Empty(await Scanner().FindIdenticalAsync(new[] { a, b }));
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public async Task Files_differing_only_in_the_middle_are_still_told_apart()
    {
        // Identical ends, identical length — this pair is only separated by
        // the full hash, which is the step the edge check exists to avoid
        // and must therefore not skip.
        var dir = Scratch();
        try
        {
            var one = Filler(400_000, 3);
            var two = (byte[])one.Clone();
            two[200_000] ^= 0xFF;

            var a = Write(dir, "a.mp4", one);
            var b = Write(dir, "b.mp4", two);

            Assert.Empty(await Scanner().FindIdenticalAsync(new[] { a, b }));
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public async Task Three_copies_come_back_as_one_group_of_three()
    {
        var dir = Scratch();
        try
        {
            var bytes = Filler(150_000, 11);
            var paths = new[] { "one.mp4", "two.mp4", "three.mp4" }
                        .Select(n => Write(dir, n, bytes)).ToArray();

            var group = Assert.Single(await Scanner().FindIdenticalAsync(paths));
            Assert.Equal(3, group.Files.Count);

            // Keeping one of three leaves two to recover.
            Assert.Equal(bytes.Length * 2, group.RecoverableBytes);
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public void A_lone_file_recovers_nothing()
    {
        var group = new DuplicateGroup(DuplicateKind.Identical,
            new[] { new DuplicateFile("a.mp4", 100, 0) }, 100);

        Assert.Equal(0, group.RecoverableBytes);
    }

    [Fact]
    public void Walking_a_folder_finds_videos_and_skips_everything_else()
    {
        var dir = Scratch();
        try
        {
            Write(dir, "clip.mp4", Filler(10, 1));
            Write(dir, "notes.csv", Filler(10, 1));
            Write(dir, "picture.png", Filler(10, 1));

            var nested = Path.Combine(dir, "inner");
            Directory.CreateDirectory(nested);
            Write(nested, "deep.mkv", Filler(10, 1));

            var all = DuplicateScanner.FindVideos(dir, recursive: true);
            Assert.Equal(new[] { "clip.mp4", "deep.mkv" },
                         all.Select(Path.GetFileName).OrderBy(n => n));

            var shallow = DuplicateScanner.FindVideos(dir, recursive: false);
            Assert.Equal(new[] { "clip.mp4" }, shallow.Select(Path.GetFileName));
        }
        finally { Directory.Delete(dir, true); }
    }

    /// <summary>
    /// Builds a clip once and caches it, like the other fixtures.
    /// </summary>
    /// <remarks>
    /// Its own source rather than TestVideos, because those are flat
    /// luminance ramps built to test timing. Every pixel in a frame of one
    /// is near enough the frame's mean, so an average hash of it is all
    /// zeroes — and every such clip would match every other, which would
    /// make this pass look like it worked while proving nothing. These have
    /// spatial detail, which is the thing being fingerprinted.
    /// </remarks>
    private static string Clip(string name, string recipe)
    {
        var root = Path.Combine(Path.GetTempPath(), "mve-dupe-fixtures");
        Directory.CreateDirectory(root);

        var path = Path.Combine(root, name);
        if (File.Exists(path) && new FileInfo(path).Length > 0) return path;

        var psi = new System.Diagnostics.ProcessStartInfo(Fixtures.TestVideos.Ffmpeg,
            $"-y -hide_banner -loglevel error {recipe} \"{path}\"")
        {
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var p = System.Diagnostics.Process.Start(psi)!;
        p.WaitForExit();
        return path;
    }

    /// <summary>A moving pattern with real detail in it.</summary>
    private static string Pattern => Clip("pattern.mp4",
        "-f lavfi -i \"testsrc2=s=320x240:r=25:d=3\" -c:v libx264 -pix_fmt yuv420p -crf 20");

    /// <summary>The same footage, half the size and badly encoded.</summary>
    private static string PatternReEncoded => Clip("pattern-small.mkv",
        $"-i \"{Pattern}\" -vf scale=160:120 -c:v libx264 -pix_fmt yuv420p -crf 40");

    /// <summary>Different footage, the same length.</summary>
    private static string Other => Clip("other.mp4",
        "-f lavfi -i \"mandelbrot=s=320x240:r=25\" -t 3 -c:v libx264 -pix_fmt yuv420p -crf 20");

    [Theory]
    [InlineData("pattern.mp4")]
    [InlineData("pattern-small.mkv")]
    public async Task A_frame_can_be_read_from_any_container(string which)
    {
        // Not really about duplicates. -ignore_editlist belongs to the MP4
        // demuxer, and ffmpeg refuses to open an input when handed an option
        // its demuxer has not declared — so while that flag went on every
        // input, every ffmpeg operation on a .mkv, .avi or .webm failed.
        // ffprobe accepted it, so durations still read and the fault stayed
        // hidden. This is the cheapest thing that notices.
        var path = which.EndsWith(".mkv") ? PatternReEncoded : Pattern;
        var pixels = await new FFmpegService(null).GreyThumbnailAsync(path, 1.0, 8);

        Assert.NotNull(pixels);
        Assert.True(pixels!.Length >= 64, $"got {pixels.Length} bytes for {which}");
    }

    [Fact]
    public async Task A_re_encoded_copy_is_recognised_as_the_same_footage()
    {
        // The claim the whole second pass rests on: a clip saved at another
        // size and bitrate still matches the original.
        var groups = await Scanner().FindSameFootageAsync(new[] { Pattern, PatternReEncoded });

        var group = Assert.Single(groups);
        Assert.Equal(DuplicateKind.SameFootage, group.Kind);
        Assert.Equal(2, group.Files.Count);
        Assert.True(group.Confidence >= 92, $"confidence was {group.Confidence}");
    }

    [Fact]
    public async Task Different_footage_of_the_same_length_is_left_alone()
    {
        // The mistake that matters. A false pair invites someone to delete
        // something that is not a copy.
        Assert.Empty(await Scanner().FindSameFootageAsync(new[] { Pattern, Other }));
    }

    [Fact]
    public async Task The_larger_copy_is_listed_first()
    {
        // The one a "keep the best" rule keeps, so the rest read as the
        // candidates for removal.
        var group = Assert.Single(await Scanner().FindSameFootageAsync(
            new[] { PatternReEncoded, Pattern }));

        Assert.Equal(Path.GetFileName(Pattern), group.Files[0].Name);
    }


    [Theory]
    // The case that went unreported: two copies of one encode, one byte
    // apart. The exact pass groups by exact length and never compares them.
    [InlineData(1_427_593_216L, 1_427_593_217L, true)]
    [InlineData(1_000_000L, 1_000_000L, true)]
    [InlineData(1_000_000L, 1_064_576L, true)]      // 64 KB, the floor
    [InlineData(1_000_000L, 1_200_000L, false)]     // a fifth bigger
    [InlineData(500L, 70_000L, false)]
    public void Sizes_within_a_rounding_error_count_as_one_encode(long a, long b, bool near)
    {
        Assert.Equal(near, DuplicateScanner.Near(a, b));
        Assert.Equal(near, DuplicateScanner.Near(b, a));
    }

    [Fact]
    public async Task A_copy_differing_by_one_byte_is_found()
    {
        // The complaint, end to end. These are real videos of the same
        // length, one with a byte appended — which is what a rewritten
        // container index looks like from outside.
        var dir = Scratch();
        try
        {
            var a = Path.Combine(dir, "a.mp4");
            var b = Path.Combine(dir, "b.mp4");

            File.Copy(Pattern, a);
            File.Copy(Pattern, b);
            using (var tail = new FileStream(b, FileMode.Append)) tail.WriteByte(0);

            Assert.Empty(await Scanner().FindIdenticalAsync(new[] { a, b }));

            var group = Assert.Single(await Scanner().FindNearIdenticalAsync(new[] { a, b }));
            Assert.Equal(DuplicateKind.NearIdentical, group.Kind);
            Assert.Equal(2, group.Files.Count);
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public async Task Two_different_videos_of_a_similar_size_are_not_paired()
    {
        // Size alone proves nothing — running time is what makes the
        // near-identical pass safe.
        var dir = Scratch();
        try
        {
            var a = Path.Combine(dir, "a.mp4");
            File.Copy(Pattern, a);

            // Padded to within a rounding error of the other clip's size.
            var b = Path.Combine(dir, "b.mp4");
            File.Copy(Other, b);

            // Pad whichever is smaller up to the other, so the two agree on
            // size and on running time and the only thing left to tell them
            // apart is what is in the frames.
            var smaller = new FileInfo(a).Length < new FileInfo(b).Length ? a : b;
            var larger = smaller == a ? b : a;
            var difference = new FileInfo(larger).Length - new FileInfo(smaller).Length;

            using (var tail = new FileStream(smaller, FileMode.Append))
                tail.Write(new byte[difference]);

            Assert.True(DuplicateScanner.Near(new FileInfo(a).Length, new FileInfo(b).Length),
                        "the two files should be the same size for this test to mean anything");

            Assert.Empty(await Scanner().FindNearIdenticalAsync(new[] { a, b }));
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public void An_identical_fingerprint_is_a_hundred_percent()
    {
        var print = new ulong[] { 0xDEADBEEF, 0x1234, 0, ulong.MaxValue };

        Assert.Equal(100, DuplicateScanner.Similarity(print, print));
    }

    [Fact]
    public void An_inverted_fingerprint_shares_nothing()
    {
        var a = new ulong[] { 0, 0 };
        var b = new ulong[] { ulong.MaxValue, ulong.MaxValue };

        Assert.Equal(0, DuplicateScanner.Similarity(a, b));
    }

    [Fact]
    public void One_wrong_bit_is_not_a_mismatch()
    {
        // Re-encoding moves a few pixels either side of the mean. The
        // threshold has to survive that or the second pass finds nothing.
        var a = new ulong[] { 0, 0, 0, 0 };
        var b = new ulong[] { 1, 0, 0, 0 };

        Assert.True(DuplicateScanner.Similarity(a, b) >= 99);
    }

    [Fact]
    public void A_flat_frame_hashes_to_nothing_set()
    {
        // Every pixel equal to the mean, so no pixel is brighter than it —
        // a black or blank frame must not read as half its bits set.
        var flat = Enumerable.Repeat((byte)128, 64).ToArray();

        Assert.Equal(0UL, DuplicateScanner.AverageHash(flat));
    }

    [Fact]
    public void A_split_frame_sets_the_bright_half()
    {
        var pixels = Enumerable.Repeat((byte)0, 32).Concat(Enumerable.Repeat((byte)255, 32)).ToArray();

        var bits = DuplicateScanner.AverageHash(pixels);

        Assert.Equal(32, System.Numerics.BitOperations.PopCount(bits));
    }
}
