using MpcHcVideoEditor.Services;
using Xunit;

namespace MpcHcVideoEditor.Tests;

/// <summary>
/// The overlay that holds notes for releases published after this build.
/// </summary>
/// <remarks>
/// These cover the shaping and the round trip rather than the file, because
/// the file lives beside the running executable and a test that wrote there
/// would be writing into the developer's own install.
/// </remarks>
public class ChangelogCacheTests
{
    [Fact]
    public void A_downloaded_body_loses_its_title_line()
    {
        const string body = """
            # MPC-HC Video Editor 5.9

            ## Playlists

            - Something new.
            """;

        var shaped = ChangelogCache.Normalise(body);

        Assert.DoesNotContain("# MPC-HC Video Editor", shaped);
        Assert.Contains("Something new", shaped);
    }

    [Fact]
    public void Sections_are_demoted_so_they_cannot_look_like_versions()
    {
        // The contract the whole format rests on: "## " starts a version.
        const string body = """
            ## Playlists

            - Something new.

            ## Merging

            - Something else.
            """;

        var shaped = ChangelogCache.Normalise(body);

        Assert.Contains("### Playlists", shaped);
        Assert.Contains("### Merging", shaped);
        Assert.DoesNotContain("\n## ", "\n" + shaped);
    }

    [Fact]
    public void A_shaped_body_reads_back_as_one_version()
    {
        // Normalise writes it, Parse reads it. If those two ever disagree the
        // cache silently turns one release into several.
        const string body = """
            # MPC-HC Video Editor 5.9

            ## Playlists

            - Something new.

            ## Merging

            - Something else.
            """;

        var file = "## 5.9 — 2026-12-01\n\n" + ChangelogCache.Normalise(body) + "\n";
        var entries = ChangelogService.Parse(file);

        Assert.Single(entries);
        Assert.Equal("5.9", entries[0].Version);
        Assert.Equal("2026-12-01", entries[0].Released);
        Assert.Contains("### Merging", entries[0].Notes);
    }

    [Fact]
    public void Shaping_leaves_an_ordinary_body_alone()
    {
        const string body = "- One thing.\n- Another thing.";

        Assert.Equal(body, ChangelogCache.Normalise(body));
    }

    [Fact]
    public void A_hard_wrapped_paragraph_becomes_one_bullet()
    {
        // Releases up to 5.7 are prose wrapped at eighty columns. No words
        // change — the paragraph gains "- " and loses the wrapping.
        const string body = """
            They are stored in one location now: a bookmarks folder inside
            the program folder. A copy is kept in %appdata%.
            """;

        Assert.Equal(
            "- They are stored in one location now: a bookmarks folder inside " +
            "the program folder. A copy is kept in %appdata%.",
            ChangelogCache.Normalise(body));
    }

    [Fact]
    public void Two_paragraphs_become_two_bullets()
    {
        const string body = "First thing\nwrapped.\n\nSecond thing.";

        Assert.Equal("- First thing wrapped.\n\n- Second thing.",
                     ChangelogCache.Normalise(body));
    }

    [Fact]
    public void A_wrapped_bullet_is_rejoined_rather_than_split()
    {
        const string body = "- The base merge operation will now show\n  a list of what you selected.";

        Assert.Equal("- The base merge operation will now show a list of what you selected.",
                     ChangelogCache.Normalise(body));
    }

    [Fact]
    public void A_fenced_block_is_left_exactly_as_written()
    {
        // The one place line breaks carry meaning — 4.8 pastes a progress
        // panel to show what it looked like.
        const string body = """
            Before:

            ```
            Merge files      File 1 of 10
            Preparing 4/10
            ```
            """;

        var shaped = ChangelogCache.Normalise(body);

        Assert.Contains("```\nMerge files      File 1 of 10\nPreparing 4/10\n```", shaped);
        Assert.Contains("- Before:", shaped);
    }

    [Fact]
    public void Headings_are_not_bulleted()
    {
        var shaped = ChangelogCache.Normalise("## Playlists\n\nSomething.");

        Assert.Contains("### Playlists", shaped);
        Assert.DoesNotContain("- ### Playlists", shaped);
    }

    [Fact]
    public void Shaping_a_shaped_body_changes_nothing()
    {
        // Idempotent, because the generator and this both run over the same
        // notes — once at release time and again if the update check sees
        // that release before the build catches up.
        const string body = """
            ## Playlists

            Some prose that
            was wrapped.

            - An existing bullet.
            """;

        var once = ChangelogCache.Normalise(body);

        Assert.Equal(once, ChangelogCache.Normalise(once));
    }

    [Theory]
    [InlineData("5.8.2")]   // the running build
    [InlineData("5.8.1")]   // older
    [InlineData("1.0")]
    [InlineData("")]
    [InlineData(null)]
    public void Nothing_at_or_below_the_running_version_is_kept(string? version)
    {
        // Remember is a no-op for these, so the overlay never duplicates what
        // the shipped file already has. Asserted through Entries rather than
        // by reading the file, which this test must not create.
        ChangelogCache.Remember(version, "2026-01-01", "- notes");

        Assert.DoesNotContain(ChangelogCache.Entries, e => e.Version == version);
    }

    [Fact]
    public void Entries_never_includes_the_running_version()
    {
        // Whatever is on disk from real use, the overlay only ever offers
        // versions newer than this build — that is what makes it self-
        // emptying once an update is taken.
        var running = UpdateCheckService.ParseVersion(Helpers.AppVersion.Display);

        Assert.All(ChangelogCache.Entries,
            e => Assert.True(UpdateCheckService.ParseVersion(e.Version) > running));
    }
}
