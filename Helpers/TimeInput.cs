using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace MpcHcVideoEditor.Helpers;

/// <summary>
/// Reads a time, or a range of two, written the way somebody would say it.
/// </summary>
/// <remarks>
/// <para>
/// One place where every form is understood, rather than a regex at the point
/// of use. The forms are not alternatives to choose between — the same person
/// types "35" one minute and "1 hour 4 minutes 35 seconds" the next — so the
/// field has to take all of them, and anything that takes all of them is worth
/// writing once and testing on its own.
/// </para>
/// <para>
/// What is accepted:
/// <list type="bullet">
///   <item>Clock: <c>35</c>, <c>12:35</c>, <c>1:04:35</c>, <c>00:00:35</c>.</item>
///   <item>Units, long or short, spaced or not: <c>1s</c>, <c>1min</c>,
///         <c>1hr</c>, <c>1 hour</c>, <c>4min 35s</c>,
///         <c>1 hour 4 minutes 35 seconds</c>.</item>
///   <item>Numbers written out: <c>sixty</c>, <c>one minute</c>,
///         <c>ninety-nine seconds</c>.</item>
///   <item>A range: any two of those with a dash between them, each side free
///         to be written differently from the other.</item>
/// </list>
/// </para>
/// <para>
/// A bare number is seconds. That is the one real ambiguity here — "60" could
/// be a minute — and seconds is both what the rest of the program means by a
/// bare number and what the field is seeded with.
/// </para>
/// </remarks>
public static class TimeInput
{
    /// <summary>A time, and the time it runs to when one was given.</summary>
    /// <param name="Start">Seconds from the beginning of the video.</param>
    /// <param name="End">The closing time, or null when a single time was typed.</param>
    public readonly record struct Parsed(double Start, double? End)
    {
        /// <summary>Whether this is a range rather than a single time.</summary>
        public bool IsRange => End.HasValue;
    }

    /// <summary>
    /// The dashes a range may be written with. The en and em dashes are here
    /// because a word processor turns a typed hyphen into one, and a pasted
    /// range should not be refused for a character the user did not type.
    /// </summary>
    private static readonly char[] Dashes = { '-', '–', '—' };

    /// <summary>
    /// Reads a single time or a range.
    /// </summary>
    /// <param name="error">Why it was refused, in the words the field shows.</param>
    public static bool TryParse(string? input, out Parsed result, out string error)
    {
        result = default;
        error = string.Empty;

        var text = (input ?? string.Empty).Trim();
        if (text.Length == 0)
        {
            error = "Type a time, or two with a dash between them.";
            return false;
        }

        // From index 1, so a leading dash stays part of the value it is on and
        // is refused as a negative time rather than splitting into an empty
        // left-hand side.
        var dash = text.IndexOfAny(Dashes, 1);
        if (dash < 0)
        {
            if (!TryParseOne(text, out var single, out error)) return false;
            result = new Parsed(single, null);
            return true;
        }

        var left = text[..dash].Trim();
        var right = text[(dash + 1)..].Trim();

        if (right.Length == 0)
        {
            error = "The range has no end time. Try 1:04 - 12:35.";
            return false;
        }

        if (!TryParseOne(left, out var start, out error)) return false;
        if (!TryParseOne(right, out var end, out error)) return false;

        result = new Parsed(start, end);
        return true;
    }

    /// <summary>
    /// Reads one time — no dash, no range.
    /// </summary>
    public static bool TryParseOne(string? input, out double seconds, out string error)
    {
        seconds = 0;
        error = string.Empty;

        var text = (input ?? string.Empty).Trim();
        if (text.Length == 0)
        {
            error = "No time was entered.";
            return false;
        }

        // Caught here rather than left to the scanner, which would otherwise
        // report the minus sign as an unknown word. A negative time is a
        // mistake worth naming.
        if (text[0] is '-' or '–' or '—')
        {
            error = "A time cannot be negative.";
            return false;
        }

        return text.Contains(':')
            ? TryParseClock(text, out seconds, out error)
            : TryParseSpoken(text, out seconds, out error);
    }

