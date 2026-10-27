using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using MpcHcVideoEditor.Models;

namespace MpcHcVideoEditor.Services;

/// <summary>
/// Reads and writes cut lists in the formats other tools speak.
/// </summary>
/// <remarks>
/// <para>
/// All three of these carry timings and names and nothing else. A cut in this
/// program also knows its speed, its rotation, whether it is flipped, muted or
/// cropped, and how it fades — none of which has anywhere to go in a CSV of
/// start and end times, a list of chapter headings, or an edit decision list.
/// Exporting is therefore lossy by nature, and importing produces plain cuts
/// with every setting at its default. The program's own CSV remains the only
/// format that round-trips a cut whole.
/// </para>
/// <para>
/// Pure text in, pure text out, so the parsing can be tested without files.
/// </para>
/// </remarks>
public static class CutListFormats
{
    /// <summary>A cut as these formats are able to describe one.</summary>
    public readonly record struct Entry(double Start, double End, string? Label);

    /// <summary>The formats this can read and write.</summary>
    public enum Kind
    {
        /// <summary>start,end,label in seconds — what LosslessCut reads.</summary>
        Csv,

        /// <summary>"0:00 Title" lines, for a video description.</summary>
        YouTube,

        /// <summary>CMX3600 edit decision list, cuts only.</summary>
        Edl
    }

    // ------------------------------------------------------------------
    // Writing
    // ------------------------------------------------------------------

    /// <summary>
    /// Seconds and a label per line, which is the shape LosslessCut and most
    /// scripts expect.
    /// </summary>
    /// <remarks>
    /// No header row. LosslessCut writes none and treats a first line it cannot
    /// parse as an error rather than as column names, so adding one for
    /// readability would make the file unreadable by the tool it exists for.
    /// </remarks>
    public static string WriteCsv(IEnumerable<Bookmark> cuts)
    {
        var sb = new StringBuilder();

        foreach (var b in Usable(cuts))
            sb.AppendLine($"{Num(b.StartSeconds)},{Num(b.EndSeconds)},{CsvField(b.Label)}");

        return sb.ToString();
    }

    /// <summary>
    /// Chapter headings for a video description.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Not a file format — a convention. YouTube reads lines beginning with a
    /// timestamp out of the description and turns them into chapters, and it
    /// only does so when three conditions hold: the first one starts at 0:00,
    /// there are at least three of them, and none is shorter than ten seconds.
    /// <see cref="YouTubeProblems"/> says which of those a list fails.
    /// </para>
    /// <para>
    /// Chapters tile a video rather than picking pieces out of it, so each one
    /// runs to the next. The gaps between cuts are left inside whichever
    /// chapter precedes them — there is no way to say "nothing here" in this
    /// format, and inventing an "untitled" chapter per gap would turn a
    /// six-cut list into eleven headings.
    /// </para>
    /// </remarks>
    public static string WriteYouTube(IEnumerable<Bookmark> cuts)
    {
        var list = Usable(cuts);
        var sb = new StringBuilder();

        for (int i = 0; i < list.Count; i++)
        {
            // The first heading has to read 0:00 whatever the first cut says,
            // or YouTube ignores the lot.
            var at = i == 0 ? 0 : list[i].StartSeconds;
            sb.AppendLine($"{YouTubeTime(at)} {Heading(list[i], i + 1)}");
        }

        return sb.ToString();
    }

    /// <summary>
    /// Why YouTube would ignore these chapters, or empty when it would not.
    /// </summary>
    public static List<string> YouTubeProblems(IEnumerable<Bookmark> cuts)
    {
        var list = Usable(cuts);
        var problems = new List<string>();

        if (list.Count < 3)
            problems.Add($"YouTube needs at least three chapters; this is {list.Count}.");

        for (int i = 0; i < list.Count; i++)
        {
            var start = i == 0 ? 0 : list[i].StartSeconds;
            var end = i + 1 < list.Count ? list[i + 1].StartSeconds : list[i].EndSeconds;

            if (end - start < 10)
            {
                problems.Add($"Chapter {i + 1} is shorter than the ten seconds YouTube requires.");
                break;
            }
        }

        return problems;
    }

