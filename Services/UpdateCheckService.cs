using System.Net.Http;
using System.Text.Json;
using MpcHcVideoEditor.Helpers;

namespace MpcHcVideoEditor.Services;

/// <summary>What a completed update check found.</summary>
public enum UpdateCheckStatus
{
    /// <summary>The latest published release is newer than this build.</summary>
    UpdateAvailable,

    /// <summary>This build is the latest release, or is ahead of it.</summary>
    UpToDate,

    /// <summary>
    /// The check did not complete. <see cref="UpdateCheckResult.Error"/> says
    /// why, for the caller that asked for the check and is owed an answer.
    /// </summary>
    Failed
}

/// <param name="Status">What the check concluded.</param>
/// <param name="LatestVersion">Published version, without its leading <c>v</c>. Null if it could not be read.</param>
/// <param name="ReleaseUrl">Page to send the user to — the specific release when known, the releases list otherwise.</param>
/// <param name="Error">Why the check failed, or null.</param>
/// <param name="Highlights">
/// A few lines from the release notes saying what changed. Empty when the
/// release has no notes, or none this could make a list out of — the notice
/// leaves the section out rather than showing an empty box.
/// </param>
public sealed record UpdateCheckResult(
    UpdateCheckStatus Status,
    string? LatestVersion,
    string ReleaseUrl,
    string? Error,
    IReadOnlyList<string> Highlights);

/// <summary>
/// Asks GitHub whether a newer release has been published.
/// </summary>
/// <remarks>
/// Reads a version number and nothing else. Nothing is downloaded and nothing
/// installs itself — the app is a portable folder, and replacing it is the
/// user's business. This exists so that a fix does not sit unnoticed in a
/// release nobody knew was there.
///
/// Every failure resolves to <see cref="UpdateCheckStatus.Failed"/> rather than
/// an exception. The startup caller discards it, because a machine that is
/// offline at launch has not encountered a problem worth a dialog; the manual
/// caller shows it, because someone who clicked "check" is owed an answer
/// either way.
/// </remarks>
public static class UpdateCheckService
{
    /// <summary>Canonical repository URL.</summary>
    /// <remarks>
    /// The one copy of this string. <see cref="Dialogs.AboutDialog"/> takes its
    /// own constant from here — Dialogs may depend on Services, not the reverse.
    /// </remarks>
    public const string RepositoryUrl = "https://github.com/gouryellas/MPC-HC-Video-Editor";

    /// <summary>Where a user is sent to get a newer build.</summary>
    public const string ReleasesPageUrl = RepositoryUrl + "/releases";

    private const string LatestReleaseApi =
        "https://api.github.com/repos/gouryellas/MPC-HC-Video-Editor/releases/latest";

    private static readonly HttpClient _http = CreateClient();

    private static HttpClient CreateClient()
    {
        // Short: this runs at startup, and a GitHub that is not answering must
        // not be something the user waits on.
        var http = new HttpClient { Timeout = TimeSpan.FromSeconds(8) };

        // GitHub rejects API requests that carry no User-Agent outright, so
        // this is required rather than polite.
        http.DefaultRequestHeaders.UserAgent.ParseAdd($"MPC-HC-Video-Editor/{AppVersion.Display}");
        http.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        return http;
    }

