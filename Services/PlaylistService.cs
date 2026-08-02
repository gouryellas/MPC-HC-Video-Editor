using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace MpcHcVideoEditor.Services;

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
    /// Reads every <c>FileN=...</c> entry from a .pls file, in order.
    /// Returns an empty list if the file doesn't exist or has no entries.
    /// </summary>
    public List<string> ReadEntries(string plsPath)
    {
        var result = new List<string>();
        if (!File.Exists(plsPath)) return result;

        foreach (var line in File.ReadAllLines(plsPath))
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

        var sb = new StringBuilder();
        if (File.Exists(plsPath))
            sb.Append(File.ReadAllText(plsPath).TrimEnd());
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
    /// Deletes a .pls playlist file from disk. Returns <c>false</c> if the
    /// file didn't exist or couldn't be deleted (e.g. locked by another
    /// process).
    /// </summary>
    public bool DeletePlaylist(string plsPath)
    {
        if (!File.Exists(plsPath)) return false;
        try
        {
            File.Delete(plsPath);
            return true;
        }
        catch
        {
            return false;
        }
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
