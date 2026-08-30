using System.IO;
using System.Windows.Media.Imaging;

namespace MpcHcVideoEditor.Services;

/// <summary>
/// Turns a moment in a video into a small image, and remembers the ones it has
/// already made.
/// </summary>
/// <remarks>
/// <para>
/// Exists so a bookmark's in and out points can be looked at before committing
/// to an encode. A pair of timestamps says a range is three minutes long; it
/// does not say whether the range is the right three minutes.
/// </para>
/// <para>
/// Only the selected bookmark is rendered, not every row. Two frames per
/// selection is a cost nobody notices; two frames per bookmark across a long
/// list is hundreds of ffmpeg launches and a lot of held bitmaps, for pictures
/// that are mostly scrolled past.
/// </para>
/// </remarks>
public sealed class ThumbnailService
{
    private readonly FFmpegService _ffmpeg;

    /// <summary>
    /// Rendered frames, keyed by video and timestamp. Null values are cached
    /// too: a frame that could not be read will not read any better on the
    /// next selection change, and retrying it on every click would launch
    /// ffmpeg over and over for nothing.
    /// </summary>
    private readonly Dictionary<string, BitmapSource?> _cache = new();

    /// <summary>
    /// Enough entries to cover moving around a list without re-rendering, and
    /// few enough that the bitmaps stay incidental. Passing it clears the lot
    /// rather than evicting cleverly — this is a convenience cache, and the
    /// cost of a miss is a few hundred milliseconds.
    /// </summary>
    private const int MaxEntries = 96;

    public ThumbnailService(FFmpegService ffmpeg) => _ffmpeg = ffmpeg;

    /// <summary>Height of a rendered thumbnail, in pixels. Width follows the aspect ratio.</summary>
    public int Height { get; set; } = 76;

    /// <summary>
    /// The frame at <paramref name="seconds"/>, or <c>null</c> when it cannot
    /// be produced.
    /// </summary>
    public async Task<BitmapSource?> GetAsync(string? videoPath, double seconds, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(videoPath) || !File.Exists(videoPath)) return null;

        var key = $"{videoPath}|{Height}|{seconds:0.###}";
        lock (_cache)
            if (_cache.TryGetValue(key, out var cached)) return cached;

        // ConfigureAwait(false) here and below: rendering a thumbnail has no
        // business resuming on the UI thread, which the stall log watches.
        var png = await _ffmpeg.ExtractFrameAsync(videoPath, seconds, Height, ct).ConfigureAwait(false);

        // Never record the outcome of an interrupted render. Moving through a
        // list cancels renders constantly, and caching those nulls left the
        // frame permanently blank for any clip the user passed over quickly —
        // the cache turned a momentary interruption into a lasting one.
        if (ct.IsCancellationRequested) return null;

        var image = png is null ? null : Decode(png);

        lock (_cache)
        {
            if (_cache.Count >= MaxEntries) _cache.Clear();
            _cache[key] = image;
        }

        return image;
    }

    /// <summary>
    /// Drops every cached frame. Called when the loaded video changes, since
    /// nothing cached can still be wanted.
    /// </summary>
    public void Clear()
    {
        lock (_cache) _cache.Clear();
    }

    /// <summary>
    /// Decodes PNG bytes into a bitmap safe to hand to any thread.
    /// </summary>
    /// <remarks>
    /// <c>OnLoad</c> plus <c>Freeze</c> matter here: without the first the
    /// bitmap keeps the stream open, and without the second it belongs to the
    /// thread that built it and cannot be shown by the UI thread that asked
    /// for it.
    /// </remarks>
    private static BitmapSource? Decode(byte[] png)
    {
        try
        {
            using var stream = new MemoryStream(png);
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.StreamSource = stream;
            image.EndInit();
            image.Freeze();
            return image;
        }
        catch
        {
            return null;
        }
    }
}