    /// <summary>
    /// Asks for the latest release and compares it with the running build.
    /// Never throws.
    /// </summary>
    public static async Task<UpdateCheckResult> CheckAsync(CancellationToken ct = default)
    {
        try
        {
            // "latest" excludes drafts and pre-releases at the server, so a
            // release published for testing never prompts anyone.
            using var response = await _http.GetAsync(LatestReleaseApi, ct);

            if (!response.IsSuccessStatusCode)
                return Failed($"GitHub answered {(int)response.StatusCode} {response.ReasonPhrase}.");

            using var stream = await response.Content.ReadAsStreamAsync(ct);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);

            var tag = doc.RootElement.TryGetProperty("tag_name", out var tagName)
                ? tagName.GetString()
                : null;
            var page = doc.RootElement.TryGetProperty("html_url", out var htmlUrl)
                ? htmlUrl.GetString()
                : null;

            var latest = ParseVersion(tag);
            var running = ParseVersion(AppVersion.Display);

            if (latest is null || running is null)
                return Failed("The published version number could not be read.");

            var url = string.IsNullOrWhiteSpace(page) ? ReleasesPageUrl : page!;
            var display = Strip(tag);

            var notes = doc.RootElement.TryGetProperty("body", out var body)
                ? body.GetString()
                : null;

            // Greater than, not "different from": a developer running a build
            // ahead of the last release is not out of date.
            return new UpdateCheckResult(
                latest > running ? UpdateCheckStatus.UpdateAvailable : UpdateCheckStatus.UpToDate,
                display, url, null, Highlights(notes));
        }
        catch (Exception ex)
        {
            return Failed(ex.Message);
        }
    }

    private static UpdateCheckResult Failed(string error) =>
        new(UpdateCheckStatus.Failed, null, ReleasesPageUrl, error, Array.Empty<string>());

    /// <summary>How many lines the notice will show.</summary>
    /// <remarks>
    /// Six. The notice is a reason to go and read the page, not a replacement
    /// for it — and the window it sits in cannot grow without limit.
    /// </remarks>
    private const int MaxHighlights = 6;

    /// <summary>One line's worth of characters before it is cut short.</summary>
    private const int MaxHighlightLength = 90;

    /// <summary>
    /// Sections every release carries, which say nothing about what is new in
    /// this one.
    /// </summary>
    private static readonly string[] _boilerplate =
    {
        "requirements", "verify", "checksums", "install", "installation",
        "upgrading", "downloads", "files", "known issues", "credits"
    };

    /// <summary>True for a heading that is the same in every release.</summary>
    /// <remarks>
    /// Matched on the opening word or two rather than the whole heading, so
    /// "Verify your download" is caught by "verify" without needing the exact
    /// wording of every release to be listed here.
    /// </remarks>
    private static bool Boilerplate(string text) =>
        _boilerplate.Any(b => text.StartsWith(b, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Pulls a short list of what changed out of a release's notes.
    /// </summary>
    /// <remarks>
    /// Release notes here are prose under <c>##</c> headings, and those headings
    /// are already the one-line summary of each change — "Cuts can no longer
    /// overlap" — so they make the list.
    ///
    /// Bullets are the fallback, not the first choice, which is the opposite of
    /// what it looks like it should be. The only bulleted list in these notes is
    /// the requirements block at the bottom, so preferring bullets produced a
    /// "what changed" list that read "Windows, .NET 8 desktop runtime" and
    /// mentioned not one of the changes. They are still worth reading for a
    /// release that has no headings at all, which is the only case left.
    ///
    /// The top-level <c>#</c> title is skipped: it repeats the version number
    /// the notice is already announcing in its headline. <see cref="Boilerplate"/>
    /// takes out the sections that are in every release rather than new to this
    /// one.
    ///
    /// Nothing here tries to understand markdown. It reads the two line shapes
    /// this repository's notes actually use, strips the emphasis marks, and
    /// gives up gracefully — an empty list leaves the section off the window
    /// rather than showing a box with nothing in it.
    /// </remarks>
    internal static IReadOnlyList<string> Highlights(string? notes)
    {
        if (string.IsNullOrWhiteSpace(notes)) return Array.Empty<string>();

        var bullets = new List<string>();
        var headings = new List<string>();

        // Set while inside Requirements or the like, so its bullets are not
        // collected either. The heading is dropped by name; the list under it
        // would otherwise survive the heading that explains it.
        var skipping = false;

        foreach (var raw in notes.Replace("\r\n", "\n").Split('\n'))
        {
            var line = raw.Trim();
            if (line.Length < 2) continue;

            if (line[0] is '-' or '*' or '•' && line[1] is ' ' or '\t')
            {
                if (!skipping) Add(bullets, line[1..]);
            }
            else if (line[0] == '#')
            {
                var hashes = line.TakeWhile(c => c == '#').Count();
                var title = line[hashes..].Replace("**", "").Replace("`", "").Trim(' ', '\t', '*', '_');

                skipping = Boilerplate(title);

                // Level 1 is the release title, not a change.
                if (hashes >= 2) Add(headings, title);
            }
        }

        return headings.Count > 0 ? headings : bullets;

        void Add(List<string> into, string text)
        {
            if (into.Count >= MaxHighlights) return;

            // Emphasis marks and inline code ticks are formatting, and there is
            // no formatting on a plain TextBlock for them to survive as.
            text = text.Replace("**", "").Replace("`", "").Trim(' ', '\t', '*', '_');

            if (text.Length == 0 || Boilerplate(text)) return;

            if (text.Length > MaxHighlightLength)
                text = text[..(MaxHighlightLength - 1)].TrimEnd() + "…";

            into.Add(text);
        }
    }

    /// <summary>Drops a leading <c>v</c> so a tag reads as a version.</summary>
    private static string? Strip(string? tag)
    {
        var s = tag?.Trim();
        if (string.IsNullOrEmpty(s)) return null;
        return s[0] is 'v' or 'V' ? s[1..] : s;
    }

    /// <summary>
    /// Reads a release tag or an assembly version into something comparable,
    /// or null if there are no numbers in it at all.
    /// </summary>
    /// <remarks>
    /// Padded to four parts so <c>4.4</c> and <c>4.4.0</c> compare equal rather
    /// than the shorter one sorting first, which is what
    /// <see cref="Version"/> does with absent components. Tags in this
    /// repository run from <c>v1.1</c> to <c>v2</c> to <c>v3.0.9</c>, so both
    /// the two-part and the bare-major forms have to land somewhere sensible.
    /// Anything trailing a number is ignored, so a <c>4.5-beta</c> tag reads as
    /// 4.5 rather than failing outright.
    /// </remarks>
    internal static Version? ParseVersion(string? raw)
    {
        var s = Strip(raw);
        if (string.IsNullOrEmpty(s)) return null;

        var parts = new List<int>();
        foreach (var piece in s.Split('.'))
        {
            var digits = new string(piece.TakeWhile(char.IsAsciiDigit).ToArray());
            if (digits.Length == 0) break;
            if (!int.TryParse(digits, out var value)) break;

            parts.Add(value);
            if (parts.Count == 4) break;
        }

        if (parts.Count == 0) return null;
        while (parts.Count < 4) parts.Add(0);

        return new Version(parts[0], parts[1], parts[2], parts[3]);
    }
}
