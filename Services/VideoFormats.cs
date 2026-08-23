namespace MpcHcVideoEditor.Services;

/// <summary>
/// The container formats video operations can write, and the ffmpeg codec
/// arguments each one needs.
/// </summary>
/// <remarks>
/// A container is not just a file extension. H.264 video and AAC audio — what
/// the segment cutter produces — are legal in MP4, MKV and MOV, but WebM
/// accepts neither, MPEG-1/2 program streams accept neither, and ASF (WMV)
/// only tolerates them. Renaming the output to <c>.webm</c> and hoping would
/// simply fail the mux, so every format carries the encoder flags that
/// actually produce a valid file in it.
///
/// <see cref="CanCopyH264"/> marks the three formats whose mux accepts the
/// intermediate segments untouched. For those the final concat is a stream
/// copy — instant, and lossless. The rest re-encode once at the concat step,
/// which is the price of the container.
/// </remarks>
public static class VideoFormats
{
    /// <param name="Key">Stable identifier persisted in settings.json.</param>
    /// <param name="Extension">Including the leading dot.</param>
    /// <param name="Display">Shown in the settings dialog.</param>
    /// <param name="EncodeArgs">ffmpeg output flags producing a valid file.</param>
    /// <param name="CanCopyH264">
    /// True when H.264/AAC can be muxed in as-is, letting the concat step
    /// stream-copy instead of re-encoding.
    /// </param>
    public sealed record Format(
        string Key,
        string Extension,
        string Display,
        string EncodeArgs,
        bool CanCopyH264);

    /// <summary>
    /// Placeholder replaced with the quality preset and CRF chosen in
    /// Settings. Kept as a token rather than baked in so the format table
    /// stays about containers and codecs, which is what varies per format,
    /// while effort — which does not — is decided in one place.
    /// </summary>
    public const string QualityToken = "{quality}";

    /// <summary>
    /// Placeholder replaced with the H.264 encoder chosen in Settings — x264 on
    /// the CPU, or one of the GPU encoders. Only the H.264 formats carry it;
    /// the legacy containers name their own codecs outright, because there is
    /// nothing to choose.
    /// </summary>
    public const string VideoCodecToken = "{vcodec}";

    private const string H264 =
        "-c:v " + VideoCodecToken + " " + QualityToken +
        " -pix_fmt yuv420p -c:a aac -b:a 192k -ar 48000 -ac 2";

    /// <summary>Offered formats, in the order the settings dialog lists them.</summary>
    /// <remarks>
    /// MP4 is first and is the default: it is the only one of these that plays
    /// everywhere without qualification.
    ///
    /// The older formats are deliberately conservative. AVI gets MPEG-4 Part 2
    /// rather than H.264 — H.264 in AVI is technically expressible but widely
    /// mishandled, and an AVI is usually asked for precisely because something
    /// old has to read it. Same reasoning for MPEG-2 in .mpg and WMV2 in .wmv.
    ///
    /// WebM's VP9 encoder is markedly slower than the others; that is inherent
    /// to the codec, not a setting worth tuning away. <c>-row-mt 1</c> and
    /// <c>-cpu-used 4</c> claw back what can be clawed back.
    /// </remarks>
    public static readonly Format[] All =
    {
        new("mp4",  ".mp4",  "MP4  (H.264 / AAC — most compatible)", H264, true),
        new("mkv",  ".mkv",  "MKV  (Matroska, H.264 / AAC)",         H264, true),
        new("mov",  ".mov",  "MOV  (QuickTime, H.264 / AAC)",        H264, true),
        new("avi",  ".avi",  "AVI  (MPEG-4 / MP3 — legacy)",
            "-c:v mpeg4 -vtag xvid -qscale:v 3 -c:a libmp3lame -q:a 2", false),
        new("wmv",  ".wmv",  "WMV  (Windows Media)",
            "-c:v wmv2 -qscale:v 3 -c:a wmav2 -b:a 192k", false),
        new("mpg",  ".mpg",  "MPG  (MPEG-2 / MP2)",
            "-c:v mpeg2video -qscale:v 4 -c:a mp2 -b:a 192k", false),
        new("mpeg", ".mpeg", "MPEG (MPEG-2 / MP2)",
            "-c:v mpeg2video -qscale:v 4 -c:a mp2 -b:a 192k", false),
        new("webm", ".webm", "WEBM (VP9 / Opus — slow to encode)",
            "-c:v libvpx-vp9 -crf 32 -b:v 0 -row-mt 1 -cpu-used 4 -c:a libopus -b:a 128k", false),
    };

    /// <summary>The format used when settings hold nothing usable.</summary>
    public static Format Default => All[0];

    /// <summary>
    /// Looks up a format by its settings key. Falls back to
    /// <see cref="Default"/> rather than returning null — a stale or
    /// hand-edited settings key must never break an operation.
    /// </summary>
    public static Format FromKey(string? key) =>
        All.FirstOrDefault(f => string.Equals(f.Key, key, StringComparison.OrdinalIgnoreCase))
        ?? Default;

    /// <summary>
    /// A SaveFileDialog filter for one format, e.g. <c>"MKV Video|*.mkv"</c>.
    /// </summary>
    public static string SaveFilter(Format format) =>
        $"{format.Key.ToUpperInvariant()} Video|*{format.Extension}";

    /// <summary>
    /// Substitutes the quality preset into a format's encode arguments.
    /// </summary>
    /// <remarks>
    /// Only the x264 formats carry the token. MPEG-2, WMV2 and MPEG-4 Part 2
    /// have no CRF at all and use <c>-qscale:v</c>; VP9 has a CRF but on a
    /// different scale and with no <c>-preset</c>. Both are left with their
    /// own tuned arguments rather than being handed a translated x264 setting,
    /// which would be a guess dressed up as a preference.
    /// </remarks>
    /// <summary>
    /// Substitutes both the encoder and its quality flags into a format's
    /// arguments. Formats that name their own codec are returned unchanged
    /// apart from the quality token, since neither placeholder appears.
    /// </summary>
    public static string ApplyEncoder(Format format, string videoCodec, string qualityArgs) =>
        format.EncodeArgs
            .Replace(VideoCodecToken, videoCodec)
            .Replace(QualityToken, qualityArgs);

    /// <summary>
    /// True when the format's video codec is the one Settings can change —
    /// i.e. an H.264 container, where a GPU encoder is an option.
    /// </summary>
    public static bool UsesConfigurableEncoder(Format format) =>
        format.EncodeArgs.Contains(VideoCodecToken, StringComparison.Ordinal);
}
