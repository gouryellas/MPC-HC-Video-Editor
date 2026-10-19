using System.IO;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using MpcHcVideoEditor.Helpers;
using MpcHcVideoEditor.Models;

namespace MpcHcVideoEditor.Services;

public class BookmarkService
{
    /// <summary>
    /// Original format examples:
    /// 123,456,Bookmark1
    /// 789,
    ///
    /// Current format adds the per-clip settings after the name:
    /// 123,456,Chapter 1,1,0,None,0
    /// 123,456,"A name, with a comma",0.5,1,Clockwise,1
    /// </summary>
    /// <remarks>
    /// The third field was always positional filler — <c>BookmarkN</c>, where N
    /// is the row number, discarded on load. It now carries the clip's name,
    /// and a third field still matching the old placeholder is read as no name
    /// rather than as a clip literally called "Bookmark3".
    ///
    /// Fields beyond it are optional, so a file written by any earlier build
    /// loads unchanged and simply takes the defaults. Speed, flip, rotation and
    /// mute were previously held only in memory: they survived until the list
    /// was reloaded, which the main window does every time it is activated.
    /// </remarks>
    public List<Bookmark> LoadFromCsv(string csvPath)
    {
        var result = new List<Bookmark>();
        if (!File.Exists(csvPath)) return result;

        // Detected, not assumed. A CSV written in the legacy Windows code page
        // — which the AutoHotkey predecessor this app replaces would have
        // produced — turns every accented character into U+FFFD when read as
        // UTF-8, and nothing downstream can recover it.
        var lines = TextFile.ReadAllLines(csvPath);
        int index = 1;

        foreach (var raw in lines)
        {
            var line = raw.Trim();
            if (string.IsNullOrEmpty(line)) continue;

            // Full bookmark: two times, then optional name and settings.
            var fields = SplitCsv(line);
            if (fields.Count >= 3 &&
                double.TryParse(fields[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var start) &&
                double.TryParse(fields[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var end))
            {
                // Auto-correct reversed times
                if (end < start) (start, end) = (end, start);

                // No IsIncomplete to set: the times decide it. A three-field
                // row whose end is zero — which older builds could write —
                // therefore loads as the open bookmark it always was, rather
                // than as a "complete" one with no range.
                var bookmark = new Bookmark
                {
                    Index = index++,
                    StartSeconds = start,
                    EndSeconds = end,
                    Label = LegacyPlaceholder.IsMatch(fields[2]) ? null : fields[2]
                };

                // Each of these is independently optional: a file from any
                // build in between carries as many as it knew about.
                if (fields.Count > 3 &&
                    double.TryParse(fields[3], NumberStyles.Float, CultureInfo.InvariantCulture, out var speed))
                    bookmark.Speed = speed;

                if (fields.Count > 4)
                    bookmark.IsFlipped = fields[4] == "1";

                if (fields.Count > 5 && Enum.TryParse<Rotation>(fields[5], ignoreCase: true, out var rotation))
                    bookmark.Rotation = rotation;

                if (fields.Count > 6)
                    bookmark.IsMuted = fields[6] == "1";

                if (fields.Count > 7 &&
                    double.TryParse(fields[7], NumberStyles.Float, CultureInfo.InvariantCulture, out var fadeIn))
                    bookmark.FadeInSeconds = fadeIn;

                if (fields.Count > 8 &&
                    double.TryParse(fields[8], NumberStyles.Float, CultureInfo.InvariantCulture, out var fadeOut))
                    bookmark.FadeOutSeconds = fadeOut;

                // The crop rectangle, as four fractions of the frame. All four
                // or none: three of them describe no rectangle, and a file
                // truncated mid-row should load as uncropped rather than as
                // some rectangle nobody drew.
                if (fields.Count > 12 &&
                    double.TryParse(fields[9], NumberStyles.Float, CultureInfo.InvariantCulture, out var cropX) &&
                    double.TryParse(fields[10], NumberStyles.Float, CultureInfo.InvariantCulture, out var cropY) &&
                    double.TryParse(fields[11], NumberStyles.Float, CultureInfo.InvariantCulture, out var cropW) &&
                    double.TryParse(fields[12], NumberStyles.Float, CultureInfo.InvariantCulture, out var cropH))
                    bookmark.SetCrop(cropX, cropY, cropW, cropH);

                result.Add(bookmark);
                continue;
            }

            // Incomplete: just a start time
            var incomplete = Regex.Match(line, @"^(\d+(?:\.\d+)?),?$");
            if (incomplete.Success)
            {
                result.Add(new Bookmark
                {
                    Index = index++,
                    StartSeconds = double.Parse(incomplete.Groups[1].Value, CultureInfo.InvariantCulture),
                    EndSeconds = 0
                });
            }
        }

        return result;
    }

    /// <summary>The filler the third field used to hold: "Bookmark" and a row number.</summary>
    private static readonly Regex LegacyPlaceholder = new(@"^Bookmark\d*$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>
    /// Splits one line, honouring double quotes so a name may contain commas.
    /// </summary>
    /// <remarks>
    /// Deliberately small rather than a CSV library: this reads one flat line
    /// with no embedded newlines, and the only escape that has to work is the
    /// one <see cref="WriteField"/> produces.
    /// </remarks>
    private static List<string> SplitCsv(string line)
    {
        var fields = new List<string>();
        var current = new StringBuilder();
        var quoted = false;

        for (int i = 0; i < line.Length; i++)
        {
            var c = line[i];

            if (quoted)
            {
                // A doubled quote inside a quoted field is one literal quote.
                if (c == '"' && i + 1 < line.Length && line[i + 1] == '"') { current.Append('"'); i++; }
                else if (c == '"') quoted = false;
                else current.Append(c);
            }
            else if (c == '"') quoted = true;
            else if (c == ',') { fields.Add(current.ToString().Trim()); current.Clear(); }
            else current.Append(c);
        }

        fields.Add(current.ToString().Trim());
        return fields;
    }

    /// <summary>
    /// A bookmark time as a whole number of seconds, rounded to the nearest.
    /// </summary>
    /// <remarks>
    /// Was a cast to <c>int</c>, which truncates: a hand-edited 5.7 was written
    /// back as 5 while the row displayed it as 6. The file and the list now agree,
    /// because both round.
    ///
    /// Whole seconds is the format — the original AutoHotkey one — and every time
    /// the program itself produces is already whole, so this only ever changes a
    /// value that arrived from outside.
    /// </remarks>
    private static string WriteTime(double seconds)
    {
        if (seconds < 0) seconds = 0;
        return ((long)Math.Round(seconds, MidpointRounding.AwayFromZero))
            .ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>A fraction of the frame, at the precision the crop is kept to.</summary>
    private static string Fraction(double value) =>
        value.ToString("0.#####", CultureInfo.InvariantCulture);

    /// <summary>Quotes a field only when it would otherwise split or mislead.</summary>
    private static string WriteField(string? value)
    {
        var text = value ?? string.Empty;
        if (!text.Contains(',') && !text.Contains('"')) return text;
        return '"' + text.Replace("\"", "\"\"") + '"';
    }

    /// <summary>
    /// Writes a bookmark file, and mirrors it to %APPDATA% when it lives in
    /// the program's own bookmarks folder.
    /// </summary>
    /// <param name="videoPath">
    /// The video these bookmarks belong to, recorded in the file's header. Null
    /// keeps whatever header the file already had, so a save that does not know
    /// the video — a hand-edited file being written back — does not strip it.
    /// </param>
    public void SaveToCsv(string csvPath, IEnumerable<Bookmark> bookmarks, string? videoPath = null)
    {
        var sb = new StringBuilder();

        // The header goes first, because that is where GetVideoPathFor stops
        // looking for it.
        var owner = string.IsNullOrWhiteSpace(videoPath) ? GetVideoPathFor(csvPath) : videoPath;
        if (!string.IsNullOrWhiteSpace(owner))
            sb.AppendLine(VideoHeaderPrefix + owner);

        foreach (var b in bookmarks.OrderBy(x => x.StartSeconds))
        {
            if (b.IsIncomplete)
            {
                sb.AppendLine($"{WriteTime(b.StartSeconds)},");
                continue;
            }

            // The name, then everything the clip carries beyond its range.
            // An unnamed clip writes an empty field rather than the old
            // "BookmarkN" filler, which said nothing the row number did not.
            sb.Append($"{WriteTime(b.StartSeconds)},{WriteTime(b.EndSeconds)},{WriteField(b.Label)},");
            sb.Append(b.Speed.ToString("0.###", CultureInfo.InvariantCulture));
            sb.Append(b.IsFlipped ? ",1," : ",0,");
            sb.Append(b.Rotation);
            sb.Append(b.IsMuted ? ",1," : ",0,");
            sb.Append(b.FadeInSeconds.ToString("0.###", CultureInfo.InvariantCulture));
            sb.Append(',');
            sb.Append(b.FadeOutSeconds.ToString("0.###", CultureInfo.InvariantCulture));

            // The crop, always written — an uncropped clip writes 0,0,1,1
            // rather than nothing, so the row has a fixed number of fields and
            // a later one can be added the same way these were.
            sb.Append(',');
            sb.Append(Fraction(b.CropX));
            sb.Append(',');
            sb.Append(Fraction(b.CropY));
            sb.Append(',');
            sb.Append(Fraction(b.CropWidth));
            sb.Append(',');
            sb.AppendLine(Fraction(b.CropHeight));
        }

        var dir = Path.GetDirectoryName(csvPath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        File.WriteAllText(csvPath, sb.ToString(), Encoding.UTF8);
        MirrorToBackup(csvPath);
    }

    // ------------------------------------------------------------------
    // Which file belongs to which video
    // ------------------------------------------------------------------

    /// <summary>
    /// Marks the line naming the video a bookmark file belongs to.
    /// </summary>
    /// <remarks>
    /// A '#' line is not a bookmark row in any format this program has ever
    /// written, and <see cref="LoadFromCsv"/> skips anything that parses as
    /// neither a full row nor a lone start time — so a file carrying this
    /// header still loads correctly in every earlier build. That is deliberate:
    /// a CSV written today can be opened by 5.6 and before.
    /// </remarks>
    public const string VideoHeaderPrefix = "#video=";

    /// <summary>
    /// The video a bookmark file belongs to, or null for a file that does not
    /// say — one written before the header existed, or dragged in from the old
    /// next-to-the-video layout.
    /// </summary>
    public string? GetVideoPathFor(string csvPath)
    {
        try
        {
            if (!File.Exists(csvPath)) return null;

            foreach (var raw in TextFile.ReadAllLines(csvPath))
            {
                var line = raw.Trim();
                if (line.Length == 0) continue;

                // Present or absent, the header is the first thing in the file.
                // Stopping at the first row keeps this to one decision rather
                // than a scan of every line.
                return line.StartsWith(VideoHeaderPrefix, StringComparison.OrdinalIgnoreCase)
                    ? line[VideoHeaderPrefix.Length..].Trim()
                    : null;
            }
        }
        catch
        {
            // An unreadable file has no owner to report. The caller falls back
            // to matching on filename.
        }

        return null;
    }

    /// <summary>
    /// The bookmark file for a video: the one already claiming it, or the path
    /// a new one should be written to.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Bookmarks used to live next to their video, which made this
    /// <c>ChangeExtension(videoPath, ".csv")</c> and nothing more. One shared
    /// folder cannot work that way: two videos called the same thing in
    /// different folders would both want <c>Bookmarks\intro.csv</c>, and the
    /// second would silently overwrite the first — or worse, load the first
    /// one's cuts onto it and be merged that way.
    /// </para>
    /// <para>
    /// So the identity is the header, not the name. A file's name is a label
    /// for the user's benefit and can be changed by hand without breaking
    /// anything; what ties it to a video is the path recorded inside it.
    /// </para>
    /// <para>
    /// Deliberately not cached. This runs once per video opened, against a
    /// folder of small files, next to an ffprobe call that costs considerably
    /// more — and a cache here would need invalidating on every save, delete
    /// and mirror restore, which is three more chances to serve a stale answer
    /// than the scan costs to avoid.
    /// </para>
    /// </remarks>
    public string GetCsvPathForVideo(string videoPath)
    {
        if (string.IsNullOrWhiteSpace(videoPath)) return string.Empty;

        var folder = PortablePaths.BookmarksFolder;
        var stem = Path.GetFileNameWithoutExtension(videoPath);

        string[] existing;
        try { existing = Directory.GetFiles(folder, "*.csv"); }
        catch { return Path.Combine(folder, stem + ".csv"); }

        // A file that names this video outright wins, whatever it is called.
        string? headerless = null;
        foreach (var candidate in existing)
        {
            var owner = GetVideoPathFor(candidate);

            if (owner is null)
            {
                // Says nothing about its video. Worth adopting if the name
                // matches, but only once nothing better has turned up.
                if (headerless is null && string.Equals(
                        Path.GetFileNameWithoutExtension(candidate), stem,
                        StringComparison.OrdinalIgnoreCase))
                    headerless = candidate;

                continue;
            }

            if (string.Equals(owner, videoPath, StringComparison.OrdinalIgnoreCase))
                return candidate;
        }

        // Adopt the matching headerless file. Saving it writes the header, so a
        // file dropped into the folder by hand joins the scheme by being used.
        if (headerless is not null) return headerless;

        // Nothing for this video yet: its own name, unless another video got
        // there first.
        var path = Path.Combine(folder, stem + ".csv");
        if (!File.Exists(path)) return path;

        for (int n = 2; n < 1000; n++)
        {
            path = Path.Combine(folder, $"{stem} ({n}).csv");
            if (!File.Exists(path)) return path;
        }

        // A thousand videos of the same name is not a real case, but silently
        // returning a path that is already taken would be a data loss.
        return Path.Combine(folder, $"{stem} ({Guid.NewGuid():N}).csv");
    }

    // ------------------------------------------------------------------
    // The %APPDATA% mirror
    // ------------------------------------------------------------------

    /// <summary>
    /// True for a path in the program's own bookmarks folder.
    /// </summary>
    /// <remarks>
    /// The mirror only tracks that folder. File ▸ Open accepts a CSV from
    /// anywhere, and saving one of those back must not drop a copy in %APPDATA%
    /// — it is not part of the collection, and mirroring it would make the
    /// restore reinstate a file the user never kept here.
    /// </remarks>
    private static bool IsInBookmarksFolder(string path)
    {
        try
        {
            var folder = Path.GetFullPath(PortablePaths.BookmarksFolder).TrimEnd('\\');
            var dir = Path.GetDirectoryName(Path.GetFullPath(path))?.TrimEnd('\\');
            return dir is not null && string.Equals(dir, folder, StringComparison.OrdinalIgnoreCase);
        }
        catch { return false; }
    }

    /// <summary>Copies one bookmark file to the %APPDATA% mirror.</summary>
    /// <remarks>
    /// Best effort, and silent when it fails: the file the user asked for is
    /// already written, and a mirror that cannot be updated must not turn a
    /// successful save into a reported failure. Nothing is lost by the silence
    /// — <see cref="SyncWithBackup"/> catches up at the next launch, which is
    /// the difference between this and the settings backup it is modelled on.
    /// </remarks>
    private static void MirrorToBackup(string csvPath)
    {
        if (!IsInBookmarksFolder(csvPath)) return;

        try
        {
            var backup = PortablePaths.BackupBookmarksFolder;
            Directory.CreateDirectory(backup);
            File.Copy(csvPath, Path.Combine(backup, Path.GetFileName(csvPath)), overwrite: true);
        }
        catch { /* see above */ }

        // Outside the try: the index describes the bookmarks folder, which was
        // written whether or not the mirror copy succeeded.
        RecordCurrentContents();
    }

    /// <summary>Removes one bookmark file from the %APPDATA% mirror.</summary>
    /// <remarks>
    /// Unlinked rather than sent to the Recycle Bin, which is otherwise this
    /// application's rule for anything it deletes. The copy in the program
    /// folder has just gone to the bin and is recoverable from there; sending
    /// its mirror too would put two entries in the bin for one deletion and
    /// make restoring either of them ambiguous.
    /// </remarks>
    public static void MirrorDelete(string csvPath)
    {
        if (!IsInBookmarksFolder(csvPath)) return;

        try
        {
            var backup = Path.Combine(PortablePaths.BackupBookmarksFolder, Path.GetFileName(csvPath));
            if (File.Exists(backup)) File.Delete(backup);
        }
        catch { /* the program folder is the one that had to change */ }

        RecordCurrentContents();
    }

    // ------------------------------------------------------------------
    // What this install last held
    // ------------------------------------------------------------------

    /// <summary>
    /// A list of the bookmark filenames this install has, kept beside the
    /// executable.
    /// </summary>
    /// <remarks>
    /// <para>
    /// It exists to tell one empty folder from another. A bookmarks folder with
    /// nothing in it is either a folder replaced during an upgrade — where the
    /// mirror is the only copy left and must be restored — or a folder the user
    /// emptied on purpose, where restoring would undo what they just did. The
    /// folder itself cannot say which; this can.
    /// </para>
    /// <para>
    /// Deliberately <em>not</em> mirrored to %APPDATA%, and that is the whole
    /// mechanism: replacing the program folder takes this file with it, so the
    /// new folder correctly reads as "never had any" and restores. Deleting a
    /// CSV by hand leaves it behind, so that correctly reads as "had it, lost
    /// it" and the mirror copy goes too.
    /// </para>
    /// </remarks>
    private static string ManifestPath => Path.Combine(PortablePaths.AppFolder, "bookmarks.index");

    private static HashSet<string> ReadManifest()
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        try
        {
            if (File.Exists(ManifestPath))
                foreach (var line in File.ReadAllLines(ManifestPath))
                {
                    var name = line.Trim();
                    if (name.Length > 0) names.Add(name);
                }
        }
        catch
        {
            // Unreadable reads as empty, which is the safe direction: nothing
            // gets pruned from the mirror on a guess.
        }

        return names;
    }

    /// <summary>Records the bookmarks folder's current contents.</summary>
    /// <remarks>
    /// Rewritten on every save and delete rather than at exit, so a crash or a
    /// kill cannot leave the index describing a folder that has moved on. It is
    /// a few dozen short lines; the write is lost in the noise of the CSV write
    /// that prompted it.
    /// </remarks>
    private static void RecordCurrentContents()
    {
        try
        {
            var names = Directory.GetFiles(PortablePaths.BookmarksFolder, "*.csv")
                .Select(Path.GetFileName)
                .OfType<string>()
                .OrderBy(n => n, StringComparer.OrdinalIgnoreCase);

            File.WriteAllLines(ManifestPath, names, Encoding.UTF8);
        }
        catch
        {
            // A stale index costs one resurrected bookmark in an uncommon case;
            // failing a save over it would cost the bookmark itself.
        }
    }

    /// <summary>
    /// Brings the bookmarks folder and its %APPDATA% mirror into step. Called
    /// once at startup.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Three steps, and the order is the whole design.
    /// </para>
    /// <para>
    /// <em>Prune.</em> Anything <see cref="ManifestPath"/> says this install had
    /// and no longer does was deleted while the program was closed. That is the
    /// same intent as deleting it through the program, so the mirror copy goes
    /// too. This has to run before the restore, or a folder emptied by hand
    /// would be refilled from the mirror on the next launch and the deletion
    /// would not stick.
    /// </para>
    /// <para>
    /// <em>Restore.</em> An empty bookmarks folder with nothing pruned is the
    /// state a replaced program folder is in, and there the mirror is the only
    /// copy left. While both folders are empty this finds nothing to do and
    /// tries again next launch; once a bookmark exists it is a no-op.
    /// </para>
    /// <para>
    /// <em>Push.</em> Everything the mirror is missing or behind on, which
    /// catches up the saves whose mirror write failed silently.
    /// </para>
    /// <para>
    /// The push itself never deletes — only the prune does, and only for names
    /// this install recorded. That is what keeps two installs on one machine
    /// from eating each other's bookmarks: a dev build and a portable build
    /// share one %APPDATA% folder, and a push that removed whatever the
    /// launching copy happened not to have would leave the other's cuts
    /// recoverable from neither place. Each install only ever prunes what it
    /// knew it had.
    /// </para>
    /// </remarks>
    public static void SyncWithBackup()
    {
        try
        {
            var live = PortablePaths.BookmarksFolder;
            var backup = PortablePaths.BackupBookmarksFolder;
            Directory.CreateDirectory(backup);

            var present = Directory.GetFiles(live, "*.csv")
                .Select(Path.GetFileName)
                .OfType<string>()
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            // 1. Deletions made while the program was closed.
            foreach (var name in ReadManifest())
            {
                if (present.Contains(name)) continue;

                try
                {
                    var orphan = Path.Combine(backup, name);
                    if (File.Exists(orphan)) File.Delete(orphan);
                }
                catch { /* one stubborn file must not stop the rest */ }
            }

            // 2. A folder with nothing left in it, and a mirror that still has
            //    something, means the folder was replaced rather than emptied.
            if (present.Count == 0)
            {
                foreach (var file in Directory.GetFiles(backup, "*.csv"))
                {
                    var name = Path.GetFileName(file);
                    File.Copy(file, Path.Combine(live, name), overwrite: true);
                    present.Add(name);
                }
            }

            // 3. Anything the mirror is missing or behind on.
            foreach (var file in Directory.GetFiles(live, "*.csv"))
            {
                var copy = Path.Combine(backup, Path.GetFileName(file));

                // File.Copy carries the write time across, so a file just
                // restored above compares equal and is not copied straight back.
                if (File.Exists(copy) &&
                    File.GetLastWriteTimeUtc(copy) >= File.GetLastWriteTimeUtc(file))
                    continue;

                File.Copy(file, copy, overwrite: true);
            }

            RecordCurrentContents();
        }
        catch
        {
            // Never block startup over a mirror. The bookmarks beside the
            // executable are the ones the program actually reads.
        }
    }
}
