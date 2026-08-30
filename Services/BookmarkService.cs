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
    /// </summary>
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

            // Full bookmark: start,end,BookmarkN
            var full = Regex.Match(line, @"^(\d+(?:\.\d+)?),(\d+(?:\.\d+)?),Bookmark\d+$", RegexOptions.IgnoreCase);
            if (full.Success)
            {
                var start = double.Parse(full.Groups[1].Value, CultureInfo.InvariantCulture);
                var end = double.Parse(full.Groups[2].Value, CultureInfo.InvariantCulture);

                // Auto-correct reversed times
                if (end < start) (start, end) = (end, start);

                // No IsIncomplete to set: the times decide it. A three-field
                // row whose end is zero — which older builds could write —
                // therefore loads as the open bookmark it always was, rather
                // than as a "complete" one with no range.
                result.Add(new Bookmark
                {
                    Index = index++,
                    StartSeconds = start,
                    EndSeconds = end
                });
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

    public void SaveToCsv(string csvPath, IEnumerable<Bookmark> bookmarks)
    {
        var sb = new StringBuilder();
        int i = 1;
        foreach (var b in bookmarks.OrderBy(x => x.StartSeconds))
        {
            if (b.IsIncomplete)
                sb.AppendLine($"{(int)b.StartSeconds},");
            else
                sb.AppendLine($"{(int)b.StartSeconds},{(int)b.EndSeconds},Bookmark{i}");
            i++;
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
