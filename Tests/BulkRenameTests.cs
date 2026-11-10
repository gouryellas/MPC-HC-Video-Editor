using System.IO;
using System.Linq;
using MpcHcVideoEditor.Services;
using Xunit;

namespace MpcHcVideoEditor.Tests;

/// <summary>
/// The preview a folder produces, and the rename that follows it.
/// </summary>
/// <remarks>
/// Over real files in a temporary folder. The parts worth testing here are
/// the ones the rules alone cannot answer: what happens when two names
/// collide, and whether a file that is already right is left alone.
/// </remarks>
public class BulkRenameTests
{
    private static string Scratch()
    {
        var dir = Path.Combine(Path.GetTempPath(), "mve-rename-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static void Touch(string dir, string name) =>
        File.WriteAllText(Path.Combine(dir, name), "x");

    private static IReadOnlyList<RenameProposal> Preview(string dir, FilenameVocabulary? vocab = null)
    {
        var names = Directory.EnumerateFiles(dir)
                             .Select(Path.GetFileName).OfType<string>()
                             .OrderBy(n => n, StringComparer.OrdinalIgnoreCase);

        return FilenameRules.Propose(names, vocab);
    }

    [Fact]
    public void A_folder_of_untidy_names_comes_back_tidy()
    {
        var dir = Scratch();
        try
        {
            Touch(dir, "WEDDING  Vows!! [Take 1].mp4");
            Touch(dir, "Holiday   Beach.mkv");

            var proposals = Preview(dir);

            Assert.Equal("holiday-beach.mkv", proposals[0].Proposed);
            Assert.Equal("wedding-vows[take-1].mp4", proposals[1].Proposed);
            Assert.All(proposals, p => Assert.True(p.Changes));
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public void A_file_already_right_is_not_offered_for_renaming()
    {
        var dir = Scratch();
        try
        {
            Touch(dir, "wedding-vows-sarah_williams-cries[take1].mp4");

            var proposal = Assert.Single(Preview(dir));

            Assert.False(proposal.Changes);
            Assert.Equal("", proposal.Note);
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public void Two_names_that_would_collide_are_flagged_and_not_renumbered()
    {
        var dir = Scratch();
        try
        {
            Touch(dir, "Clip One.mp4");
            Touch(dir, "clip  one.mp4");

            var proposals = Preview(dir);

            Assert.All(proposals, p => Assert.Equal("clip-one.mp4", p.Proposed));
            Assert.Contains(proposals, p => p.Note.Contains("already takes this name"));
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public void Renaming_moves_the_file_and_leaves_the_folder_otherwise_alone()
    {
        var dir = Scratch();
        try
        {
            Touch(dir, "Holiday   Beach.mkv");

            var proposal = Assert.Single(Preview(dir));
            File.Move(Path.Combine(dir, proposal.Original),
                      Path.Combine(dir, proposal.Proposed));

            Assert.True(File.Exists(Path.Combine(dir, "holiday-beach.mkv")));
            Assert.Single(Directory.EnumerateFiles(dir));
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public void A_second_pass_over_a_renamed_folder_finds_nothing_to_do()
    {
        // The property that makes the feature safe to run twice. If the
        // rules did not accept their own output, a tidy folder would be
        // churned every time it was previewed.
        var dir = Scratch();
        try
        {
            Touch(dir, "WEDDING  Vows!! [Take 1].mp4");
            Touch(dir, "Holiday   Beach.mkv");

            foreach (var p in Preview(dir).Where(p => p.Changes))
                File.Move(Path.Combine(dir, p.Original), Path.Combine(dir, p.Proposed));

            Assert.All(Preview(dir), p => Assert.False(p.Changes));
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public void The_vocabulary_is_cleaned_the_same_way_a_filename_is()
    {
        // A category typed as "Wedding Party" has to match a word taken out
        // of a cleaned filename, or it would never match anything and would
        // fail silently.
        var vocab = new FilenameVocabulary
        {
            Words = new[] { FilenameRules.Clean("Wedding Party") }
        };

        Assert.Equal("wedding-party", vocab.Words.Single());
    }
}
