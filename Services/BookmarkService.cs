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

    /// <summary>Quotes a field only when it would otherwise split or mislead.</summary>
    private static string WriteField(string? value)
    {
        var text = value ?? string.Empty;
        if (!text.Contains(',') && !text.Contains('"')) return text;
        return '"' + text.Replace("\"", "\"\"") + '"';
    }

    public void SaveToCsv(string csvPath, IEnumerable<Bookmark> bookmarks)
    {
        var sb = new StringBuilder();
        foreach (var b in bookmarks.OrderBy(x => x.StartSeconds))
        {
            if (b.IsIncomplete)
            {
                sb.AppendLine($"{(int)b.StartSeconds},");
                continue;
            }

            // The name, then everything the clip carries beyond its range.
            // An unnamed clip writes an empty field rather than the old
            // "BookmarkN" filler, which said nothing the row number did not.
            sb.Append($"{(int)b.StartSeconds},{(int)b.EndSeconds},{WriteField(b.Label)},");
            sb.Append(b.Speed.ToString("0.###", CultureInfo.InvariantCulture));
            sb.Append(b.IsFlipped ? ",1," : ",0,");
            sb.Append(b.Rotation);
            sb.AppendLine(b.IsMuted ? ",1" : ",0");
        }

        var dir = Path.GetDirectoryName(csvPath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        File.WriteAllText(csvPath, sb.ToString(), Encoding.UTF8);
    }

    public string GetCsvPathForVideo(string videoPath)
    {
        // Original convention: same folder + same name + .csv
        // or a dedicated bookmarks folder. We keep it simple and next to the video.
        return Path.ChangeExtension(videoPath, ".csv");
    }
}
