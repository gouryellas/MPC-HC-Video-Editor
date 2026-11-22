using MpcHcVideoEditor.Models;
using MpcHcVideoEditor.Services;
using Xunit;

namespace MpcHcVideoEditor.Tests;

/// <summary>
/// The arithmetic and parsing — no player, no files, no ffmpeg.
/// </summary>
/// <remarks>
/// These run in milliseconds and need nothing installed, which is what makes
/// them worth having: the parts of this program that are pure are also the
/// parts whose bugs are silent, because a time parsed slightly wrong still
/// produces a clip.
/// </remarks>
public class ProcessingOrderTests
{
    private static List<Bookmark> Cuts(int count) =>
        Enumerable.Range(1, count)
                  .Select(i => new Bookmark { Index = i, StartSeconds = i * 100, EndSeconds = i * 100 + 50 })
                  .ToList();

    [Theory]
    [InlineData("3,1,2")]
    [InlineData("3, 1, 2")]
    [InlineData("3 1 2")]
    [InlineData(" 3;;1 ,2 ")]
    public void Separators_do_not_matter(string text)
    {
        Assert.True(ProcessingOrderInput.TryParse(text, Cuts(3), out var order, out _));
        Assert.Equal(new[] { 3, 1, 2 }, order.Select(b => b.Index));
    }

    [Theory]
    [InlineData("3,1,2,3", "more than once")]
    [InlineData("3,1,9", "9")]
    [InlineData("0,1,2", "0")]
    [InlineData("3,x,1", "not a clip number")]
    [InlineData("3,1", "missing")]
    [InlineData("   ", "Type the clip numbers")]
    public void Bad_input_is_refused_and_says_why(string text, string mustMention)
    {
        var ok = ProcessingOrderInput.TryParse(text, Cuts(3), out var order, out var problem);

        Assert.False(ok);
        Assert.Contains(mustMention, problem, StringComparison.OrdinalIgnoreCase);

        // Nothing is handed back on a refusal. A caller that reads the list
        // without checking the bool should get an empty one rather than the
        // first few clips of an order that was rejected.
        Assert.Empty(order);
    }

    [Fact]
    public void A_single_missing_clip_reads_differently_from_several()
    {
        ProcessingOrderInput.TryParse("1,2", Cuts(3), out _, out var one);
        ProcessingOrderInput.TryParse("1", Cuts(3), out _, out var many);

        Assert.Contains("Clip 3 is missing", one);
        Assert.Contains("Clips 2, 3", many);
    }
}

public class PlayheadSplitTests
{
    private static BookmarkState Cut(double start, double end, string? name = null) =>
        BookmarkState.From(new Bookmark { StartSeconds = start, EndSeconds = end, Label = name });

    private static string Shape(IEnumerable<BookmarkState> cuts) =>
        string.Join(" ", cuts.Select(c => $"{c.StartSeconds:0}-{c.EndSeconds:0}"));

    [Fact]
    public void An_ordinary_list_is_divided_in_place_and_stays_sorted()
    {
        var cuts = new List<BookmarkState> { Cut(10, 50), Cut(100, 150), Cut(200, 250) };

        var result = PlayheadSplit.At(cuts, 120, 400);

        Assert.Equal(PlayheadSplit.Outcome.Divided, result.Outcome);
        Assert.Equal("10-50 100-120 120-150 200-250", Shape(result.Cuts));
    }

    [Fact]
    public void The_result_comes_back_in_time_order_whatever_went_in()
    {
        // Pinning current behaviour rather than endorsing it. A processing
        // order is held beside the cuts and not in them, so the list itself
        // is always the video's order — but if that ever changes, this is
        // the test that will say so.
        var cuts = new List<BookmarkState> { Cut(200, 250, "c"), Cut(10, 50, "a"), Cut(100, 150, "b") };

        var result = PlayheadSplit.At(cuts, 120, 400);

        Assert.Equal("10-50 100-120 120-150 200-250", Shape(result.Cuts));
    }

    [Fact]
    public void The_two_halves_inherit_the_name_of_what_they_came_from()
    {
        var cuts = new List<BookmarkState> { Cut(100, 150, "the good bit") };

        var result = PlayheadSplit.At(cuts, 120, 400);

        Assert.All(result.Cuts, c => Assert.Equal("the good bit", c.Label));
    }