    /// <summary>
    /// <c>35</c>, <c>12:35</c>, <c>1:04:35</c> — read right to left, so the
    /// last field is always seconds whether or not the ones above it are there.
    /// </summary>
    private static bool TryParseClock(string text, out double seconds, out string error)
    {
        seconds = 0;
        error = string.Empty;

        var parts = text.Split(':');
        if (parts.Length > 3)
        {
            error = $"\"{text}\" has too many colons. The most a time needs is hours:minutes:seconds.";
            return false;
        }

        double total = 0;
        foreach (var raw in parts)
        {
            var part = raw.Trim();
            if (part.Length == 0)
            {
                error = $"\"{text}\" has an empty place between its colons.";
                return false;
            }

            if (!double.TryParse(part, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
            {
                error = $"\"{part}\" is not a number.";
                return false;
            }

            if (value < 0)
            {
                error = "A time cannot be negative.";
                return false;
            }

            total = total * 60 + value;
        }

        seconds = total;
        return true;
    }

    /// <summary>The unit each spelling means, and how to report it.</summary>
    private static readonly Dictionary<string, (double Multiplier, string Name)> Units =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["h"] = (3600, "hours"),
            ["hr"] = (3600, "hours"),
            ["hrs"] = (3600, "hours"),
            ["hour"] = (3600, "hours"),
            ["hours"] = (3600, "hours"),
            ["m"] = (60, "minutes"),
            ["min"] = (60, "minutes"),
            ["mins"] = (60, "minutes"),
            ["minute"] = (60, "minutes"),
            ["minutes"] = (60, "minutes"),
            ["s"] = (1, "seconds"),
            ["sec"] = (1, "seconds"),
            ["secs"] = (1, "seconds"),
            ["second"] = (1, "seconds"),
            ["seconds"] = (1, "seconds")
        };

    /// <summary>
    /// A number and the unit it was given, if any. The unit is optional so a
    /// bare "35" can be read, and so "1h30" can say what is wrong with it.
    /// </summary>
    private static readonly Regex Term = new(
        @"(?<num>\d+(?:\.\d+)?)\s*(?<unit>[a-z]+)?",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>
    /// <c>1s</c>, <c>4min 35s</c>, <c>1 hour 4 minutes 35 seconds</c>,
    /// <c>sixty</c>, or a bare number of seconds.
    /// </summary>
    private static bool TryParseSpoken(string text, out double seconds, out string error)
    {
        seconds = 0;
        error = string.Empty;

        // Written-out numbers become digits first, so everything below only
        // ever deals with one kind of number.
        var normalised = NumberWords.ToDigits(text);

        // Punctuation people write between the parts, and the "and" they say
        // between them. Removed rather than parsed: none of it changes the
        // answer, and leaving it in would make every rule below allow for it.
        normalised = Regex.Replace(normalised, @"[,]+", " ");
        normalised = Regex.Replace(normalised, @"\band\b", " ", RegexOptions.IgnoreCase);

        double total = 0;
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var bareNumbers = 0;
        var terms = 0;
        var consumed = 0;

        foreach (Match m in Term.Matches(normalised))
        {
            // Anything skipped over is something that was not understood. The
            // gap is quoted back rather than described, because the user is
            // looking at what they typed and the quickest thing to say is
            // which part of it did not land.
            var gap = normalised[consumed..m.Index].Trim();
            if (gap.Length > 0)
            {
                error = $"\"{gap}\" is not a time or a unit.";
                return false;
            }
            consumed = m.Index + m.Length;

            var value = double.Parse(m.Groups["num"].Value, CultureInfo.InvariantCulture);
            var unit = m.Groups["unit"].Value;
            terms++;

            if (unit.Length == 0)
            {
                bareNumbers++;
                total += value;      // seconds, settled below if it is the only term
                continue;
            }

            if (!Units.TryGetValue(unit, out var known))
            {
                error = $"\"{unit}\" is not a unit of time. Use hours, minutes or seconds.";
                return false;
            }

            if (!seen.Add(known.Name))
            {
                error = $"{known.Name} were given twice.";
                return false;
            }

            total += value * known.Multiplier;
        }

        var tail = normalised[consumed..].Trim();
        if (tail.Length > 0)
        {
            error = $"\"{tail}\" is not a time or a unit.";
            return false;
        }

        if (terms == 0)
        {
            error = $"\"{text}\" has no number in it.";
            return false;
        }

        // A number on its own is seconds. A number beside units is a part
        // somebody forgot to label — "1 hour 4" could be four of anything, and
        // guessing would be worse than asking.
        if (bareNumbers > 0 && terms > 1)
        {
            error = "Every part needs its unit — 1 hour 4 minutes, not 1 hour 4.";
            return false;
        }

        seconds = total;
        return true;
    }

    /// <summary>
    /// How a parsed time reads back, for the line under the field that says
    /// what was understood.
    /// </summary>
    public static string Describe(double seconds)
    {
        var whole = (int)Math.Floor(seconds);
        var h = whole / 3600;
        var m = whole % 3600 / 60;
        var s = seconds - h * 3600 - m * 60;

        var parts = new List<string>();
        if (h > 0) parts.Add($"{h}h");
        if (m > 0) parts.Add($"{m}m");

        // Seconds always, so a round minute still says what it is rather than
        // reading as an hour-and-minute figure with a missing piece.
        if (s > 0 || parts.Count == 0)
            parts.Add(s % 1 == 0
                ? $"{(int)s}s"
                : s.ToString("0.##", CultureInfo.InvariantCulture) + "s");

        return string.Join(" ", parts);
    }
}

/// <summary>
/// Turns numbers written as words into digits, so the time parser only ever
/// sees one kind of number.
/// </summary>
/// <remarks>
/// Up to 999, which is every number anybody types into a field measured in
/// hours, minutes and seconds. "sixty" is the case this exists for: it is a
/// perfectly ordinary way to say a length, and it was the one thing in that
/// list a digits-only parser could make no sense of at all.
/// </remarks>
public static class NumberWords
{
    private static readonly Dictionary<string, int> Small = new(StringComparer.OrdinalIgnoreCase)
    {
        ["zero"] = 0, ["one"] = 1, ["two"] = 2, ["three"] = 3, ["four"] = 4,
        ["five"] = 5, ["six"] = 6, ["seven"] = 7, ["eight"] = 8, ["nine"] = 9,
        ["ten"] = 10, ["eleven"] = 11, ["twelve"] = 12, ["thirteen"] = 13,
        ["fourteen"] = 14, ["fifteen"] = 15, ["sixteen"] = 16, ["seventeen"] = 17,
        ["eighteen"] = 18, ["nineteen"] = 19
    };

    private static readonly Dictionary<string, int> Tens = new(StringComparer.OrdinalIgnoreCase)
    {
        ["twenty"] = 20, ["thirty"] = 30, ["forty"] = 40, ["fourty"] = 40,
        ["fifty"] = 50, ["sixty"] = 60, ["seventy"] = 70, ["eighty"] = 80,
        ["ninety"] = 90
    };

    /// <summary>
    /// Rewrites every run of number words in <paramref name="text"/> as
    /// digits, leaving everything else exactly as it was.
    /// </summary>
    public static string ToDigits(string text)
    {
        // Hyphens inside a number are part of how it is written — "twenty-five"
        // — and nowhere else in a time, so they can be split on outright.
        var tokens = Regex.Split(text, @"(\s+|-)")
                          .Where(t => t.Length > 0)
                          .ToList();

        var output = new StringBuilder();
        var run = 0;
        var inRun = false;

        void FlushRun()
        {
            if (!inRun) return;
            output.Append(run.ToString(CultureInfo.InvariantCulture)).Append(' ');
            run = 0;
            inRun = false;
        }

        foreach (var token in tokens)
        {
            var word = token.Trim();
            if (word.Length == 0 || word == "-")
            {
                // Whitespace between number words holds the run together;
                // outside one it is just whitespace to pass through.
                if (!inRun) output.Append(token);
                continue;
            }

            if (Small.TryGetValue(word, out var small))
            {
                run += small;
                inRun = true;
                continue;
            }

            if (Tens.TryGetValue(word, out var tens))
            {
                run += tens;
                inRun = true;
                continue;
            }

            if (word.Equals("hundred", StringComparison.OrdinalIgnoreCase) && inRun)
            {
                run = Math.Max(run, 1) * 100;
                continue;
            }

            // "a minute" and "an hour" are how people say one of them.
            if ((word.Equals("a", StringComparison.OrdinalIgnoreCase) ||
                 word.Equals("an", StringComparison.OrdinalIgnoreCase)) && !inRun)
            {
                run = 1;
                inRun = true;
                continue;
            }

            FlushRun();
            output.Append(token);
        }

        FlushRun();
        return output.ToString();
    }
}
