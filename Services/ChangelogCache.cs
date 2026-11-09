using System.IO;
using MpcHcVideoEditor.Helpers;

namespace MpcHcVideoEditor.Services;

/// <summary>
/// Keeps the notes for releases published after this build was made.
/// </summary>
/// <remarks>
/// <para>
/// The changelog compiled into the assembly is complete up to the moment the
/// build was produced and cannot grow — it is inside the .exe. This is the
/// thin layer over it: a handful of entries for versions that came later,
/// stored beside the settings file and merged in when About is opened.
/// </para>
/// <para>
/// Nothing is fetched for it. <see cref="UpdateCheckService"/> already asks
/// GitHub for the newest release and already reads that release's notes, of
/// which it previously kept six lines and discarded the rest. This keeps what
/// was already downloaded, so the whole feature costs no request it was not
/// making anyway.
/// </para>
/// <para>
/// Deliberately an overlay rather than a writable copy of the whole thing.
/// Copying the shipped notes out to a file and appending to it forever sounds
/// simpler, but the moment a new .exe arrives that copy is older than the one
/// inside the binary and every version needs reconciling against two sources.
/// A read-only baseline with a small overlay has no such problem: entries that
/// the baseline has caught up with are simply dropped.
/// </para>
/// <para>
/// It therefore empties itself. Only versions newer than the running build are
/// kept, so updating makes the entries redundant and the next load discards
/// them.
/// </para>
/// </remarks>
public static class ChangelogCache
{
    /// <summary>
    /// How many entries are kept. A cache that only holds releases newer than
    /// the running build is normally one deep; this is the bound for somebody
    /// who has not updated in a very long time.
    /// </summary>
    private const int Capacity = 20;

    private static string Path => System.IO.Path.Combine(PortablePaths.AppFolder, "changelog.local.md");

