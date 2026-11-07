using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace MpcHcVideoEditor.Helpers;

/// <summary>
/// Somewhere for a failure to go when it is caught and carried on from.
/// </summary>
/// <remarks>
/// <para>
/// This program catches a great deal and says nothing about most of it, which
/// is usually right — a thumbnail that will not render should not stop a
/// merge. But the ones worth ignoring and the ones hiding a real fault look
/// identical from outside, and that has cost: a toast notification was broken
/// for the entire life of the feature because one blanket catch swallowed the
/// exception thrown every single time it was constructed, and nothing
/// anywhere recorded that it had happened.
/// </para>
/// <para>
/// So the catches that matter write here instead of discarding. Nothing is
/// shown to the user and nothing is interrupted; the entry simply exists, for
/// the diagnostics panel to show and for a bug report to carry.
/// </para>
/// <para>
/// Deliberately not a logging framework. A ring of recent entries in memory
/// answers "what just went wrong", which is the question being asked, and
/// costs nothing when the answer is "nothing". The file is append-only and
/// capped, for the case where the program did not survive long enough to be
/// asked.
/// </para>
/// </remarks>
public static class Diagnostics
{
    /// <summary>One thing that went wrong and was carried on from.</summary>
    /// <param name="At">When, in local time.</param>
    /// <param name="Where">The operation, e.g. "BookmarkService.LoadFromCsv".</param>
    /// <param name="What">The failure, as short as it can honestly be.</param>
    public readonly record struct Entry(DateTime At, string Where, string What)
    {
        public override string ToString() => $"{At:HH:mm:ss}  {Where}  {What}";
    }

    /// <summary>
    /// How many are kept. Enough to cover a session's worth of the occasional
    /// miss, and far too few to matter to memory.
    /// </summary>
    private const int Capacity = 200;

    private static readonly ConcurrentQueue<Entry> Recent = new();
    private static readonly object FileLock = new();

    /// <summary>
    /// Stops a failure that repeats every poll from filling the ring and
    /// pushing out everything else. Counted rather than dropped, so the
    /// hundredth is still visible as a count.
    /// </summary>
    private static readonly ConcurrentDictionary<string, int> Seen = new();

    private static string? _logPath;

    /// <summary>Entries so far, oldest first.</summary>
    public static IReadOnlyList<Entry> Entries => Recent.ToArray();

    /// <summary>Whether anything has gone wrong this session.</summary>
    public static bool HasEntries => !Recent.IsEmpty;

    /// <summary>Notes a failure that was caught and not shown.</summary>
    public static void Swallowed(string where, Exception ex) =>
        Record(where, Describe(ex));

    /// <summary>Notes something wrong that is not an exception.</summary>
    public static void Note(string where, string what) => Record(where, what);

    /// <summary>Everything recorded, as text, for a bug report.</summary>
    public static string Report()
    {
        var sb = new StringBuilder();

        foreach (var entry in Recent)
        {
            sb.Append(entry.At.ToString("yyyy-MM-dd HH:mm:ss")).Append("  ")
              .Append(entry.Where).Append("  ")
              .AppendLine(entry.What);
        }

        foreach (var (key, count) in Seen.Where(p => p.Value > 1))
            sb.AppendLine($"(repeated {count}×) {key}");

        return sb.ToString();
    }

    public static void Clear()
    {
        while (Recent.TryDequeue(out _)) { }
        Seen.Clear();
    }

    private static void Record(string where, string what)
    {
        // The same failure on every poll is one fact, not six hundred.
        var key = where + "|" + what;
        var count = Seen.AddOrUpdate(key, 1, (_, n) => n + 1);
        if (count > 3) return;

        Recent.Enqueue(new Entry(DateTime.Now, where, what));
        while (Recent.Count > Capacity) Recent.TryDequeue(out _);

        Debug.WriteLine($"[swallowed] {where}: {what}");
        AppendToFile(where, what);
    }

    /// <summary>
    /// The useful line of an exception. The type and message, and the first
    /// frame of the stack that belongs to this program — which is the one
    /// that says where to look.
    /// </summary>
    private static string Describe(Exception ex)
    {
        var frame = ex.StackTrace?
            .Split('\n')
            .FirstOrDefault(l => l.Contains("MpcHcVideoEditor", StringComparison.Ordinal))?
            .Trim();

        var name = ex.GetType().Name;
        return frame is null ? $"{name}: {ex.Message}" : $"{name}: {ex.Message} — {frame}";
    }

    private static void AppendToFile(string where, string what)
    {
        try
        {
            lock (FileLock)
            {
                _logPath ??= Path.Combine(PortablePaths.AppFolder, "diagnostics.log");

                // Capped by starting again rather than by rotating. This is a
                // breadcrumb trail, not an archive, and the recent end is the
                // only part anybody reads.
                if (File.Exists(_logPath) && new FileInfo(_logPath).Length > 256 * 1024)
                    File.Delete(_logPath);

                File.AppendAllText(_logPath,
                    $"{DateTime.Now:yyyy-MM-dd HH:mm:ss}  {where}  {what}{Environment.NewLine}");
            }
        }
        catch
        {
            // A diagnostics log that cannot be written is not worth a second
            // failure. The in-memory ring above still has it.
        }
    }
}
