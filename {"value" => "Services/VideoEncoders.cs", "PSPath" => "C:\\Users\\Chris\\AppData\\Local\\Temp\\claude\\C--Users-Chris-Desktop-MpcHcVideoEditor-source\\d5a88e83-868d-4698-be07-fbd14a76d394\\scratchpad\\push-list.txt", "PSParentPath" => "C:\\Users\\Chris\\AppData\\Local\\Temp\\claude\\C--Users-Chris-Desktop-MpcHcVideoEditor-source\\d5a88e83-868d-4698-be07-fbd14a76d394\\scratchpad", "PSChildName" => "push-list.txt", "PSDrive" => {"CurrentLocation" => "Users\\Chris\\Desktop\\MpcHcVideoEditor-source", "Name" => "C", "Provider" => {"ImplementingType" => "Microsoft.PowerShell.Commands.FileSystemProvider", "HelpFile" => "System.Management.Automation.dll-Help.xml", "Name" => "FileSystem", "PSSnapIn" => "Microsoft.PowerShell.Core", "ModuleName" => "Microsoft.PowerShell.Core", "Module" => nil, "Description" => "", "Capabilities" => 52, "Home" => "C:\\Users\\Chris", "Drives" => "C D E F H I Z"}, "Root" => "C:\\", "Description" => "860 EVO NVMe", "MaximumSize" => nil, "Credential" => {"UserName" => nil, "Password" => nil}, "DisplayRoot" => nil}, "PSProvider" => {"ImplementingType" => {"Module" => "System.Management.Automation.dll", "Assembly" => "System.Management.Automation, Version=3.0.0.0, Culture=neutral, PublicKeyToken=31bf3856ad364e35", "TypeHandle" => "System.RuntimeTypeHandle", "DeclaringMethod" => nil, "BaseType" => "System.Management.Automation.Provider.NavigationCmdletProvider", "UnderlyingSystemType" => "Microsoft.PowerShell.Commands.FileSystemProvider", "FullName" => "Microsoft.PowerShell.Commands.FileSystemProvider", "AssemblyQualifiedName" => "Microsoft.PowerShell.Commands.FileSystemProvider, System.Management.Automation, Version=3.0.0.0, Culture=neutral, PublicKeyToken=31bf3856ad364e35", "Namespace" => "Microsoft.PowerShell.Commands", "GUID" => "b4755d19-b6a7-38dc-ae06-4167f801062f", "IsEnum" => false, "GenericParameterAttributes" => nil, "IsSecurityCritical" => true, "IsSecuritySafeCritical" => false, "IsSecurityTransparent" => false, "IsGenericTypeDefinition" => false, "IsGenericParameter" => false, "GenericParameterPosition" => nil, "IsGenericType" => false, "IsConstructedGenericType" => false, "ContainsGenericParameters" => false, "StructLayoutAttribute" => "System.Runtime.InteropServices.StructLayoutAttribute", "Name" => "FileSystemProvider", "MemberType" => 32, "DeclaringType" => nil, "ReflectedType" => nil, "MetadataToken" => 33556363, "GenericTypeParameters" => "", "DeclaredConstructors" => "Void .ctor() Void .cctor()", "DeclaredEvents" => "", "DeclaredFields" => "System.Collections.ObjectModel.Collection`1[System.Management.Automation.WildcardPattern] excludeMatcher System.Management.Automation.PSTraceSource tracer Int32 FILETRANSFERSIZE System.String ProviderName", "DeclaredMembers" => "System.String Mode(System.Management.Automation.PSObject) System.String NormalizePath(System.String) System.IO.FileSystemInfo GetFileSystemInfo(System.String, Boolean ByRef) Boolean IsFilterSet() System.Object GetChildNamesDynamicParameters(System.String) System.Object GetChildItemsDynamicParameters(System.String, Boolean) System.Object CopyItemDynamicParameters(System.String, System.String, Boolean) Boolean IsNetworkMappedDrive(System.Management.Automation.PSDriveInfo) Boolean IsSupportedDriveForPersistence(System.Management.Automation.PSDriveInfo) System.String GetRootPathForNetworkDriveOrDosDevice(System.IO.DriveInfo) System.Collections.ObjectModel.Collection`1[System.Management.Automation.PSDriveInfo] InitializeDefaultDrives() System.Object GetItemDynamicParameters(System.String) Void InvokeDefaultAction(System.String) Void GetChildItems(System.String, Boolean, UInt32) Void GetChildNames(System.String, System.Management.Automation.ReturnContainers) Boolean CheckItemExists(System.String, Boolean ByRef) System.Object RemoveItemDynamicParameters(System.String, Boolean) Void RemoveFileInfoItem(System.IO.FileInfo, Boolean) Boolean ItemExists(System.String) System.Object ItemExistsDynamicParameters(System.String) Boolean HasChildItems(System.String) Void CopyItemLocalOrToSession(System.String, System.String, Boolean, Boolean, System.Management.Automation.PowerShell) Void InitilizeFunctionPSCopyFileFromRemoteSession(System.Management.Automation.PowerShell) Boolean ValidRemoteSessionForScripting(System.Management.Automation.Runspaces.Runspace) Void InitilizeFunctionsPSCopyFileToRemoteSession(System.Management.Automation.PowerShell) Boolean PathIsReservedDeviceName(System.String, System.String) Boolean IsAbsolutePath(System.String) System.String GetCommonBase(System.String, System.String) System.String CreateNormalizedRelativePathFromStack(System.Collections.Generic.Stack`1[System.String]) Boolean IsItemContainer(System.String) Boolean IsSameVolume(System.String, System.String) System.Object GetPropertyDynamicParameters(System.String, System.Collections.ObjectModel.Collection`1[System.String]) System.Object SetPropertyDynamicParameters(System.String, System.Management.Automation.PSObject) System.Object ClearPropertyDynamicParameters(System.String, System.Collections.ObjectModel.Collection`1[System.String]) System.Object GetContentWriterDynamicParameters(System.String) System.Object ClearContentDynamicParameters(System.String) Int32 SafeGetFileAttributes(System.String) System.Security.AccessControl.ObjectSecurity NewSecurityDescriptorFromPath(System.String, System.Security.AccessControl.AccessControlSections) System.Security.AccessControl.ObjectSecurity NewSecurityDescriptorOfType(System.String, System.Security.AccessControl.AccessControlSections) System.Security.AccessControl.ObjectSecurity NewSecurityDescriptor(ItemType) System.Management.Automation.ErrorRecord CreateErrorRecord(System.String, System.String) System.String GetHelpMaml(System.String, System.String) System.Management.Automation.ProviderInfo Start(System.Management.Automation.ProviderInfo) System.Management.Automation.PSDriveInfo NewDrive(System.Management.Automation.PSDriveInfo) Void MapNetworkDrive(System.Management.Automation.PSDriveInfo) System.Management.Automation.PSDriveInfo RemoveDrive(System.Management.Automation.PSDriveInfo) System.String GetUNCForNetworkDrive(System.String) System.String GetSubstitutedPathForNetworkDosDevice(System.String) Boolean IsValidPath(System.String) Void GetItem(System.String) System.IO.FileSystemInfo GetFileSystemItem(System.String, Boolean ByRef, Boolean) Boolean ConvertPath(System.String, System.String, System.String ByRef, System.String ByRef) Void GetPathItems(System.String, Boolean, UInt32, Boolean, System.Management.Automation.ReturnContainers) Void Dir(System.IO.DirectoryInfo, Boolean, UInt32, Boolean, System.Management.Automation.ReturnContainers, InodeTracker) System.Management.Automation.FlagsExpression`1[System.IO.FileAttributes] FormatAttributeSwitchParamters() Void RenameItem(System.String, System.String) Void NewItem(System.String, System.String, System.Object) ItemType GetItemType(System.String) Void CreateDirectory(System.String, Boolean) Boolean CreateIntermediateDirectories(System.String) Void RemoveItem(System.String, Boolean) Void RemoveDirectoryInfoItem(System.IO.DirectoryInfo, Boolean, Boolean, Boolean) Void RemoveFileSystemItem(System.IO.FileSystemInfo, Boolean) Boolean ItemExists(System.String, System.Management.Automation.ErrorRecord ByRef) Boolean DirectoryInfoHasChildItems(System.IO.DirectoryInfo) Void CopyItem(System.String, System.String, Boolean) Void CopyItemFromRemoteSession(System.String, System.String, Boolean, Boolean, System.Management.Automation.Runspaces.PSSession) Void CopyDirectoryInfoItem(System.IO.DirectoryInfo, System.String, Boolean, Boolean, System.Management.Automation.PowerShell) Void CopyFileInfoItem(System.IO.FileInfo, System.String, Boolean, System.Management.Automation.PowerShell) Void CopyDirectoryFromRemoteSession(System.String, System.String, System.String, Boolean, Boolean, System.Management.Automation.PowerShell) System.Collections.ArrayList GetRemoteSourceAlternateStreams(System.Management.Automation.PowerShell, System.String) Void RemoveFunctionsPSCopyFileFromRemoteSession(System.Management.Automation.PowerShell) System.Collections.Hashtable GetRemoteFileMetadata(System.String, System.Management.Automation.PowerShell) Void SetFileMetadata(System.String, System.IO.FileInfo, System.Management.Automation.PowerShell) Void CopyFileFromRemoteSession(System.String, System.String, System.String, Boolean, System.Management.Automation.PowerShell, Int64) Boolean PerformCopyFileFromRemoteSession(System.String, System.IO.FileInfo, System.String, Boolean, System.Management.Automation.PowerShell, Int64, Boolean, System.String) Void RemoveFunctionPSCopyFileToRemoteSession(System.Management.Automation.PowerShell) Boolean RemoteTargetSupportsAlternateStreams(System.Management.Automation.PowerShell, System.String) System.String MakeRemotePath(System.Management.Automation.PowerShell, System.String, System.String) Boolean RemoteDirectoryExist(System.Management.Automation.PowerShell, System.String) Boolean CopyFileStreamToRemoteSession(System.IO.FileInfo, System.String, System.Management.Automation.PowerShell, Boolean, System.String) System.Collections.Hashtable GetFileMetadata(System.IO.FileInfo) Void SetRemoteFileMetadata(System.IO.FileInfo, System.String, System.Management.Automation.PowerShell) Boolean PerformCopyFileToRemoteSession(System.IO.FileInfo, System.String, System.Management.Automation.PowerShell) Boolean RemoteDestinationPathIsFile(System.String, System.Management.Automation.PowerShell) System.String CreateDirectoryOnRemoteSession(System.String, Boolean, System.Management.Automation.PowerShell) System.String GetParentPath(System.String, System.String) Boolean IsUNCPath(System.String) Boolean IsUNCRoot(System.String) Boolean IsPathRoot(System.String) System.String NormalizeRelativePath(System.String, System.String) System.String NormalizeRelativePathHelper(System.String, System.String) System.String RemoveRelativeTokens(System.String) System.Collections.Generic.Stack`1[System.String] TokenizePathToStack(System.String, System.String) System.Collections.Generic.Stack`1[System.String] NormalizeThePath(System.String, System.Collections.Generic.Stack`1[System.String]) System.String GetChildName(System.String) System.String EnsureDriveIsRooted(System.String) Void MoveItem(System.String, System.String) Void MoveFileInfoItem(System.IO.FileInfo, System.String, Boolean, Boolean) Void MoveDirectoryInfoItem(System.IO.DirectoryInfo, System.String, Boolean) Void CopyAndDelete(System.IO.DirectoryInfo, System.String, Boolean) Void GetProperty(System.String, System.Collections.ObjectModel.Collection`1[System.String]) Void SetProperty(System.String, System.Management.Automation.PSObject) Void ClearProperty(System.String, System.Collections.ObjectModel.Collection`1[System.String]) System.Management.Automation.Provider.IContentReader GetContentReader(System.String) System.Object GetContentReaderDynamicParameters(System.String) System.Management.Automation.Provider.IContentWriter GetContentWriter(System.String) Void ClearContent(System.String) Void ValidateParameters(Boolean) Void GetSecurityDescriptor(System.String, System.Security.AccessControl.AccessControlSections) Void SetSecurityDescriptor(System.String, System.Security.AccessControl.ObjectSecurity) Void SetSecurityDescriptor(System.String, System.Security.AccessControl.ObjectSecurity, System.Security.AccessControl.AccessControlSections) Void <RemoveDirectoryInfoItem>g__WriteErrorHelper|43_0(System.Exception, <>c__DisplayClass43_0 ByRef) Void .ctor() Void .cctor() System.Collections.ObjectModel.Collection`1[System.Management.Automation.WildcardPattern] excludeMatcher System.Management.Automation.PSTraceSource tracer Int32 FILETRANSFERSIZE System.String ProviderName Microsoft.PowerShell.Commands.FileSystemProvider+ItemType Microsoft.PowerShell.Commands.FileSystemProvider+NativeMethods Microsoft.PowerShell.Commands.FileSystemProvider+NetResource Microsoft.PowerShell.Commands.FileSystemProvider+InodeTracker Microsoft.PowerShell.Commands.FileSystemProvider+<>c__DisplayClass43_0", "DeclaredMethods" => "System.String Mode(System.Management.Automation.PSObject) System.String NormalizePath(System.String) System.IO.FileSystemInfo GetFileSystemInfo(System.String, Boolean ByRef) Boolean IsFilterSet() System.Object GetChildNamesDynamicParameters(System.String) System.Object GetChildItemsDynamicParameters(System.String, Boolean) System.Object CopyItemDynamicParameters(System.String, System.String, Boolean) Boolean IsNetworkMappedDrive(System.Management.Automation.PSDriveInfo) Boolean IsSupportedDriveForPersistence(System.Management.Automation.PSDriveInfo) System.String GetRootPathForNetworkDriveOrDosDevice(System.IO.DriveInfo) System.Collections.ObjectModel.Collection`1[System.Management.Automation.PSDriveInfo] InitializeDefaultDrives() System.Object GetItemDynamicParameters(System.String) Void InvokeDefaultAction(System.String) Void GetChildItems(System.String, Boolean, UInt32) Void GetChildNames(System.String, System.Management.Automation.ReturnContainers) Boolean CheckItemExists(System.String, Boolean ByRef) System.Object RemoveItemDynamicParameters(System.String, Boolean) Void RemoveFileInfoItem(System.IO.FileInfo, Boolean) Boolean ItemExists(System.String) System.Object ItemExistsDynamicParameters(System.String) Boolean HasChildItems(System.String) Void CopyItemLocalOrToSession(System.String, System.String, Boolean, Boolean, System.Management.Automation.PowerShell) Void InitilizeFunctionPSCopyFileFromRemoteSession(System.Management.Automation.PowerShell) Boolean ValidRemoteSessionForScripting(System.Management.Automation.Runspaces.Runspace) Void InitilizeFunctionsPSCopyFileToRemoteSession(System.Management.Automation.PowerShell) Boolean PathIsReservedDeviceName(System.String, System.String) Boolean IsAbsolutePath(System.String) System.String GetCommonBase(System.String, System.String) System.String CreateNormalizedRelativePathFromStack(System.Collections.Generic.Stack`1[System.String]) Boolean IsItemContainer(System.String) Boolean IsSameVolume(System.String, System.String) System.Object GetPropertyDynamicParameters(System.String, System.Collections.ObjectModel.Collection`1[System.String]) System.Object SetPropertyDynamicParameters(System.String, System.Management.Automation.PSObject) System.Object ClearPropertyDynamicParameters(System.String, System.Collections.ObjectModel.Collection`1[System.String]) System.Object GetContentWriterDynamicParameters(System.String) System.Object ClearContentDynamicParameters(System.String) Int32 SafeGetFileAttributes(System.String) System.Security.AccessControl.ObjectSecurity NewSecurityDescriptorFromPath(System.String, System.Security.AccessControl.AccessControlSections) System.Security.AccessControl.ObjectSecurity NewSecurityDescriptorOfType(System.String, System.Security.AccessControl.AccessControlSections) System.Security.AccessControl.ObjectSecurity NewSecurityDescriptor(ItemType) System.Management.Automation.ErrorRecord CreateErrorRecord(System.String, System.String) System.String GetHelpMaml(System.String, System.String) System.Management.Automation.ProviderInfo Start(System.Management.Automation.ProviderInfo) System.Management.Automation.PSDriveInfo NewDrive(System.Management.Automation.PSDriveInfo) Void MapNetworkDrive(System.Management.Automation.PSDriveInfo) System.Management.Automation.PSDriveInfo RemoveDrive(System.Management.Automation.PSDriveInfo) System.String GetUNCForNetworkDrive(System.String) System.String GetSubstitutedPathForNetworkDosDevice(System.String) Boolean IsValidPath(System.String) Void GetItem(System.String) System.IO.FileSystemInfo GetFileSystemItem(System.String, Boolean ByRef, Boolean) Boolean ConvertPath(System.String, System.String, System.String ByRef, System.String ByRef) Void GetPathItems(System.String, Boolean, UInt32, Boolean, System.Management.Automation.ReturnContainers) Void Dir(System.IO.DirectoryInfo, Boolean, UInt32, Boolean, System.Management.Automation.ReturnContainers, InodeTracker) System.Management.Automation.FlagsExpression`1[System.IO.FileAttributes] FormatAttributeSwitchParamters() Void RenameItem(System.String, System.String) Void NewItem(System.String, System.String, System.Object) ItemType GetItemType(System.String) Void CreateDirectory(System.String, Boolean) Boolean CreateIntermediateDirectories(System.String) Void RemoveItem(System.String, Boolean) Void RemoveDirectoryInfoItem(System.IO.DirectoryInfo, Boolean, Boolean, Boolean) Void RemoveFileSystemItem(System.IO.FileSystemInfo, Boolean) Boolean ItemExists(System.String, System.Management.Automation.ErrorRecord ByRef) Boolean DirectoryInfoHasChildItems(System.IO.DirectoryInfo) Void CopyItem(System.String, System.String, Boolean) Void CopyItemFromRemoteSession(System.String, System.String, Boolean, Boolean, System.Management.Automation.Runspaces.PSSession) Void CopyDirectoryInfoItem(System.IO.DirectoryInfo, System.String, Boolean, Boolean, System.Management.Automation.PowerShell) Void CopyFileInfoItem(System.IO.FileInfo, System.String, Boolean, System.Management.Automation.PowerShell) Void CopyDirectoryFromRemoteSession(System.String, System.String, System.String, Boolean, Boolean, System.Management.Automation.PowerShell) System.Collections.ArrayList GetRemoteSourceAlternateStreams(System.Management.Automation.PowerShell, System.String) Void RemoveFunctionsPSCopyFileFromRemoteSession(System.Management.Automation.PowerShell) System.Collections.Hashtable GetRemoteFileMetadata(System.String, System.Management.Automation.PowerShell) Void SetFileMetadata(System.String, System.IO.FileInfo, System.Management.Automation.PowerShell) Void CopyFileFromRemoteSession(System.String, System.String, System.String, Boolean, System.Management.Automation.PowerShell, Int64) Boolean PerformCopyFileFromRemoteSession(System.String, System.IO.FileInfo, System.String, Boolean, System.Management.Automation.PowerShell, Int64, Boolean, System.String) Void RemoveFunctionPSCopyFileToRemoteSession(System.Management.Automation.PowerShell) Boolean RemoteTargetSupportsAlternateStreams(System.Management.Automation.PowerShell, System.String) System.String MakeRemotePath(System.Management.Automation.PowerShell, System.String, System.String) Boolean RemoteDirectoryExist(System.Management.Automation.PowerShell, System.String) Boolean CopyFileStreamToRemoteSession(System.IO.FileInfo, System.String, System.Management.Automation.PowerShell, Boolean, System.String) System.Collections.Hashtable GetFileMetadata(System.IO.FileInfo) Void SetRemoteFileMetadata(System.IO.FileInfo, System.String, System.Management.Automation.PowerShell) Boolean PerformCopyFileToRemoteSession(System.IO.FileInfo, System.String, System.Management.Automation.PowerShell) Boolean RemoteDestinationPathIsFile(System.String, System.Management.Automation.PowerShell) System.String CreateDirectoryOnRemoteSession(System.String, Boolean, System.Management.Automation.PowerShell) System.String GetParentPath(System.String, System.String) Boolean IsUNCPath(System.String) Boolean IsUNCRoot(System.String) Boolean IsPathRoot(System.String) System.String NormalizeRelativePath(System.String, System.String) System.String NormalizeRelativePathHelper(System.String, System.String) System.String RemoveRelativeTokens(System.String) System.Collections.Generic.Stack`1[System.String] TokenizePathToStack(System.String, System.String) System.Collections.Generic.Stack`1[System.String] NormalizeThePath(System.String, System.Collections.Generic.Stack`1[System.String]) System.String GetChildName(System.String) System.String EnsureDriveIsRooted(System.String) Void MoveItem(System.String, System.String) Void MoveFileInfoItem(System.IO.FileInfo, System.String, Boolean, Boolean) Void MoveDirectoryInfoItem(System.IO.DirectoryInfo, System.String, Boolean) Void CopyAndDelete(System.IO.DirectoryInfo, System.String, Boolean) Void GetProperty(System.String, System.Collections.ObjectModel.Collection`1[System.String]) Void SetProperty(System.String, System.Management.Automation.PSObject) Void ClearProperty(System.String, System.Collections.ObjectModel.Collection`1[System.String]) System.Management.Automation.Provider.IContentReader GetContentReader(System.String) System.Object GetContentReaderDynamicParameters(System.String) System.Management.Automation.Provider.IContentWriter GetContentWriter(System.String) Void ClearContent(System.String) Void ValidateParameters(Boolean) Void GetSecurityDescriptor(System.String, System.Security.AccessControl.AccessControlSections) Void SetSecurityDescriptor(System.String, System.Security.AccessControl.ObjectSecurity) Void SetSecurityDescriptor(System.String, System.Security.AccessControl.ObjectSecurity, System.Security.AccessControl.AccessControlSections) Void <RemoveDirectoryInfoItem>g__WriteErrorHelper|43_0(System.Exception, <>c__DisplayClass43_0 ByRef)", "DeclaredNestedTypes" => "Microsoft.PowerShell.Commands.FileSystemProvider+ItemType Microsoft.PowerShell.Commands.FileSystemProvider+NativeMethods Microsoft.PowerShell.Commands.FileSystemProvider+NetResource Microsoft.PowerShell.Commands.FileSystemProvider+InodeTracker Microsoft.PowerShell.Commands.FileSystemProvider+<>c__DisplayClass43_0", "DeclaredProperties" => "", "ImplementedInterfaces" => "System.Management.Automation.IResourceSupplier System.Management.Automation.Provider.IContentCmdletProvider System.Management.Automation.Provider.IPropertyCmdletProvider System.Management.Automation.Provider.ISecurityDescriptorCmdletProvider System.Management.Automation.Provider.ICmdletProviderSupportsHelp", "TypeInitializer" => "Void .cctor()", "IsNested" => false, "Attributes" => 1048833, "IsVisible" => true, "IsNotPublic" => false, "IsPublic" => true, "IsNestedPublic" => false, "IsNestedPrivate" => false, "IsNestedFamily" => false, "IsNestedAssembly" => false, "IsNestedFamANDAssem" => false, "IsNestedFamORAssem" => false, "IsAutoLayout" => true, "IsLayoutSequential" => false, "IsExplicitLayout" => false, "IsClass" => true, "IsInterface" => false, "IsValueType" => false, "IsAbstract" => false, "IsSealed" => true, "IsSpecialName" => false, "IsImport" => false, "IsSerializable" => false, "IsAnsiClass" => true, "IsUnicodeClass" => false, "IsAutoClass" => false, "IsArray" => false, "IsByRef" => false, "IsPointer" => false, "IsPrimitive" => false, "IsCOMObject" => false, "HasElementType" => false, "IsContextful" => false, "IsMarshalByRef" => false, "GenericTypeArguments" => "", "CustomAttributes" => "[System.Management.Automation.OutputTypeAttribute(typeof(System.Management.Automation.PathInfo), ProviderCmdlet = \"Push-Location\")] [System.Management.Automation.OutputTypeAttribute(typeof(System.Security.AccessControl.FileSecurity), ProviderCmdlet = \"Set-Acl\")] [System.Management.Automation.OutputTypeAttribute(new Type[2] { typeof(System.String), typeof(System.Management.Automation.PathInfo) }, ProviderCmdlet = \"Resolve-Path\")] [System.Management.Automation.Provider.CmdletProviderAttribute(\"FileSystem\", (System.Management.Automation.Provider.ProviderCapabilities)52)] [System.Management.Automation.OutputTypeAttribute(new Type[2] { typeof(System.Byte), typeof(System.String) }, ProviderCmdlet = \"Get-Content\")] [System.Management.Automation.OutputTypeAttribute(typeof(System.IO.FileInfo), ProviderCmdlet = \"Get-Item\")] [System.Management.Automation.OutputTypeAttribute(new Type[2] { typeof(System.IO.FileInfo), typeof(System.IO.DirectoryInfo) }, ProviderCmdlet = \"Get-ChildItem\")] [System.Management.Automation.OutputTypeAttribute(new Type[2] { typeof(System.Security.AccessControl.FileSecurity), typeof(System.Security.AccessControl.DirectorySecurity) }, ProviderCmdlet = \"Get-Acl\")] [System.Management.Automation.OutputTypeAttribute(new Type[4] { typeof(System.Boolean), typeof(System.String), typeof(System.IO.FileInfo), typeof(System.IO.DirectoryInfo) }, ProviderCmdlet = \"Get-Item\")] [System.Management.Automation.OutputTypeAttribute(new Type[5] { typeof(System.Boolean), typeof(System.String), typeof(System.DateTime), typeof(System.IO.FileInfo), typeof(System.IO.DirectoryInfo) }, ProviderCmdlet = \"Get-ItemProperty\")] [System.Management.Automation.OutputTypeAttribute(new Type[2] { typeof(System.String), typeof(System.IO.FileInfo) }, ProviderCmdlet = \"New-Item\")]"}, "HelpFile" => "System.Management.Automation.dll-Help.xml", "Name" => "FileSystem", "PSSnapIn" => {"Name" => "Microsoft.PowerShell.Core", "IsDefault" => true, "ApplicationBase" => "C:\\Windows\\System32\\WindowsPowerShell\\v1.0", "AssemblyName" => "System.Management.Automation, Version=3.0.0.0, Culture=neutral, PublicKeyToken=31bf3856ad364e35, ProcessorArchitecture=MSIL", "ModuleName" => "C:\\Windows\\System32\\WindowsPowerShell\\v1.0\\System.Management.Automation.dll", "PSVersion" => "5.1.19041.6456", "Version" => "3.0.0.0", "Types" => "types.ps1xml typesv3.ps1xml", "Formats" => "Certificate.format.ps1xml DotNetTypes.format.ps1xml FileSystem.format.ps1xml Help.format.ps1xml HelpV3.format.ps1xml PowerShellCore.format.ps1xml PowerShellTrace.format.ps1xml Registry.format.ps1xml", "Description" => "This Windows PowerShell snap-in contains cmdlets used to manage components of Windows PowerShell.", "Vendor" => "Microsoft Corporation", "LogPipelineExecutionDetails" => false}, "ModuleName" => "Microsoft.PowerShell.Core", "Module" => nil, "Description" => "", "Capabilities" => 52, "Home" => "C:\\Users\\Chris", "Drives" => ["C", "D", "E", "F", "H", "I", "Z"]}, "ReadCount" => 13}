namespace MpcHcVideoEditor.Services;