    /// <summary>
    /// Stores one release's notes, if they are for something newer than this
    /// build and not already held.
    /// </summary>
    /// <remarks>
    /// Quietly does nothing on any kind of failure, including a version that
    /// cannot be read or notes that are empty. This is an extra on top of a
    /// changelog that is already complete and correct; nothing it does is
    /// worth interrupting an update check over.
    /// </remarks>
    public static void Remember(string? version, string? released, string? notes)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(version) || string.IsNullOrWhiteSpace(notes)) return;

            var incoming = UpdateCheckService.ParseVersion(version);
            var running = UpdateCheckService.ParseVersion(AppVersion.Display);
            if (incoming is null || running is null || incoming <= running) return;

            var held = Load().ToList();
            if (held.Any(e => e.Version == version.Trim())) return;

            held.Add(new ChangelogEntry(version.Trim(), (released ?? "").Trim(), Normalise(notes)));

            // Newest first, matching the shipped file, and capped from that
            // end so the ones dropped are the oldest.
            var ordered = held
                .OrderByDescending(e => UpdateCheckService.ParseVersion(e.Version) ?? new Version(0, 0))
                .Take(Capacity)
                .ToList();

            Write(ordered);
        }
        catch (Exception ex)
        {
            Diagnostics.Swallowed("ChangelogCache.Remember", ex);
        }
    }

    /// <summary>
    /// The held entries that are still newer than this build, newest first.
    /// </summary>
    public static IReadOnlyList<ChangelogEntry> Entries
    {
        get
        {
            try
            {
                var running = UpdateCheckService.ParseVersion(AppVersion.Display);
                if (running is null) return Array.Empty<ChangelogEntry>();

                return Load()
                    .Where(e => (UpdateCheckService.ParseVersion(e.Version) ?? new Version(0, 0)) > running)
                    .OrderByDescending(e => UpdateCheckService.ParseVersion(e.Version) ?? new Version(0, 0))
                    .ToList();
            }
            catch (Exception ex)
            {
                Diagnostics.Swallowed("ChangelogCache.Entries", ex);
                return Array.Empty<ChangelogEntry>();
            }
        }
    }

    private static IReadOnlyList<ChangelogEntry> Load()
    {
        var path = Path;
        if (!File.Exists(path)) return Array.Empty<ChangelogEntry>();

        // The same shape as the shipped file, read by the same parser — one
        // format rather than two, so a change to either is caught by the
        // tests that already cover it.
        return ChangelogService.Parse(File.ReadAllText(path));
    }

    private static void Write(IReadOnlyList<ChangelogEntry> entries)
    {
        var text = new System.Text.StringBuilder();
        text.AppendLine("# Newer releases");
        text.AppendLine();
        text.AppendLine("Notes for versions published after this build, kept from the update");
        text.AppendLine("check. Safe to delete; it is rebuilt on the next check and discarded");
        text.AppendLine("once the build catches up.");
        text.AppendLine();

        foreach (var entry in entries)
        {
            text.Append("## ").Append(entry.Version);
            if (!string.IsNullOrEmpty(entry.Released)) text.Append(" — ").Append(entry.Released);
            text.AppendLine();
            text.AppendLine();
            text.AppendLine(entry.Notes);
            text.AppendLine();
        }

        File.WriteAllText(Path, text.ToString());
    }

    /// <summary>
    /// Brings a downloaded release body into the house style: no title line,
    /// sections one level down, and every paragraph a bullet on one line.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The sections move down so that "## " keeps meaning "a version starts
    /// here" and nothing else — without it a release whose notes contain
    /// "## Playlists" would be read back as a version called Playlists.
    /// </para>
    /// <para>
    /// The bulleting is what makes a release written as prose read like one
    /// written as a list. No words change: a paragraph gains "- " and loses
    /// the line breaks that were wrapping it at eighty columns, which the
    /// panel it is displayed in does not want anyway.
    /// </para>
    /// <para>
    /// The same transformation tools/update-changelog.ps1 applies when it
    /// writes CHANGELOG.md. The two must agree, or a version would change
    /// shape on the day the build caught up with it.
    /// </para>
    /// </remarks>
    internal static string Normalise(string body)
    {
        var titleLine = new System.Text.RegularExpressions.Regex(
            @"^#\s*(MPC-HC|MPC-BE)\s+Video\s+Editor",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        var heading = new System.Text.RegularExpressions.Regex(@"^#{1,6}\s");
        var bullet = new System.Text.RegularExpressions.Regex(@"^[-*]\s");

        var outLines = new List<string>();
        var run = new List<string>();
        var fenced = false;

        void Flush()
        {
            if (run.Count == 0) return;

            var text = string.Join(" ", run).Trim();
            run.Clear();

            if (text.Length == 0) return;
            outLines.Add("- " + bullet.Replace(text, "").TrimStart());
        }

        foreach (var raw in body.Replace("\r\n", "\n").Split('\n'))
        {
            var line = raw.TrimEnd();
            var trimmed = line.TrimStart();

            // Fenced blocks are the one place line breaks carry meaning.
            if (trimmed.StartsWith("```"))
            {
                Flush();
                outLines.Add(line);
                fenced = !fenced;
                continue;
            }

            if (fenced) { outLines.Add(line); continue; }

            if (titleLine.IsMatch(line)) continue;

            if (heading.IsMatch(trimmed))
            {
                Flush();

                if (line.StartsWith("## ")) outLines.Add("#" + line);
                else if (line.StartsWith("# ")) outLines.Add("##" + line);
                else outLines.Add(line);
                continue;
            }

            if (trimmed.Length == 0) { Flush(); outLines.Add(""); continue; }

            // A new bullet ends the one before it; anything else continues
            // whatever is open, which is how the wrapped lines in the older
            // notes were written.
            if (bullet.IsMatch(trimmed)) Flush();
            run.Add(trimmed);
        }

        Flush();

        var text = string.Join("\n", outLines);
        text = System.Text.RegularExpressions.Regex.Replace(text, "\n{3,}", "\n\n");
        return text.Trim('\n');
    }
}
