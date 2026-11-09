using System.IO;
using System.Text.RegularExpressions;
using System.Windows;

namespace MpcHcVideoEditor.Services;

/// <summary>One released version and the notes published with it.</summary>
/// <param name="Version">The number alone, e.g. "5.8.2".</param>
/// <param name="Released">The publication date, as written in the file.</param>
/// <param name="Notes">The notes, still in their markdown.</param>
public sealed record ChangelogEntry(string Version, string Released, string Notes)
{
    /// <summary>What the expander shows when it is shut.</summary>
    public string Heading => string.IsNullOrEmpty(Released)
        ? Version
        : $"{Version}        {Released}";
}

/// <summary>
/// Reads the shipped changelog into one entry per released version.
/// </summary>
/// <remarks>
/// <para>
/// The file is <c>CHANGELOG.md</c> at the root of the repository, built from
/// the published GitHub releases and compiled in as a resource. Read from the
/// assembly rather than from disk so the portable single-file build has it
/// too — there is no folder beside that .exe to put it in.
/// </para>
/// <para>
/// Deliberately not fetched from GitHub at run time. The notes for a version
/// cannot change after it ships, so there is nothing to be gained by asking
/// the network for them, and plenty to lose: the About box would need to
/// handle being offline, being rate-limited, and being slow, to show text
/// that was already known when the build was made.
/// </para>
/// <para>
/// The format is a contract with the generator, and a narrow one: a line
/// beginning <c>"## "</c> starts a version and nothing else does. The
/// generator demotes each release body's own <c>##</c> sections to
/// <c>###</c> for exactly this reason, so a section called "## Playlists"
/// inside a release cannot be mistaken for a version called "Playlists".
/// </para>
/// </remarks>
public static class ChangelogService
{
    private static IReadOnlyList<ChangelogEntry>? _cached;

    /// <summary>
    /// Every released version, newest first. Empty if the file is missing or
    /// unreadable — the About box simply says so rather than failing to open.
    /// </summary>
    public static IReadOnlyList<ChangelogEntry> Entries => _cached ??= Read();

    private static IReadOnlyList<ChangelogEntry> Read()
    {
        try
        {
            var text = LoadText();
            return string.IsNullOrWhiteSpace(text)
                ? Array.Empty<ChangelogEntry>()
                : Parse(text);
        }
        catch (Exception ex)
        {
            Helpers.Diagnostics.Swallowed("Changelog.Read", ex);
            return Array.Empty<ChangelogEntry>();
        }
    }

    /// <summary>
    /// Reads the compiled-in file.
    /// </summary>
    /// <remarks>
    /// Names the assembly rather than using the short
    /// <c>pack://application:,,,/CHANGELOG.md</c> form. The short form is
    /// resolved against the *entry* assembly, which is this one when the
    /// program runs normally and is not when anything else hosts it — a test
    /// harness, a designer. Naming the assembly the resource actually lives
    /// in is correct either way, and the name is escaped because this one
    /// has spaces in it.
    /// </remarks>
    private static string? LoadText()
    {
        var assembly = System.Reflection.Assembly.GetExecutingAssembly().GetName().Name;
        var uri = new Uri($"pack://application:,,,/{Uri.EscapeDataString(assembly ?? "")};component/CHANGELOG.md",
                          UriKind.Absolute);

        var resource = Application.GetResourceStream(uri);
        if (resource is null) return null;

        using var reader = new StreamReader(resource.Stream);
        return reader.ReadToEnd();
    }

    /// <summary>
    /// Splits the file on its version headings. Internal so the tests can
    /// put known text through it without a resource to load.
    /// </summary>
    internal static IReadOnlyList<ChangelogEntry> Parse(string text)
    {
        // "## 5.8.2 — 2026-11-08", with the date optional: a version that
        // never got one should still be listed rather than dropped.
        var heading = new Regex(@"^##\s+(?<version>\S+)(?:\s+[—-]\s+(?<date>.+))?$",
                                RegexOptions.Multiline);

        var matches = heading.Matches(text.Replace("\r\n", "\n"));
        var entries = new List<ChangelogEntry>(matches.Count);

        for (int i = 0; i < matches.Count; i++)
        {
            var start = matches[i].Index + matches[i].Length;
            var end = i + 1 < matches.Count ? matches[i + 1].Index : text.Length;

            if (end < start) continue;

            entries.Add(new ChangelogEntry(
                matches[i].Groups["version"].Value,
                matches[i].Groups["date"].Success ? matches[i].Groups["date"].Value.Trim() : "",
                text[start..end].Trim('\n', '\r', ' ')));
        }

        return entries;
    }
}
