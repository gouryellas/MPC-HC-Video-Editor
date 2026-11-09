using System.IO;
using MpcHcVideoEditor.Services;
using Xunit;

namespace MpcHcVideoEditor.Tests;

/// <summary>
/// The worked examples, kept apart from the rule-by-rule tests because they
/// are the ones a person checks by eye.
/// </summary>
public class FilenameExamplesTests
{
    private static readonly FilenameVocabulary Vocab = new()
    {
        Categories = new[] { "wedding" },
        Subjects = new[] { "vows" },
        FirstNames = new[] { "sarah" }
    };

    [Fact]
    public void A_correct_name_is_left_alone_with_or_without_a_vocabulary()
    {
        const string correct = "wedding-vows-sarah_williams-cries.mp4";

        Assert.Equal(correct, FilenameRules.Apply(correct));
        Assert.Equal(correct, FilenameRules.Apply(correct, Vocab));
    }

    [Fact]
    public void A_sentence_is_sorted_into_sections_by_the_vocabulary()
    {
        // Every section lands where it belongs. The filler words stay, as
        // description — nothing here knows that "video of us at the" is not
        // worth keeping, and dropping words on a guess is how a rename
        // loses something somebody wanted.
        var result = FilenameRules.Apply(
            "video of us at the wedding, sarah_williams cries during her vows!!!.mp4", Vocab);

        Assert.StartsWith("wedding-vows-sarah_williams-", result);
        Assert.EndsWith(".mp4", result);
    }

    [Fact]
    public void A_subject_behind_the_name_is_brought_in_front_of_it()
    {
        var result = FilenameRules.Apply("sarah_williams crying during vows.mp4", Vocab);

        Assert.StartsWith("vows-sarah_williams-", result);
    }

    [Fact]
    public void Without_a_vocabulary_that_same_name_keeps_its_order()
    {
        // Nothing marks "vows" as the subject, so it stays where it was
        // found rather than being promoted on a guess.
        Assert.Equal("sarah_williams-crying-during-vows.mp4",
                     FilenameRules.Apply("sarah_williams crying during vows.mp4"));
    }

    [Theory]
    // Each of the shapes from the list, fed back in, must come out as it
    // went in: whatever the rules produce they must also accept.
    [InlineData("wedding-vows-sarah_williams-cries[take1].mp4")]
    [InlineData("wedding-vows-sarah_williams-cries.mp4")]
    [InlineData("wedding-vows-sarah_williams.mp4")]
    [InlineData("wedding-vows-sarah.mp4")]
    [InlineData("wedding-vows[take1].mp4")]
    [InlineData("wedding-vows.mp4")]
    [InlineData("wedding-sarah_williams-cries.mp4")]
    [InlineData("wedding-cries.mp4")]
    [InlineData("wedding.mp4")]
    [InlineData("vows-sarah_williams-cries[take1].mp4")]
    [InlineData("vows-sarah_williams.mp4")]
    [InlineData("vows-cries.mp4")]
    [InlineData("vows.mp4")]
    [InlineData("sarah_williams-cries[take1].mp4")]
    [InlineData("sarah_williams.mp4")]
    [InlineData("cries[take1].mp4")]
    [InlineData("cries.mp4")]
    public void Every_valid_shape_survives_the_rules_unchanged(string name)
    {
        Assert.Equal(name, FilenameRules.Apply(name, Vocab));
    }
}
