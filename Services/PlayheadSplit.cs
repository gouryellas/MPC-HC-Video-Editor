using MpcHcVideoEditor.Models;

namespace MpcHcVideoEditor.Services;

/// <summary>
/// Puts a boundary in the cut list wherever the player happens to be.
/// </summary>
/// <remarks>
/// <para>
/// The timeline is read as a run of regions from <see cref="TimelineStart"/> to
/// the duration: the cuts themselves, and the gaps between and around them.
/// Whichever region holds the playhead is divided there, and a gap that gets
/// divided stops being a gap — it becomes bookmarks.
/// </para>
/// <para>
/// That one rule covers every case, including the ones that look like they need
/// their own. A video with no cuts at all is a single gap spanning the whole
/// timeline, so the first split on an empty list produces two bookmarks and
/// brings the CSV into existence. A cut still waiting for its closing timestamp
/// occupies everything from its start to the end of the video, because unbounded
/// is what incomplete means — so splitting inside one closes it and yields two
/// complete cuts rather than leaving half of it open.
/// </para>
/// <para>
/// A playhead landing exactly on a boundary divides nothing: the boundary it
/// would add is already there. What it does instead is fill in whichever
/// neighbouring region is a gap, the one on the left for preference. That is
/// what makes a split at a cut's start produce the stretch before it, and a
/// split at a cut's end produce the stretch after.
/// </para>
/// <para>
/// Separated from the ViewModel because it is the part worth testing: pure
/// arithmetic over a list, no player, no files, no UI.
/// </para>
/// </remarks>
public static class PlayheadSplit
{
    /// <summary>
    /// Where the timeline is treated as beginning.
    /// </summary>
    /// <remarks>
    /// One second, not zero, matching what opening a bookmark by hotkey already
    /// does: a cut starting at exactly 0 is almost always the player reporting
    /// its position before playback has really begun, and ffmpeg's seek at 0 is
    /// unreliable regardless.
    /// </remarks>
    public const double TimelineStart = 1.0;

    /// <summary>
    /// The shortest piece worth making, in seconds.
    /// </summary>
    /// <remarks>
    /// A tenth of a second rather than one frame. A frame would be the more
    /// principled floor, but the frame rate costs an ffprobe call and this runs
    /// on a keystroke while a video plays; a tenth of a second is longer than a
    /// frame at any rate anybody films at, and nothing shorter is a clip.
    /// </remarks>
    public const double MinimumLength = 0.1;

    /// <summary>What a split did, or why it did nothing.</summary>
    public enum Outcome
    {
        /// <summary>A region was divided in two.</summary>
        Divided,

        /// <summary>A gap next to the playhead became a bookmark.</summary>
        Filled,

        /// <summary>The playhead is already a boundary with cuts either side.</summary>
        AlreadySplit,

        /// <summary>Dividing here would make a piece too short to be a clip.</summary>
        TooShort,

        /// <summary>The playhead is outside the timeline.</summary>
        OutOfRange,

        /// <summary>No duration, so the timeline has no end to measure from.</summary>
        NoDuration
    }

    /// <param name="Cuts">
    /// The whole list as it should now be, in order. Empty for any outcome
    /// other than <see cref="Outcome.Divided"/> or <see cref="Outcome.Filled"/>.
    /// </param>
    /// <param name="Added">How many bookmarks the list gained.</param>
    public sealed record Result(Outcome Outcome, List<BookmarkState> Cuts, int Added);

    /// <summary>One stretch of the timeline, and the cut behind it if any.</summary>
    private readonly record struct Region(double Start, double End, BookmarkState? Cut)
    {
        public bool IsGap => Cut is null;
    }