    /// <summary>
    /// A CMX3600 edit decision list — cuts only, one video track.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Enough for an editor to receive a cut list and nothing more. The format
    /// can also describe dissolves, wipes, speed records, audio channel maps
    /// and multiple reels; none of that is expressible here, and a cut list is
    /// what this program has to give.
    /// </para>
    /// <para>
    /// Non-drop-frame throughout, which is what the FCM line declares. Drop
    /// frame is a counting trick for 29.97 and 59.94 that skips frame numbers
    /// to keep clock time honest over an hour; getting it subtly wrong is
    /// worse than not offering it, and an editor reading this will conform
    /// against the source file anyway.
    /// </para>
    /// <para>
    /// Source timecode is where the cut sits in the original. Record timecode
    /// is where it lands in the assembly, so it accumulates — the second cut
    /// starts at the first one's length, not at its own source time.
    /// </para>
    /// </remarks>
    public static string WriteEdl(IEnumerable<Bookmark> cuts, double fps, string title)
    {
        var list = Usable(cuts);
        var rate = fps > 0 ? fps : 25;

        var sb = new StringBuilder();
        sb.AppendLine($"TITLE: {EdlTitle(title)}");
        sb.AppendLine("FCM: NON-DROP FRAME");
        sb.AppendLine();

        var record = 0.0;
        var n = 1;

        foreach (var b in list)
        {
            var length = b.EndSeconds - b.StartSeconds;

            // Reel names are eight characters in the strict grammar; AX is the
            // conventional stand-in for "the source, whatever it is called".
            sb.AppendLine(
                $"{n:D3}  AX       V     C        " +
                $"{Timecode(b.StartSeconds, rate)} {Timecode(b.EndSeconds, rate)} " +
                $"{Timecode(record, rate)} {Timecode(record + length, rate)}");

            if (!string.IsNullOrWhiteSpace(b.Label))
                sb.AppendLine($"* FROM CLIP NAME: {b.Label.Trim()}");

            record += length;
            n++;
        }

        return sb.ToString();
    }

    // ------------------------------------------------------------------
    // Reading
    // ------------------------------------------------------------------

    /// <summary>
    /// Which format some text is, judged by what it looks like.
    /// </summary>
    /// <remarks>
    /// By content rather than by extension. A cut list arrives as whatever the
    /// tool that wrote it chose to call the file, and ".txt" is the commonest
    /// answer for two of these three.
    /// </remarks>
    public static Kind Detect(string text)
    {
        if (Regex.IsMatch(text, @"^\s*(TITLE:|FCM:|\d{3}\s)", RegexOptions.Multiline))
            return Kind.Edl;

        // A timestamp leading the line, with words after it rather than another
        // number, is a chapter heading.
        if (Regex.IsMatch(text, @"^\s*\d{1,2}(:\d{2}){1,2}\s+\S", RegexOptions.Multiline))
            return Kind.YouTube;

        return Kind.Csv;
    }

    /// <summary>Reads whichever of the three <paramref name="text"/> is.</summary>
    /// <param name="duration">
    /// Needed only by chapter lists, whose last entry has no end of its own.
    /// </param>
    public static List<Entry> Read(string text, double fps, double duration) =>
        Detect(text) switch
        {
            Kind.Edl => ReadEdl(text, fps),
            Kind.YouTube => ReadYouTube(text, duration),
            _ => ReadCsv(text)
        };

