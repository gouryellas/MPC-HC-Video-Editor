using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using MpcHcVideoEditor.Helpers;

namespace MpcHcVideoEditor.Services;

/// <summary>
/// What is actually known about the file behind a playlist entry.
/// </summary>
public enum PlaylistEntryStatus
{
    /// <summary>The file is there.</summary>
    Present,

    /// <summary>
    /// The file is not there and the drive holding it <em>is</em> connected,
    /// so it is genuinely gone. This is the only status that justifies
    /// removing an entry.
    /// </summary>
    Missing,

    /// <summary>
    /// The file could not be reached and the drive it lives on is not
    /// connected, so nothing can be said about whether it still exists. Not a
    /// weaker form of <see cref="Missing"/> — a different claim altogether,
    /// and one that must never be acted on as though it were the other.
    /// </summary>
    Unknown
}

/// <summary>One playlist entry, paired with what is known about its file.</summary>
public readonly record struct PlaylistEntry(string Path, PlaylistEntryStatus Status);

/// <summary>
/// What a cleanup pass removed, and what it deliberately did not.
/// </summary>
/// <param name="Removed">Entries whose files were confirmed gone.</param>
/// <param name="Unverifiable">
/// Entries left in place because the drive holding them is not connected, so
/// there was no honest way to judge them. Reported rather than silently
/// skipped: a caller that says "removed 3" while quietly passing over another
/// forty has told the user something misleading.
/// </param>
public readonly record struct PlaylistCleanup(
    List<string> Removed,
    List<string> Unverifiable);

/// <summary>
/// Reads, writes, and manages MPC-HC-compatible <c>.pls</c> playlist files.
/// </summary>
/// <remarks>
/// <para>
/// The .pls format is a simple INI-like text file:
/// </para>
/// <code>
/// [playlist]
/// File1=C:\path\to\video1.mp4
/// File2=C:\path\to\video2.mp4
/// ...
/// </code>
/// <para>
/// MPC-HC, VLC, and most other players can open these files directly. We
/// preserve the format exactly so our edits don't break compatibility.
/// </para>
/// </remarks>
public class PlaylistService
{
    /// <summary>The playlist formats this service reads and writes.</summary>
    public static readonly string[] Extensions = { ".pls", ".m3u8", ".m3u" };