    /// <summary>
    /// Works out the new cut list. Changes nothing itself.
    /// </summary>
    public static Result At(IReadOnlyList<BookmarkState> cuts, double playhead, double duration)
    {
        if (duration <= TimelineStart) return Fail(Outcome.NoDuration);
        if (playhead <= TimelineStart || playhead >= duration) return Fail(Outcome.OutOfRange);

        var regions = MapTimeline(cuts, duration);

        // Inside a region: divide it.
        foreach (var region in regions)
        {
            if (playhead <= region.Start || playhead >= region.End) continue;

            if (playhead - region.Start < MinimumLength ||
                region.End - playhead < MinimumLength)
                return Fail(Outcome.TooShort);

            return new Result(Outcome.Divided, Rebuild(cuts, region, Divide(region, playhead)), 1);
        }

        // On a boundary instead. Nothing to divide, so fill in a neighbouring
        // gap — the one before the playhead for preference, since a split at a
        // cut's start is asking about the stretch leading up to it.
        var before = regions.FirstOrDefault(r => r.IsGap && Near(r.End, playhead));
        var after = regions.FirstOrDefault(r => r.IsGap && Near(r.Start, playhead));

        var gap = before.Cut is null && before.End > before.Start ? before
                : after.Cut is null && after.End > after.Start ? after
                : default;

        if (gap.End <= gap.Start) return Fail(Outcome.AlreadySplit);
        if (gap.End - gap.Start < MinimumLength) return Fail(Outcome.TooShort);

        return new Result(Outcome.Filled, Rebuild(cuts, gap, new[] { Fresh(gap.Start, gap.End) }), 1);
    }

    /// <summary>
    /// The timeline as a continuous run of regions, in order, with no holes.
    /// </summary>
    private static List<Region> MapTimeline(IReadOnlyList<BookmarkState> cuts, double duration)
    {
        var regions = new List<Region>();
        var cursor = TimelineStart;

        foreach (var cut in cuts.OrderBy(c => c.StartSeconds))
        {
            // Incomplete means unbounded: it runs to the end of the video, which
            // is why no trailing gap is ever offered after one.
            var end = cut.EndSeconds > cut.StartSeconds ? cut.EndSeconds : duration;
            var start = Math.Max(cut.StartSeconds, TimelineStart);

            if (start > cursor) regions.Add(new Region(cursor, start, null));
            if (end > start) regions.Add(new Region(start, end, cut));

            cursor = Math.Max(cursor, end);
        }

        if (duration > cursor) regions.Add(new Region(cursor, duration, null));
        return regions;
    }

    /// <summary>The two pieces a region becomes when cut at <paramref name="at"/>.</summary>
    /// <remarks>
    /// A gap divides into two bookmarks with nothing on them — it was never a
    /// cut, so there are no settings to inherit and taking a neighbour's crop
    /// would be inventing one.
    ///
    /// A real cut hands both halves everything it had, with the fades as the
    /// single exception. A fade belongs to an outer edge, and after a division
    /// each outer edge lies in exactly one of the halves; copying both to both
    /// would dip the picture and the sound to nothing in the middle of what was
    /// one continuous shot and bring them back again.
    /// </remarks>
    private static BookmarkState[] Divide(Region region, double at)
    {
        if (region.Cut is not { } cut)
            return new[] { Fresh(region.Start, at), Fresh(at, region.End) };

        return new[]
        {
            cut with { StartSeconds = region.Start, EndSeconds = at, FadeOutSeconds = 0 },
            cut with { StartSeconds = at, EndSeconds = region.End, FadeInSeconds = 0 }
        };
    }

    /// <summary>A bookmark over a stretch that was not one before.</summary>
    /// <remarks>
    /// Built from a default <see cref="Bookmark"/> rather than by naming each
    /// field, so it picks up whatever the defaults are — speed, crop rectangle
    /// — instead of restating them here and drifting the first time one moves.
    /// </remarks>
    private static BookmarkState Fresh(double start, double end) =>
        BookmarkState.From(new Bookmark { StartSeconds = start, EndSeconds = end });

    /// <summary>
    /// The list with <paramref name="replaced"/> swapped for
    /// <paramref name="pieces"/>, back in time order.
    /// </summary>
    private static List<BookmarkState> Rebuild(
        IReadOnlyList<BookmarkState> cuts, Region replaced, IEnumerable<BookmarkState> pieces)
    {
        var rebuilt = cuts.Where(c => !ReferenceEquals(c, replaced.Cut)).ToList();
        rebuilt.AddRange(pieces);

        // Incomplete cuts sort by where they start, like everything else: their
        // end is not a time, so ordering on it would put them first.
        return rebuilt.OrderBy(c => c.StartSeconds).ToList();
    }

    /// <summary>
    /// Whether two times are the same instant as far as a boundary goes.
    /// </summary>
    /// <remarks>
    /// The playhead arrives in milliseconds from the player and the boundaries
    /// come from a CSV, so two values standing for the same frame are rarely
    /// equal to the last bit. A millisecond is finer than either source.
    /// </remarks>
    private static bool Near(double a, double b) => Math.Abs(a - b) < 0.001;

    private static Result Fail(Outcome outcome) => new(outcome, new List<BookmarkState>(), 0);
}