    /// <summary>
    /// start,end,label per line.
    /// </summary>
    /// <remarks>
    /// Lines that do not parse are skipped rather than refused: a file written
    /// by hand or by some other tool may carry a header row, a comment or a
    /// blank, and throwing the whole list away over one of them helps nobody.
    /// </remarks>
    public static List<Entry> ReadCsv(string text)
    {
        var entries = new List<Entry>();

        foreach (var line in Lines(text))
        {
            var parts = SplitCsv(line);
            if (parts.Count < 2) continue;

            if (!TryTime(parts[0], out var start)) continue;
            if (!TryTime(parts[1], out var end)) continue;
            if (end <= start) continue;

            entries.Add(new Entry(start, end, parts.Count > 2 ? Trimmed(parts[2]) : null));
        }

        return entries;
    }

    /// <summary>
    /// "0:00 Title" lines, each running to the next.
    /// </summary>
    /// <remarks>
    /// A description holds prose as well as chapters, so anything that does not
    /// begin with a timestamp is passed over rather than treated as a failure.
    /// The last chapter runs to the end of the video, which is the one thing
    /// this format cannot say for itself.
    /// </remarks>
    public static List<Entry> ReadYouTube(string text, double duration)
    {
        var starts = new List<(double At, string? Label)>();

        foreach (var line in Lines(text))
        {
            var m = Regex.Match(line.Trim(), @"^(\d{1,2}(?::\d{2}){1,2})\s*[-–—]?\s*(.*)$");
            if (!m.Success) continue;
            if (!TryTime(m.Groups[1].Value, out var at)) continue;

            starts.Add((at, Trimmed(m.Groups[2].Value)));
        }

        starts.Sort((a, b) => a.At.CompareTo(b.At));

        var entries = new List<Entry>();
        for (int i = 0; i < starts.Count; i++)
        {
            var end = i + 1 < starts.Count ? starts[i + 1].At : duration;
            if (end <= starts[i].At) continue;

            entries.Add(new Entry(starts[i].At, end, starts[i].Label));
        }

        return entries;
    }

    /// <summary>
    /// The source in and out of each cut record, with the rest ignored.
    /// </summary>
    /// <remarks>
    /// Only plain cuts are read. A dissolve or a wipe describes two sources
    /// overlapping, which is not a thing this program's cut list can hold, and
    /// guessing which half was meant would be worse than passing it by.
    /// </remarks>
    public static List<Entry> ReadEdl(string text, double fps)
    {
        var rate = fps > 0 ? fps : 25;
        var entries = new List<Entry>();

        // edit number, reel, track, transition, then four timecodes.
        var record = new Regex(
            @"^\s*(\d{1,4})\s+(\S+)\s+(\S+)\s+(C)\s+" +
            @"(\d{2}[:;]\d{2}[:;]\d{2}[:;]\d{2})\s+(\d{2}[:;]\d{2}[:;]\d{2}[:;]\d{2})",
            RegexOptions.IgnoreCase);

        foreach (var line in Lines(text))
        {
            // A clip name belongs to the record above it.
            var name = Regex.Match(line, @"^\s*\*\s*FROM CLIP NAME:\s*(.+)$", RegexOptions.IgnoreCase);
            if (name.Success && entries.Count > 0)
            {
                var last = entries[^1];
                entries[^1] = last with { Label = Trimmed(name.Groups[1].Value) };
                continue;
            }

            var m = record.Match(line);
            if (!m.Success) continue;

            // Audio-only records describe the same edit as the video one beside
            // them; taking both would double every cut.
            var track = m.Groups[3].Value.ToUpperInvariant();
            if (!track.StartsWith("V") && track != "B") continue;

            var start = FromTimecode(m.Groups[5].Value, rate);
            var end = FromTimecode(m.Groups[6].Value, rate);
            if (end <= start) continue;

            entries.Add(new Entry(start, end, null));
        }

        return entries;
    }

    // ------------------------------------------------------------------
    // Time
    // ------------------------------------------------------------------

    /// <summary>HH:MM:SS:FF at the given rate.</summary>
    public static string Timecode(double seconds, double fps)
    {
        var frames = (long)Math.Round(seconds * fps);
        var perHour = (long)Math.Round(fps * 3600);
        var perMinute = (long)Math.Round(fps * 60);
        var perSecond = (long)Math.Round(fps);

        var h = frames / perHour; frames %= perHour;
        var m = frames / perMinute; frames %= perMinute;
        var s = frames / perSecond; frames %= perSecond;

        return $"{h:D2}:{m:D2}:{s:D2}:{frames:D2}";
    }

