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
    /// <summary>
    /// Runs something on an STA thread and hands back what it returned.
    /// </summary>
    /// <remarks>
    /// The note renderer builds real WPF elements, and several of those
    /// refuse to be constructed on a multi-threaded apartment — which is
    /// what xunit runs tests on. A thread per test is cheap at this scale
    /// and costs no package reference.
    ///
    /// The exception is rethrown rather than swallowed, or a renderer that
    /// threw would show up as a null result and a confusing assertion
    /// failure somewhere below.
    /// </remarks>
    private static T Sta<T>(Func<T> work)
    {
        T result = default!;
        Exception? failure = null;

        var thread = new Thread(() =>
        {
            try { result = work(); }
            catch (Exception ex) { failure = ex; }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (failure is not null)
            throw new InvalidOperationException("The renderer threw on the STA thread.", failure);

        return result;
    }

    /// <summary>
    /// The text a block actually carries.
    /// </summary>
    /// <remarks>
    /// Not <c>TextBlock.Text</c>. The renderer builds its paragraphs out of
    /// inline runs so that bold and code spans can be styled, and the Text
    /// property reads back empty when the content was supplied that way —
    /// it reflects what was assigned to it, not what the block will draw.
    /// Rendering is unaffected; only a test reading it back is.
    /// </remarks>
    private static string TextOf(System.Windows.Controls.TextBlock block) =>
        block.Inlines.Count > 0
            ? string.Concat(block.Inlines.OfType<System.Windows.Documents.Run>().Select(r => r.Text))
            : block.Text;

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
        Assert.Equal(expectedRuns, Sta(() => NoteRenderer.Inline(text).Count()));
    }

    [Fact]
    public void A_hard_wrapped_paragraph_becomes_one_block()
    {
        // Releases up to 5.7 are prose wrapped at about eighty columns. One
        // block per source line made each fragment re-wrap on its own inside
        // a much narrower panel, which is what made the old notes look
        // nothing like the new ones.
        const string notes = """
            ### Bookmark storage

            They are stored in one location now: a bookmarks folder inside
            the program folder. A copy is kept in %appdata%, so replacing
            the program folder will have these restored.
            """;

        var matching = Sta(() => NoteRenderer.Render(notes)
                                            .OfType<System.Windows.Controls.TextBlock>()
                                            .Count(b => TextOf(b).Contains("bookmarks folder")));

        Assert.Equal(1, matching);
    }

    [Fact]
    public void A_wrapped_bullet_stays_one_bullet()
    {
        const string notes = """
            - The base merge operation will now show a list of what you
              selected in the bookmarks area, with a step tracker.
            - The step tracker will work with any view.
            """;

        // Two bullets, not three or four — the continuation line belongs to
        // the bullet above it rather than standing at the margin as a new
        // paragraph.
        Assert.Equal(2, Sta(() => NoteRenderer.Render(notes)
                                              .OfType<System.Windows.Controls.Grid>().Count()));
    }

    [Fact]
    public void Both_bullet_markers_are_understood()
    {
        // The earliest releases used "*"; everything since has used "-".
        Assert.Equal(2, Sta(() => NoteRenderer.Render("- one\n* two")
                                              .OfType<System.Windows.Controls.Grid>().Count()));
    }

    [Theory]
    [InlineData("- a bullet")]
    [InlineData("* a bullet")]
    public void A_bullet_keeps_all_of_its_text(string line)
    {
        var text = Sta(() => NoteRenderer.Render(line)
                                         .OfType<System.Windows.Controls.Grid>().Single()
                                         .Children.OfType<System.Windows.Controls.TextBlock>()
                                         .Select(TextOf).Last());

        Assert.Equal("a bullet", text);
    }

    [Fact]
    public void A_dash_that_is_not_a_bullet_is_left_alone()
    {
        // An em dash opening a line, or bold text, must not lose its first
        // character to the bullet rule.
        var bullets = Sta(() => NoteRenderer.Render("**Relative entries** now resolve correctly.")
                                            .OfType<System.Windows.Controls.Grid>().Count());

        Assert.Equal(0, bullets);
    }

    [Fact]
    public void A_fenced_block_keeps_its_own_line_breaks()
    {
        const string notes = """
            Before:

            ```
            Merge files      File 1 of 10      27%
            Preparing 4/10
            ```

            After.
            """;

        var fenced = Sta(() => NoteRenderer.Render(notes)
                                           .OfType<System.Windows.Controls.Border>()
                                           .Select(b => b.Child)
                                           .OfType<System.Windows.Controls.ScrollViewer>()
                                           .Select(s => s.Content)
                                           .OfType<System.Windows.Controls.TextBlock>()
                                           .Select(TextOf).Single());

        Assert.Contains("File 1 of 10", fenced);
        Assert.Contains("\n", fenced);
        Assert.DoesNotContain("```", fenced);
    }

    [Fact]
    public void Inline_keeps_the_whole_line()
    {
        const string text = "a **bold** and `code` together";

        Assert.Equal(text.Replace("**", "").Replace("`", ""),
                     Sta(() => string.Concat(NoteRenderer.Inline(text).Select(r => r.Text))));
    }
}
