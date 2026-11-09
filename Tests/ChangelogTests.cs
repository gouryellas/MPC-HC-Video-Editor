using System.Linq;
using MpcHcVideoEditor.Helpers;
using MpcHcVideoEditor.Services;
using Xunit;

namespace MpcHcVideoEditor.Tests;

/// <summary>
/// The changelog's format is a contract between the generator that writes
/// CHANGELOG.md and the About box that reads it. These hold both ends to it.
/// </summary>
public class ChangelogTests
{
    private const string Sample = """
        # Changelog

        Some preamble that is not a version.

        ## 5.8.2 — 2026-11-08

        ### Playlists

        - New "Open all" for each playlist.
        - You can drop .m3u files now.

        ## 5.8.1 — 2026-11-04

        ### Merging

        - Fixed the remaining time.

        ## 2.1

        First one, no date published.
        """;

    [Fact]
    public void Finds_every_version_and_nothing_else()
    {
        var entries = ChangelogService.Parse(Sample);

        Assert.Equal(new[] { "5.8.2", "5.8.1", "2.1" }, entries.Select(e => e.Version));
    }

    [Fact]
    public void Keeps_the_order_the_file_is_written_in()
    {
        // Newest first, because that is how the file is generated and the
        // About box does not sort — a parser that reordered would be the
        // only thing deciding, invisibly.
        var entries = ChangelogService.Parse(Sample);

        Assert.Equal("5.8.2", entries[0].Version);
        Assert.Equal("2.1", entries[^1].Version);
    }

    [Fact]
    public void Reads_the_date_when_there_is_one()
    {
        var entries = ChangelogService.Parse(Sample);

        Assert.Equal("2026-11-08", entries[0].Released);
    }

    [Fact]
    public void Lists_a_version_that_never_got_a_date()
    {
        // Dropping it would silently lose a release from the history.
        var last = ChangelogService.Parse(Sample)[^1];

        Assert.Equal("2.1", last.Version);
        Assert.Equal("", last.Released);
        Assert.Equal("2.1", last.Heading);
    }

    [Fact]
    public void A_section_inside_a_release_is_not_mistaken_for_a_version()
    {
        // The whole reason the generator demotes release bodies to "###".
        var entries = ChangelogService.Parse(Sample);

        Assert.DoesNotContain(entries, e => e.Version == "Playlists");
        Assert.Contains("### Playlists", entries[0].Notes);
    }

    [Fact]
    public void Notes_stop_at_the_next_version()
    {
        var first = ChangelogService.Parse(Sample)[0];

        Assert.Contains("Open all", first.Notes);
        Assert.DoesNotContain("Fixed the remaining time", first.Notes);
    }

    [Fact]
    public void Preamble_before_the_first_version_is_not_an_entry()
    {
        var entries = ChangelogService.Parse(Sample);

        Assert.DoesNotContain(entries, e => e.Notes.Contains("Some preamble"));
    }

    [Fact]
    public void The_shipped_changelog_parses_and_holds_this_version()
    {
        // The real file, through the real resource loader — which is what
        // catches a CHANGELOG.md that was renamed, dropped from the csproj,
        // or regenerated into a shape the parser no longer reads.
        var entries = ChangelogService.Entries;

        Assert.NotEmpty(entries);
        Assert.Contains(entries, e => e.Version == AppVersion.Display);
    }

    [Theory]
    [InlineData("plain text", 1)]
    [InlineData("a **bold** word", 3)]
    [InlineData("a `code` word", 3)]
    [InlineData("an unmatched ** marker", 1)]
    [InlineData("an unmatched ` marker", 1)]
    public void Inline_markers_split_only_when_they_close(string text, int expectedRuns)
    {
        // An unmatched marker stays as written. Eating a character somebody
        // meant is worse than showing one they did not.
        Assert.Equal(expectedRuns, NoteRenderer.Inline(text).Count());
    }

    [Fact]
    public void Inline_keeps_the_whole_line()
    {
        const string text = "a **bold** and `code` together";

        Assert.Equal(text.Replace("**", "").Replace("`", ""),
                     string.Concat(NoteRenderer.Inline(text).Select(r => r.Text)));
    }
}
