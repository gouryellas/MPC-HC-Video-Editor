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