/// <summary>
/// Which H.264 encoder to use when video is actually re-encoded.
/// </summary>
/// <remarks>
/// Only affects the H.264 containers (MP4, MKV, MOV). The legacy formats carry
/// their own codecs — MPEG-4 Part 2, WMV2, MPEG-2, VP9 — and none of them has a
/// hardware path worth taking here.
/// </remarks>
public enum VideoEncoder
{
    /// <summary>x264 on the CPU. Always available, best quality per bit.</summary>
    Software,

    /// <summary>NVIDIA NVENC. Needs a reasonably recent GeForce or Quadro.</summary>
    Nvenc,

    /// <summary>Intel Quick Sync. Needs an Intel iGPU or Arc.</summary>
    QuickSync,

    /// <summary>AMD AMF/VCE. Needs a Radeon.</summary>
    Amf
}

/// <summary>
/// Maps an encoder choice and a quality level onto ffmpeg arguments.
/// </summary>
/// <remarks>
/// <para>
/// The hardware encoders do not understand <c>-crf</c> and do not share x264's
/// preset names, so each needs its own translation rather than a codec swap.
/// The numbers below are chosen to land near the software equivalent, but they
/// are not the same scale and will not produce identical output — a hardware
/// encode is generally larger at a given visual quality. That is the trade for
/// finishing several times sooner.
/// </para>
/// <para>
/// Being listed here is not a promise the machine can do it. A GPU that lacks
/// the silicon still advertises the encoder in <c>ffmpeg -encoders</c>, so the
/// only reliable test is to try one — see
/// <see cref="FFmpegService.CanEncodeAsync"/>.
/// </para>
/// </remarks>
public static class VideoEncoders
{
    /// <summary>Every encoder, in the order the settings dialog lists them.</summary>
    public static readonly VideoEncoder[] All =
    {
        VideoEncoder.Software,
        VideoEncoder.Nvenc,
        VideoEncoder.QuickSync,
        VideoEncoder.Amf
    };

