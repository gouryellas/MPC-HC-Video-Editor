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
}
