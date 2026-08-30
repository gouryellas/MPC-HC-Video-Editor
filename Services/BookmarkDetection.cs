namespace MpcHcVideoEditor.Services;

/// <summary>
/// How to look for the boundaries between one clip and the next.
/// </summary>
public enum DetectionMode
{
    /// <summary>
    /// Propose the stretches that are <em>not</em> silent. Best for anything
    /// speech-driven, where the gaps are the joins.
    /// </summary>
    Silence,

    /// <summary>
    /// Propose the stretches that are not black frames. Best for recordings
    /// with fades between segments, and for adverts.
    /// </summary>
    BlackFrames,

    /// <summary>
    /// Propose the stretches between hard visual cuts. Best for edited footage
    /// with no audio or fade cues to go on.
    /// </summary>
    SceneChanges
}

/// <summary>
/// One proposed clip, in seconds.
/// </summary>
public readonly record struct DetectedRange(double Start, double End)
{
    public double Duration => End - Start;
}

/// <summary>
/// What to look for and how hard.
/// </summary>
/// <param name="Mode">Which signal marks a boundary.</param>
/// <param name="Threshold">
/// Silence level in dBFS (−30 is a reasonable room), blackness as a fraction
/// (0.98), or scene-change score (0–1, around 0.4). Meaning depends on
/// <paramref name="Mode"/>, which is why one number serves all three.
/// </param>
/// <param name="MinBoundarySeconds">
/// How long a gap has to last before it counts as a boundary. Below this it is
/// a pause in speech, not the end of a clip.
/// </param>
/// <param name="MinClipSeconds">
/// Proposals shorter than this are dropped. Detection on real footage produces
/// a scattering of fragments, and a list of eighty half-second clips is not
/// something anyone will review.
/// </param>
public readonly record struct DetectionSettings(
    DetectionMode Mode,
    double Threshold,
    double MinBoundarySeconds,
    double MinClipSeconds)
{
    /// <summary>Sensible starting point for each mode.</summary>
    /// <remarks>
    /// The scene threshold is 0.3, not 0.4. A measured hard cut between two
    /// wholly different shots scored exactly 0.400, and the comparison is a
    /// strict greater-than — so a 0.4 default silently missed the very cut it
    /// was meant to catch. Detection that misses is worse than detection that
    /// over-offers, because the user can delete a wrong proposal but cannot see
    /// one that was never made.
    /// </remarks>
    public static DetectionSettings For(DetectionMode mode) => mode switch
    {
        DetectionMode.BlackFrames  => new(mode, 0.98, 0.5, 2.0),
        DetectionMode.SceneChanges => new(mode, 0.30, 0.0, 2.0),
        _                          => new(mode, -30,  0.5, 2.0)
    };
}
