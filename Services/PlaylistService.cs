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
    /// <summary>
    /// Lists every <c>.pls</c> file in the given folder, sorted
    /// alphabetically. Returns an empty sequence if the folder is missing
    /// or empty.
    /// </summary>
    public IEnumerable<string> ListPlaylists(string folder)
    {
        if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
            return Array.Empty<string>();
        return Directory.GetFiles(folder, "*.pls")
            .OrderBy(f => f, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Windows-1252's 0x80–0x9F range, the only place it differs from
    /// Latin-1. Five of the 32 are genuinely unassigned and decode to the
    /// replacement character.
    /// </summary>
    private static readonly char[] Cp1252Punctuation =
    {
        '€', '�', '‚', 'ƒ', '„', '…', '†', '‡',
        'ˆ', '‰', 'Š', '‹', 'Œ', '�', 'Ž', '�',
        '�', '‘', '’', '“', '”', '•', '–', '—',
        '˜', '™', 'š', '›', 'œ', '�', 'ž', 'Ÿ'
    };

    /// <summary>
    /// Reads a .pls as text, working out its encoding instead of assuming one.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Playlists this application writes are UTF-8 with a BOM, but a .pls is
    /// an ordinary text file that MPC-HC, VLC and any number of older tools
    /// also write — frequently in the legacy Windows code page, where an
    /// accented character is a single byte. Read as UTF-8, every one of those
    /// bytes becomes U+FFFD, and a path containing U+FFFD matches nothing on
    /// disk: the entry shows as missing while the video sits right where it
    /// always was. Worse, rewriting the playlist then bakes the replacement
    /// character in for good.
    /// </para>
    /// <para>
    /// So: honour a BOM when there is one; otherwise try <em>strict</em>
    /// UTF-8, which rejects the byte sequences legacy text produces; and only
    /// when that fails fall back to Windows-1252. Genuine UTF-8 essentially
    /// never fails a strict decode, so the fallback cannot misclaim a file
    /// that was UTF-8 all along.
    /// </para>
    /// </remarks>
    private static string ReadAllTextDetected(string path)
    {
        var bytes = File.ReadAllBytes(path);

        // A byte-order mark settles it outright — no guessing required.
        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
            return Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3);
        if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE)
            return Encoding.Unicode.GetString(bytes, 2, bytes.Length - 2);
        if (bytes.Length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF)
            return Encoding.BigEndianUnicode.GetString(bytes, 2, bytes.Length - 2);

        try
        {
            return new UTF8Encoding(false, throwOnInvalidBytes: true).GetString(bytes);
        }
        catch (DecoderFallbackException)
        {
            return DecodeWindows1252(bytes);
        }
    }

    /// <summary>Splits <see cref="ReadAllTextDetected"/> into lines.</summary>
    private static string[] ReadAllLinesDetected(string path)
        => ReadAllTextDetected(path)
            .Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.None);

    /// <summary>
    /// Decodes bytes as Windows-1252.
    /// </summary>
    /// <remarks>
    /// Hand-rolled because <c>Encoding.GetEncoding(1252)</c> needs the
    /// System.Text.Encoding.CodePages package on modern .NET, and
    /// <see cref="Encoding.Latin1"/> — identical from 0xA0 up, and enough for
    /// the accented letters — leaves 0x80–0x9F as control codes, which is
    /// exactly where Windows-1252 keeps the curly quotes, dashes and ellipsis
    /// that turn up in filenames taken off the web.
    /// </remarks>
    private static string DecodeWindows1252(byte[] bytes)
    {
        var chars = new char[bytes.Length];
        for (int i = 0; i < bytes.Length; i++)
        {
            var b = bytes[i];
            chars[i] = b < 0x80  ? (char)b                          // ASCII
                     : b < 0xA0  ? Cp1252Punctuation[b - 0x80]      // the divergent range
                     :             (char)b;                         // 0xA0-0xFF match Unicode
        }
        return new string(chars);
    }

    /// <summary>
    /// Reads every <c>FileN=...</c> entry from a .pls file, in order.
    /// Returns an empty list if the file doesn't exist or has no entries.
    /// </summary>
    public List<string> ReadEntries(string plsPath)
    {
        var result = new List<string>();
        if (!File.Exists(plsPath)) return result;

        foreach (var line in ReadAllLinesDetected(plsPath))
        {
            var m = Regex.Match(line, @"^File\d+=(.*)$", RegexOptions.IgnoreCase);
            if (m.Success)
            {
                var path = m.Groups[1].Value.Trim();
                if (!string.IsNullOrEmpty(path))
                    result.Add(path);
            }
        }
        return result;
    }

    /// <summary>
    /// Appends one or more files to a .pls playlist. Files already present
    /// (case-insensitive) are skipped to avoid duplicates. Creates the
    /// file with a <c>[playlist]</c> header if it doesn't exist.
    /// </summary>
    public void AddFiles(string plsPath, IEnumerable<string> files)
    {
        var existing = new HashSet<string>(ReadEntries(plsPath), StringComparer.OrdinalIgnoreCase);
        int next = existing.Count + 1;

        // Detected, not assumed: appending to a legacy-encoded playlist with a
        // plain UTF-8 read would rewrite its existing accented entries as
        // U+FFFD and destroy them. Because every write here is UTF-8 with a
        // BOM, a legacy file is converted properly the first time it is
        // touched and stays correct from then on.
        var sb = new StringBuilder();
        if (File.Exists(plsPath))
            sb.Append(ReadAllTextDetected(plsPath).TrimEnd());
        else
            sb.AppendLine("[playlist]");

        foreach (var f in files)
        {
            if (existing.Contains(f)) continue;
            sb.AppendLine();
            sb.Append($"File{next}={f}");
            next++;
            existing.Add(f);
        }

        var dir = Path.GetDirectoryName(plsPath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);
        File.WriteAllText(plsPath, sb.ToString(), Encoding.UTF8);
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

        // Rewrite with fresh sequential numbering.
        var sb = new StringBuilder();
        sb.AppendLine("[playlist]");
        for (int i = 0; i < entries.Count; i++)
            sb.AppendLine($"File{i + 1}={entries[i]}");

        File.WriteAllText(plsPath, sb.ToString(), Encoding.UTF8);
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

        var sb = new StringBuilder();
        sb.AppendLine("[playlist]");
        for (int i = 0; i < kept.Count; i++)
            sb.AppendLine($"File{i + 1}={kept[i]}");

        File.WriteAllText(plsPath, sb.ToString(), Encoding.UTF8);
        return new PlaylistCleanup(removed, unverifiable);
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
        var dir = Path.GetDirectoryName(plsPath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);
        File.WriteAllText(plsPath, "[playlist]\n", Encoding.UTF8);
        return true;
    }
}
