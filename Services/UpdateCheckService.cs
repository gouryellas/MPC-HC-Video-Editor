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
public sealed record UpdateCheckResult(
    UpdateCheckStatus Status,
    string? LatestVersion,
    string ReleaseUrl,
    string? Error);

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

            // Greater than, not "different from": a developer running a build
            // ahead of the last release is not out of date.
            return new UpdateCheckResult(
                latest > running ? UpdateCheckStatus.UpdateAvailable : UpdateCheckStatus.UpToDate,
                display, url, null);
        }
        catch (Exception ex)
        {
            return Failed(ex.Message);
        }
    }

    private static UpdateCheckResult Failed(string error) =>
        new(UpdateCheckStatus.Failed, null, ReleasesPageUrl, error);

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