    [Theory]
    [InlineData(50, PlayheadSplit.Outcome.AlreadySplit)]
    [InlineData(500, PlayheadSplit.Outcome.OutOfRange)]
    public void Refusals_are_named(double playhead, PlayheadSplit.Outcome expected)
    {
        var cuts = new List<BookmarkState> { Cut(10, 50), Cut(50, 90) };

        Assert.Equal(expected, PlayheadSplit.At(cuts, playhead, 400).Outcome);
    }

    [Fact]
    public void No_duration_is_its_own_answer()
    {
        Assert.Equal(PlayheadSplit.Outcome.NoDuration,
                     PlayheadSplit.At(new List<BookmarkState>(), 100, 0).Outcome);
    }

    // ------------------------------------------------------------------
    // Words that stand for a time in the video
    // ------------------------------------------------------------------

    [Theory]
    [InlineData("start", 0)]
    [InlineData("quarter", 60)]
    [InlineData("third", 80)]
    [InlineData("middle", 120)]
    [InlineData("end", 240)]
    [InlineData("END", 240)]
    [InlineData("  Middle  ", 120)]
    public void A_word_can_stand_for_a_time_in_the_video(string typed, double expected)
    {
        Assert.True(Helpers.TimeInput.TryParseOne(
            typed, new Helpers.TimeInput.Context(240), out var seconds, out _));

        Assert.Equal(expected, seconds, 3);
    }

    [Fact]
    public void A_word_that_needs_the_length_is_refused_without_one()
    {
        Assert.False(Helpers.TimeInput.TryParseOne("end", default, out _, out var error));
        Assert.Contains("length", error);
    }

    [Fact]
    public void Start_needs_no_length()
    {
        // Nothing has to be known about the video to say where it begins,
        // so this one word works before a duration has been read.
        Assert.True(Helpers.TimeInput.TryParseOne("start", default, out var seconds, out _));
        Assert.Equal(0, seconds);
    }

    [Fact]
    public void A_random_time_lands_in_the_video_and_after_the_last_mark()
    {
        var context = new Helpers.TimeInput.Context(240, After: 200);

        // Drawn, so it is worth asking more than once.
        for (var i = 0; i < 50; i++)
        {
            Assert.True(Helpers.TimeInput.TryParseOne("random", context, out var seconds, out _));
            Assert.InRange(seconds, 200, 240);
        }
    }

    [Fact]
    public void A_random_time_with_no_room_left_says_so()
    {
        var context = new Helpers.TimeInput.Context(240, After: 240);

        Assert.False(Helpers.TimeInput.TryParseOne("random", context, out _, out var error));
        Assert.Contains("no room", error);
    }

    [Fact]
    public void A_range_may_be_written_in_words()
    {
        Assert.True(Helpers.TimeInput.TryParse(
            "middle - end", new Helpers.TimeInput.Context(240), out var parsed, out _));

        Assert.Equal(120, parsed.Start, 3);
        Assert.Equal(240, parsed.End!.Value, 3);
    }

    [Fact]
    public void A_random_end_comes_after_the_start_it_was_given()
    {
        // The right-hand side is drawn knowing the left, so the range
        // cannot come out backwards.
        for (var i = 0; i < 50; i++)
        {
            Assert.True(Helpers.TimeInput.TryParse(
                "200 - random", new Helpers.TimeInput.Context(240), out var parsed, out _));

            Assert.InRange(parsed.End!.Value, 200, 240);
        }
    }

    [Fact]
    public void An_unknown_word_is_still_refused()
    {
        Assert.False(Helpers.TimeInput.TryParseOne(
            "banana", new Helpers.TimeInput.Context(240), out _, out var error));

        Assert.NotEmpty(error);
    }