    /// <summary>True when the path names a playlist this service understands.</summary>
    public static bool IsPlaylist(string? path) =>
        !string.IsNullOrWhiteSpace(path) &&
        Extensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Lists every playlist in the given folder, sorted alphabetically.
    /// Returns an empty sequence if the folder is missing or empty.
    /// </summary>
    public IEnumerable<string> ListPlaylists(string folder)
    {
        if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
            return Array.Empty<string>();

        return Directory.EnumerateFiles(folder)
            .Where(f => IsPlaylist(f))
            .OrderBy(f => f, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Reads a playlist's entries in order, as absolute paths. Returns an
    /// empty list if the file doesn't exist or has no entries.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Handles both the <c>FileN=</c> form of .pls and the bare-path form of
    /// m3u8, which is enough to tell them apart without trusting the
    /// extension — a mislabelled file still reads correctly.
    /// </para>
    /// <para>
    /// Relative entries are resolved against the <em>playlist's own folder</em>,
    /// not the process working directory. The latter is where they used to be
    /// resolved, which meant a perfectly good relative playlist was judged
    /// against wherever the app happened to be started from and reported as
    /// entirely missing. Resolving against the file is both correct and what
    /// makes a playlist portable: a folder and its .m3u8 can be copied to a USB
    /// stick and still work.
    /// </para>
    /// </remarks>
    public List<string> ReadEntries(string plsPath)
    {
        var result = new List<string>();
        if (!File.Exists(plsPath)) return result;

        var folder = Path.GetDirectoryName(Path.GetFullPath(plsPath)) ?? string.Empty;

        foreach (var raw in TextFile.ReadAllLines(plsPath))
        {
            var line = raw.Trim();
            if (line.Length == 0) continue;

            // m3u8 comments and directives; also skips .pls section headers.
            if (line[0] == '#' || (line[0] == '[' && line[^1] == ']')) continue;

            var m = Regex.Match(line, @"^File\d+=(.*)$", RegexOptions.IgnoreCase);
            var path = m.Success ? m.Groups[1].Value.Trim() : line;

            // .pls carries Title/Length keys too; anything else with an "="
            // and no path separator is metadata, not an entry.
            if (!m.Success && Regex.IsMatch(line, @"^(Title|Length|NumberOfEntries|Version)\d*=", RegexOptions.IgnoreCase))
                continue;

            if (path.Length == 0) continue;
            result.Add(Absolute(path, folder));
        }

        return result;
    }

    /// <summary>
    /// Resolves an entry against the playlist's folder when it is relative.
    /// </summary>
    /// <remarks>
    /// A URL is left exactly as written — this application cannot open one, and
    /// mangling it into a nonsense local path would turn "not supported" into
    /// "missing", which is a worse answer.
    /// </remarks>
    private static string Absolute(string entry, string playlistFolder)
    {
        try
        {
            if (entry.Contains("://", StringComparison.Ordinal)) return entry;
            if (Path.IsPathRooted(entry)) return entry;
            if (string.IsNullOrEmpty(playlistFolder)) return entry;
            return Path.GetFullPath(Path.Combine(playlistFolder, entry));
        }
        catch
        {
            // A path malformed enough to throw is returned untouched; the
            // caller will report it missing, which is the truth.
            return entry;
        }
    }

    /// <summary>
    /// Writes a playlist in whichever format its extension names.
    /// </summary>
    /// <remarks>
    /// <para>
    /// One writer for every mutation, so the three paths that used to emit
    /// <c>[playlist]</c> headers by hand cannot drift apart — and so a .m3u8
    /// is never silently rewritten as a .pls with the wrong extension.
    /// </para>
    /// <para>
    /// Always UTF-8 with a BOM. That is what makes a legacy-encoded playlist
    /// heal the first time it is touched: it is read with detection, and
    /// written back in a form nothing has to guess about.
    /// </para>
    /// </remarks>
    private static void WriteEntries(string playlistPath, IReadOnlyList<string> entries)
    {
        var sb = new StringBuilder();

        if (IsM3u(playlistPath))
        {
            sb.AppendLine("#EXTM3U");
            foreach (var e in entries) sb.AppendLine(e);
        }
        else
        {
            sb.AppendLine("[playlist]");
            for (int i = 0; i < entries.Count; i++)
                sb.AppendLine($"File{i + 1}={entries[i]}");

            // MPC-HC and VLC both cope without these, but they are part of the
            // format and some older players insist on them.
            sb.AppendLine($"NumberOfEntries={entries.Count}");
            sb.AppendLine("Version=2");
        }

        var dir = Path.GetDirectoryName(playlistPath);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        File.WriteAllText(playlistPath, sb.ToString(), Encoding.UTF8);
    }

    private static bool IsM3u(string path)
    {
        var ext = Path.GetExtension(path);
        return ext.Equals(".m3u8", StringComparison.OrdinalIgnoreCase)
            || ext.Equals(".m3u", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Converts a playlist to the format named by <paramref name="targetPath"/>'s
    /// extension, leaving the original untouched. Returns how many entries
    /// were written.
    /// </summary>
    public int ConvertPlaylist(string sourcePath, string targetPath)
    {
        var entries = ReadEntries(sourcePath);
        WriteEntries(targetPath, entries);
        return entries.Count;
    }

    /// <summary>
    /// Appends one or more files to a playlist. Files already present
    /// (case-insensitive) are skipped to avoid duplicates. Creates the file
    /// if it doesn't exist.
    /// </summary>
    public void AddFiles(string plsPath, IEnumerable<string> files)
    {
        // Read through the detecting reader and rewrite whole, rather than
        // appending text to whatever is already on disk. Appending preserved
        // the original bytes, which meant a legacy-encoded playlist stayed
        // legacy for its existing entries while gaining UTF-8 ones — a file in
        // two encodings at once. Rewriting converts it properly, once.
        var entries = ReadEntries(plsPath);
        var seen = new HashSet<string>(entries, StringComparer.OrdinalIgnoreCase);

        foreach (var f in files)
        {
            if (string.IsNullOrWhiteSpace(f) || !seen.Add(f)) continue;
            entries.Add(f);
        }

        WriteEntries(plsPath, entries);
    }

    /// <summary>
    /// Removes the entry at the given 1-based index from a .pls playlist,
    /// renumbering the remaining entries so they stay contiguous
    /// (<c>File1</c>, <c>File2</c>, …). Does nothing if the index is out
    /// of range. If the playlist becomes empty, the file is left in place
    /// with just the <c>[playlist]</c> header — we don't auto-delete it
    /// so the user's playlist name survives.
    /// </summary>
    /// <returns><c>true</c> if an entry was removed, <c>false</c> otherwise.</returns>
    public bool RemoveEntry(string plsPath, int index)
    {
        if (!File.Exists(plsPath)) return false;
        if (index < 1) return false;

        var entries = ReadEntries(plsPath);
        if (index > entries.Count) return false;

        entries.RemoveAt(index - 1);
        WriteEntries(plsPath, entries);
        return true;
    }

    /// <summary>
    /// Reads a playlist and says, for every entry, whether its file is there,
    /// confirmed gone, or simply out of reach.
    /// </summary>
    /// <remarks>
    /// The three-way answer exists because <see cref="File.Exists"/> gives a
    /// two-way one. It returns false for a deleted file and for a file on an
    /// unplugged drive alike, and collapsing those into "missing" is how a
    /// playlist pointing at an external disk comes to be reported as entirely
    /// dead — and, if anything acts on that report, cleared out.
    /// </remarks>
    public List<PlaylistEntry> ClassifyEntries(string plsPath)
    {
        var drives = new DriveAvailability();
        var result = new List<PlaylistEntry>();

        foreach (var path in ReadEntries(plsPath))
        {
            var status = File.Exists(path)
                ? PlaylistEntryStatus.Present
                : drives.IsAvailable(DriveAvailability.RootOf(path))
                    ? PlaylistEntryStatus.Missing      // drive is here, file is not
                    : PlaylistEntryStatus.Unknown;     // drive is away, nothing can be said

            result.Add(new PlaylistEntry(path, status));
        }

        return result;
    }

    /// <summary>
    /// Removes every entry whose file is confirmed gone, renumbering what
    /// remains so the <c>FileN</c> keys stay contiguous. Reports both what was
    /// removed and what was deliberately left alone; the file is not rewritten
    /// when there is nothing to remove.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Entries whose drive is not connected are <em>never</em> removed. Their
    /// status is unknown, and unknown is not grounds for deleting somebody's
    /// list — the point of <see cref="ClassifyEntries"/> is precisely that the
    /// two cases stop being interchangeable. This holds regardless of what the
    /// caller asks for: it is the guarantee, not a default.
    /// </para>
    /// <para>
    /// Classification happens here, at the moment of the write, so the answer
    /// acted on is the answer as it is now rather than one gathered earlier and
    /// gone stale.
    /// </para>
    /// </remarks>
    public PlaylistCleanup RemoveMissingEntries(string plsPath)
    {
        var removed = new List<string>();
        var unverifiable = new List<string>();
        if (!File.Exists(plsPath)) return new PlaylistCleanup(removed, unverifiable);

        var kept = new List<string>();
        foreach (var entry in ClassifyEntries(plsPath))
        {
            if (entry.Status == PlaylistEntryStatus.Missing)
            {
                removed.Add(entry.Path);
                continue;
            }

            if (entry.Status == PlaylistEntryStatus.Unknown) unverifiable.Add(entry.Path);
            kept.Add(entry.Path);
        }

        if (removed.Count == 0) return new PlaylistCleanup(removed, unverifiable);

        WriteEntries(plsPath, kept);
        return new PlaylistCleanup(removed, unverifiable);
    }

    /// <summary>
    /// One entry that was found again somewhere else.
    /// </summary>
    public readonly record struct RelocatedEntry(string OldPath, string NewPath);

    /// <summary>
    /// Looks for the files of confirmed-gone entries elsewhere and repoints
    /// them, rather than deleting the entries.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The counterpart to <see cref="RemoveMissingEntries"/>, and usually the
    /// one that is actually wanted: a file that cannot be found at its old path
    /// has far more often been moved or reorganised than deleted. Removal
    /// throws away the record of it; this puts the record back on the file.
    /// </para>
    /// <para>
    /// Only entries classified <see cref="PlaylistEntryStatus.Missing"/> are
    /// touched. An entry on a disconnected drive is not searched for — its file
    /// is probably exactly where it always was, and "finding" a same-named file
    /// somewhere else would silently repoint the playlist at a different video.
    /// </para>
    /// <para>
    /// Matching is by filename, and the first match wins. Searching stops at
    /// <paramref name="maxDepth"/> because a scan rooted at a drive letter over
    /// a spinning disk is otherwise unbounded, and this runs while the user
    /// waits.
    /// </para>
    /// </remarks>
    /// <param name="searchRoots">
    /// Folders to search. Callers normally pass the folders the surviving
    /// entries live in, plus anywhere the user nominates.
    /// </param>
    public List<RelocatedEntry> RelocateMissingEntries(
        string plsPath, IEnumerable<string> searchRoots, int maxDepth = 4,
        CancellationToken ct = default)
    {
        var moved = new List<RelocatedEntry>();
        if (!File.Exists(plsPath)) return moved;

        var classified = ClassifyEntries(plsPath);
        var gone = classified.Where(e => e.Status == PlaylistEntryStatus.Missing).ToList();
        if (gone.Count == 0) return moved;

        // One index of every candidate filename, built once. Searching per
        // entry would re-walk the same folders for every missing file.
        var index = BuildFileIndex(searchRoots, maxDepth, ct);

        var updated = new List<string>(classified.Count);
        foreach (var entry in classified)
        {
            if (entry.Status != PlaylistEntryStatus.Missing)
            {
                updated.Add(entry.Path);
                continue;
            }

            var name = Path.GetFileName(entry.Path);
            if (!string.IsNullOrEmpty(name) && index.TryGetValue(name, out var found))
            {
                moved.Add(new RelocatedEntry(entry.Path, found));
                updated.Add(found);
            }
            else
            {
                updated.Add(entry.Path);
            }
        }

        if (moved.Count > 0) WriteEntries(plsPath, updated);
        return moved;
    }

    /// <summary>
    /// Maps filename to full path for everything under the given roots, to the
    /// given depth. First occurrence wins.
    /// </summary>
    private static Dictionary<string, string> BuildFileIndex(
        IEnumerable<string> roots, int maxDepth, CancellationToken ct)
    {
        var index = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var seenRoots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var root in roots)
        {
            if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root)) continue;
            if (!seenRoots.Add(Path.GetFullPath(root))) continue;
            Walk(root, 0);
        }

        return index;

        void Walk(string folder, int depth)
        {
            ct.ThrowIfCancellationRequested();

            try
            {
                foreach (var file in Directory.EnumerateFiles(folder))
                {
                    var name = Path.GetFileName(file);
                    if (!index.ContainsKey(name)) index[name] = file;
                }

                if (depth >= maxDepth) return;
                foreach (var sub in Directory.EnumerateDirectories(folder))
                    Walk(sub, depth + 1);
            }
            catch
            {
                // A folder that cannot be read is skipped, not fatal. Denied
                // system directories are normal on any real drive.
            }
        }
    }

    /// <summary>
    /// Deletes a .pls playlist file from disk. Returns <c>false</c> if the
    /// file didn't exist or couldn't be deleted (e.g. locked by another
    /// process).
    /// </summary>
    /// <remarks>
    /// Goes through <see cref="RecycleBin"/> like every other deletion the app
    /// makes, so it honours the "delete files to the Recycle Bin" setting. It
    /// used to call <see cref="File.Delete"/> directly, which meant a playlist
    /// was destroyed outright even while everything else was recoverable — and
    /// a playlist is a hand-built list that can represent a lot of collecting.
    /// </remarks>
    public bool DeletePlaylist(string plsPath)
    {
        if (!File.Exists(plsPath)) return false;
        return RecycleBin.TryDelete(plsPath, out _);
    }

    /// <summary>
    /// Creates a new empty .pls playlist file with just the
    /// <c>[playlist]</c> header. If the file already exists, this is a
    /// no-op (returns <c>false</c>) — use <see cref="DeletePlaylist"/>
    /// first if you want to replace it.
    /// </summary>
    public bool CreatePlaylist(string plsPath)
    {
        if (File.Exists(plsPath)) return false;
        WriteEntries(plsPath, Array.Empty<string>());
        return true;
    }
}