    /// <summary>The ffmpeg encoder name.</summary>
    public static string CodecFor(VideoEncoder encoder) => encoder switch
    {
        VideoEncoder.Nvenc     => "h264_nvenc",
        VideoEncoder.QuickSync => "h264_qsv",
        VideoEncoder.Amf       => "h264_amf",
        _                      => "libx264"
    };

    /// <summary>Short label for the settings dialog.</summary>
    public static string DisplayName(VideoEncoder encoder) => encoder switch
    {
        VideoEncoder.Nvenc     => "NVIDIA (NVENC)",
        VideoEncoder.QuickSync => "Intel (Quick Sync)",
        VideoEncoder.Amf       => "AMD (AMF)",
        _                      => "Software (x264)"
    };

    /// <summary>
    /// The rate-control flags for an encoder at a given quality level.
    /// </summary>
    /// <remarks>
    /// NVENC and AMF need an explicit <c>-b:v 0</c> alongside their quality
    /// figure: without it both fall back to a default bitrate and quietly
    /// ignore the setting, which looks like the quality control doing nothing.
    /// </remarks>
    public static string QualityArgsFor(VideoEncoder encoder, EncodingQuality quality) => encoder switch
    {
        VideoEncoder.Nvenc => quality switch
        {
            EncodingQuality.Fast => "-preset p1 -rc vbr -cq 28 -b:v 0",
            EncodingQuality.High => "-preset p6 -rc vbr -cq 20 -b:v 0",
            _                    => "-preset p4 -rc vbr -cq 24 -b:v 0"
        },

        VideoEncoder.QuickSync => quality switch
        {
            EncodingQuality.Fast => "-preset veryfast -global_quality 28",
            EncodingQuality.High => "-preset slower -global_quality 20",
            _                    => "-preset medium -global_quality 24"
        },

        VideoEncoder.Amf => quality switch
        {
            EncodingQuality.Fast => "-quality speed -rc cqp -qp_i 28 -qp_p 28 -b:v 0",
            EncodingQuality.High => "-quality quality -rc cqp -qp_i 20 -qp_p 20 -b:v 0",
            _                    => "-quality balanced -rc cqp -qp_i 24 -qp_p 24 -b:v 0"
        },

        _ => quality switch
        {
            EncodingQuality.Fast => "-preset veryfast -crf 23",
            EncodingQuality.High => "-preset medium -crf 17",
            _                    => "-preset faster -crf 20"
        }
    };
}