    [Theory]
    [InlineData("now", 90)]
    [InlineData("here", 90)]
    [InlineData("last", 200)]
    [InlineData("half", 120)]
    [InlineData("25%", 60)]
    [InlineData("0%", 0)]
    [InlineData("100%", 240)]
    [InlineData("end minus 10s", 230)]
    [InlineData("middle + 30s", 150)]
    [InlineData("last + 5s", 205)]
    [InlineData("now minus 1min", 30)]
    [InlineData("END MINUS 10s", 230)]
    public void The_rest_of_the_words_and_sums_read(string typed, double expected)
    {
        var context = new Helpers.TimeInput.Context(240, After: 200, Now: 90);

        Assert.True(Helpers.TimeInput.TryParseOne(typed, context, out var seconds, out var error), error);
        Assert.Equal(expected, seconds, 3);
    }

    [Theory]
    [InlineData("start minus 10s", "before the start")]
    [InlineData("end + 10s", "after the end")]
    [InlineData("150%", "0% to 100%")]
    public void A_sum_that_leaves_the_video_says_so(string typed, string expected)
    {
        var context = new Helpers.TimeInput.Context(240, After: 200, Now: 90);

        Assert.False(Helpers.TimeInput.TryParseOne(typed, context, out _, out var error));
        Assert.Contains(expected, error);
    }

    [Fact]
    public void Now_and_last_refuse_when_there_is_nothing_to_name()
    {
        var bare = new Helpers.TimeInput.Context(240);

        Assert.False(Helpers.TimeInput.TryParseOne("now", bare, out _, out var noPlayhead));
        Assert.Contains("playhead", noPlayhead);

        Assert.False(Helpers.TimeInput.TryParseOne("last", bare, out _, out var noMark));
        Assert.Contains("bookmark", noMark);
    }

    [Fact]
    public void Rest_runs_from_the_last_mark_to_the_end()
    {
        Assert.True(Helpers.TimeInput.TryParse(
            "rest", new Helpers.TimeInput.Context(240, After: 200), out var parsed, out _));

        Assert.Equal(200, parsed.Start, 3);
        Assert.Equal(240, parsed.End!.Value, 3);
    }

    [Fact]
    public void All_is_the_whole_video_whatever_has_been_marked()
    {
        Assert.True(Helpers.TimeInput.TryParse(
            "all", new Helpers.TimeInput.Context(240, After: 200), out var parsed, out _));

        Assert.Equal(0, parsed.Start, 3);
        Assert.Equal(240, parsed.End!.Value, 3);
    }

    [Fact]
    public void A_random_clip_is_the_length_asked_for_and_fits()
    {
        var context = new Helpers.TimeInput.Context(240, After: 100);

        for (var i = 0; i < 50; i++)
        {
            Assert.True(Helpers.TimeInput.TryParse("random 30s", context, out var parsed, out _));

            Assert.Equal(30, parsed.End!.Value - parsed.Start, 2);
            Assert.InRange(parsed.Start, 100, 210);
            Assert.InRange(parsed.End!.Value, 130, 240);
        }
    }

    [Fact]
    public void A_random_clip_too_long_to_fit_is_refused()
    {
        var context = new Helpers.TimeInput.Context(240, After: 230);

        Assert.False(Helpers.TimeInput.TryParse("random 30s", context, out _, out var error));
        Assert.Contains("not", error);
    }

    // ------------------------------------------------------------------
    // A path cannot smuggle extra arguments onto a command line
    // ------------------------------------------------------------------

    [Fact]
    public void An_ordinary_path_is_quoted_for_the_command_line()
    {
        Assert.Equal("\"C:\\Videos\\a clip.mp4\"",
                     FFmpegService.Quoted(@"C:\Videos\a clip.mp4"));
    }

    [Theory]
    [InlineData("C:\\a\" -y -f lavfi -i nullsrc \"b.mp4")]
    [InlineData("\"")]
    public void A_path_carrying_a_quote_is_refused(string hostile)
    {
        // Windows has no legal filename containing a quote, so such a
        // path did not come from the filesystem — and left alone it would
        // close its own argument and leave the rest to be read as further
        // switches. Playlists are plain text and can say anything.
        Assert.Throws<ArgumentException>(() => FFmpegService.Quoted(hostile));
    }

    [Fact]
    public void A_whole_range_word_cannot_be_one_end_of_a_range()
    {
        Assert.False(Helpers.TimeInput.TryParse(
            "10 - rest", new Helpers.TimeInput.Context(240), out _, out var error));

        Assert.Contains("whole range", error);
    }
}
