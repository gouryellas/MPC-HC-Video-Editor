using System.IO;

namespace MpcHcVideoEditor.Helpers;

/// <summary>
/// Answers whether the volume a path lives on is attached right now.
/// </summary>
/// <remarks>
/// <para>
/// Windows gives the same answer — no — whether a file was deleted or the
/// drive holding it is unplugged, and nothing asked about the file alone can
/// separate those two. The volume can be asked separately though, and that is
/// enough to stop the application announcing that a video is gone when an
/// external disk is merely detached.
/// </para>
/// <para>
/// An instance rather than a static helper because answers are cached for the
/// life of one pass: a playlist of two hundred entries spans a handful of
/// drives, and asking the OS once per drive rather than once per entry keeps a
/// menu that opens on the UI thread from making two hundred volume queries.
/// </para>
/// </remarks>
public sealed class DriveAvailability
{
    private readonly Dictionary<string, bool> _cache = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// The volume or share a path belongs to — <c>"H:\"</c>, or
    /// <c>@"\\server\share"</c>. Empty for a path relative or malformed enough
    /// to have no root at all.
    /// </summary>
    public static string RootOf(string path)
    {
        try { return Path.GetPathRoot(path) ?? string.Empty; }
        catch { return string.Empty; }
    }

    /// <summary>
    /// True when <paramref name="root"/> is present and ready to be read.
    /// </summary>
    /// <remarks>
    /// A path with no root — a relative one — reports as available. There is
    /// no volume to point at, so a file that cannot be found at a relative
    /// path is treated as genuinely absent rather than given the benefit of a
    /// doubt that nothing supports.
    /// </remarks>
    public bool IsAvailable(string root)
    {
        if (string.IsNullOrWhiteSpace(root)) return true;
        if (_cache.TryGetValue(root, out var cached)) return cached;

        bool available;
        try
        {
            available = root.StartsWith(@"\\")
                ? Directory.Exists(root)          // a share has no DriveInfo
                : new DriveInfo(root).IsReady;
        }
        catch
        {
            available = false;
        }

        _cache[root] = available;
        return available;
    }
}