    private static double FromTimecode(string tc, double fps)
    {
        var parts = tc.Split(':', ';');
        if (parts.Length != 4) return 0;

        double N(string p) => double.TryParse(p, NumberStyles.Integer,
            CultureInfo.InvariantCulture, out var v) ? v : 0;

        return N(parts[0]) * 3600 + N(parts[1]) * 60 + N(parts[2]) + N(parts[3]) / fps;
    }

    /// <summary>
    /// 0:00, 1:23 or 1:02:03 — the hour only when there is one.
    /// </summary>
    private static string YouTubeTime(double seconds)
    {
        var t = TimeSpan.FromSeconds(Math.Max(0, Math.Floor(seconds)));
        return t.TotalHours >= 1
            ? $"{(int)t.TotalHours}:{t.Minutes:D2}:{t.Seconds:D2}"
            : $"{t.Minutes}:{t.Seconds:D2}";
    }

    /// <summary>
    /// Seconds, or a clock reading. Both turn up in files written by hand.
    /// </summary>
    private static bool TryTime(string text, out double seconds)
    {
        seconds = 0;
        var t = text.Trim();
        if (t.Length == 0) return false;

        if (t.Contains(':'))
        {
            var parts = t.Split(':');
            if (parts.Length is < 2 or > 3) return false;

            double total = 0;
            foreach (var p in parts)
            {
                if (!double.TryParse(p, NumberStyles.Float, CultureInfo.InvariantCulture, out var v))
                    return false;
                total = total * 60 + v;
            }

            seconds = total;
            return true;
        }

        return double.TryParse(t, NumberStyles.Float, CultureInfo.InvariantCulture, out seconds);
    }

    // ------------------------------------------------------------------
    // Odds and ends
    // ------------------------------------------------------------------

    private static List<Bookmark> Usable(IEnumerable<Bookmark> cuts) =>
        cuts.Where(b => b.IsValid).OrderBy(b => b.StartSeconds).ToList();

    private static IEnumerable<string> Lines(string text) =>
        text.Split('\n').Select(l => l.TrimEnd('\r')).Where(l => l.Trim().Length > 0);

    private static string? Trimmed(string? s) =>
        string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    private static string Heading(Bookmark b, int number) =>
        string.IsNullOrWhiteSpace(b.Label) ? $"Chapter {number}" : b.Label.Trim();

    private static string Num(double seconds) =>
        seconds.ToString("0.###", CultureInfo.InvariantCulture);

    /// <summary>A title line cannot wrap, so it cannot hold a newline.</summary>
    private static string EdlTitle(string title) =>
        title.Replace('\r', ' ').Replace('\n', ' ').Trim();

    /// <summary>Quotes only when the value would otherwise split or confuse.</summary>
    private static string CsvField(string? value)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;

        return value.Contains(',') || value.Contains('"') || value.Contains('\n')
            ? '"' + value.Replace("\"", "\"\"") + '"'
            : value;
    }

    /// <summary>Splits one CSV line, honouring quotes around a field.</summary>
    private static List<string> SplitCsv(string line)
    {
        var fields = new List<string>();
        var sb = new StringBuilder();
        var quoted = false;

        for (int i = 0; i < line.Length; i++)
        {
            var c = line[i];

            if (quoted)
            {
                if (c != '"') { sb.Append(c); continue; }

                // A doubled quote inside quotes is one literal quote.
                if (i + 1 < line.Length && line[i + 1] == '"') { sb.Append('"'); i++; }
                else quoted = false;
            }
            else if (c == '"') quoted = true;
            else if (c == ',') { fields.Add(sb.ToString()); sb.Clear(); }
            else sb.Append(c);
        }

        fields.Add(sb.ToString());
        return fields;
    }
}
