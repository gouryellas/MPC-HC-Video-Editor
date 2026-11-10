using System.Globalization;
using System.IO;
using System.Text;

namespace MpcHcVideoEditor.Services;

/// <summary>
/// "If these words are in the name, lead with those words in that order."
/// </summary>
/// <remarks>
/// <para>
/// One word list cannot say how its words relate. A rule is where that is
/// written: a condition of up to three words that must all be present, and
/// an ordering of up to three words to put at the front of the name when
/// they are.
/// </para>
/// <para>
/// A word in the ordering that is not in the filename is added — which is
/// the point of it. A clip named after a team can be made to say which sport
/// and which league it belongs to without either word ever having been typed
/// into the name.
/// </para>
/// <para>
/// The otherwise ordering is used when the condition does not hold. A rule
/// that has one therefore always applies, which ends the matter; a rule
/// without one steps aside and lets the next rule be tried.
/// </para>
/// </remarks>
public sealed class RenameRule
{
    /// <summary>
    /// Words that must all be present. Up to three; empties are ignored.
    /// </summary>
    public List<string> If { get; set; } = new();

    /// <summary>The order to lead with when the condition holds.</summary>
    public List<string> Then { get; set; } = new();

    /// <summary>The order to lead with when it does not, if any.</summary>
    public List<string> Else { get; set; } = new();

    /// <summary>Just the words, without the gaps left by unused slots.</summary>
    public static IReadOnlyList<string> Used(IEnumerable<string>? slots) =>
        (slots ?? Array.Empty<string>())
        .Where(w => !string.IsNullOrWhiteSpace(w))
        .Select(w => w.Trim())
        .ToList();

    public IReadOnlyList<string> Conditions => Used(If);
    public IReadOnlyList<string> ThenOrder => Used(Then);
    public IReadOnlyList<string> ElseOrder => Used(Else);

    /// <summary>
    /// Whether this rule says anything. A condition with no ordering behind
    /// it, either way, is a sentence that stops halfway.
    /// </summary>
    public bool IsUsable =>
        Conditions.Count > 0 && (ThenOrder.Count > 0 || ElseOrder.Count > 0);

    /// <summary>
    /// The ordering this rule calls for, or null if it has nothing to say
    /// about this name.
    /// </summary>
    public IReadOnlyList<string>? OrderFor(IReadOnlyCollection<string> words)
    {
        if (!IsUsable) return null;

        var holds = Conditions.All(c => words.Contains(c, StringComparer.OrdinalIgnoreCase));

        if (holds) return ThenOrder.Count > 0 ? ThenOrder : null;
        return ElseOrder.Count > 0 ? ElseOrder : null;
    }
}

/// <summary>
/// The words a filename can be read with, and the rules that order them.
/// </summary>
/// <remarks>
/// Kept in settings beside the executable, never in the repository. A
/// filename carries no marker saying which of its words matters; this is how
/// the program is told.
/// </remarks>
public sealed class FilenameVocabulary
{
    /// <summary>
    /// Every word worth recognising, in one list.
    /// </summary>
    /// <remarks>
    /// One list rather than a column per section. Which section a word
    /// belongs to is not a property of the word — the same word can lead one
    /// name and trail another — so it is the rules that decide, and the list
    /// just says which words are worth noticing at all.
    /// </remarks>
    public IReadOnlyCollection<string> Words { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Words struck out of every filename.
    /// </summary>
    /// <remarks>
    /// Matched whole, never as part of a longer word: a blacklisted "cam"
    /// takes out "cam" and leaves "camera" alone. Deleting a run of letters
    /// wherever it appeared would quietly damage words nobody meant to
    /// touch, and the damage would be invisible in a list of two hundred
    /// names.
    /// </remarks>
    public IReadOnlyCollection<string> Blacklist { get; init; } = Array.Empty<string>();

    public IReadOnlyList<RenameRule> Rules { get; init; } = Array.Empty<RenameRule>();

    public static readonly FilenameVocabulary Empty = new();

    public bool IsEmpty => Words.Count == 0 && Blacklist.Count == 0 && Rules.Count == 0;
}

/// <summary>What a rename would do to one file.</summary>
/// <param name="Original">The name as it is now, without its folder.</param>
/// <param name="Proposed">What the rules make of it.</param>
/// <param name="Note">Why it needs attention, or empty.</param>
public sealed record RenameProposal(string Original, string Proposed, string Note)
{
    /// <summary>Whether the rules would change anything at all.</summary>
    public bool Changes => !string.Equals(Original, Proposed, StringComparison.Ordinal);
}

public static class FilenameRules
{
    /// <summary>Everything a name may contain, besides the extension's dot.</summary>
    private const string Allowed = "0123456789abcdefghijklmnopqrstuvwxyz()[]_-";

    /// <summary>
    /// Letters that do not come apart into a letter and an accent, and so
    /// have to be spelled out by hand.
    /// </summary>
    /// <remarks>
    /// Decomposition handles the large majority — é is e plus an acute, and
    /// dropping the accent leaves the letter. These are the ones where the
    /// mark is part of the letter's own shape, so there is nothing to strip
    /// and they would otherwise simply vanish.
    /// </remarks>
    private static readonly Dictionary<char, string> Spelled = new()
    {
        ['ø'] = "o", ['æ'] = "ae", ['œ'] = "oe", ['ß'] = "ss",
        ['đ'] = "d", ['ð'] = "d", ['þ'] = "th", ['ł'] = "l",
        ['ı'] = "i", ['ħ'] = "h", ['ŋ'] = "n", ['ŧ'] = "t",
    };

    /// <summary>
    /// Applies every rule to one file's name.
    /// </summary>
    /// <param name="fileName">A name with its extension, no folder.</param>
    /// <param name="vocabulary">Words to recognise, or null for none.</param>
    public static string Apply(string fileName, FilenameVocabulary? vocabulary = null)
    {
        // The extension comes off first. A dot is not an allowed character,
        // so every dot in the name is deleted — and the one separating the
        // extension would go with them if it were still attached.
        var extension = Path.GetExtension(fileName);
        var stem = Path.GetFileNameWithoutExtension(fileName);

        var cleaned = Clean(stem);
        if (cleaned.Length == 0) return fileName;   // nothing left; see Propose

        cleaned = Restructure(cleaned, vocabulary ?? FilenameVocabulary.Empty);

        var ext = Clean(extension.TrimStart('.'));
        return ext.Length > 0 ? cleaned + "." + ext : cleaned;
    }

    /// <summary>
    /// Whether the rules would leave nothing of a name behind.
    /// </summary>
    /// <remarks>
    /// True for a name written entirely in a script with no plain-letter
    /// equivalent. Such a file is left alone, the alternative being to
    /// rename it to ".mp4".
    /// </remarks>
    internal static bool EmptiesOut(string fileName) =>
        Clean(Path.GetFileNameWithoutExtension(fileName)).Length == 0;

    /// <summary>
    /// The character rules.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A run of characters that are not allowed — spaces among them — is one
    /// separator, and what becomes of it depends on where it sits. Between
    /// two kept characters it was dividing two things, so it becomes a
    /// dash: "vol2.take3" is two parts and should stay two parts. At either
    /// end it was dividing nothing, so it goes.
    /// </para>
    /// <para>
    /// That one rule covers the spaces as well. A space is not an allowed
    /// character, so "wedding  vows" is a word, a separator and a word, and
    /// arrives at "wedding-vows" without needing a rule of its own.
    /// </para>
    /// <para>
    /// Then no character may repeat: "((((" becomes "(", "---" becomes "-".
    /// Letters and digits are exempt, which is not in the instruction but
    /// has to be — collapsing those would turn "bookkeeper" into "bokeper"
    /// and "2000" into "20".
    /// </para>
    /// </remarks>
    internal static string Clean(string text)
    {
        var plain = Transliterate(text.ToLowerInvariant());

        var kept = new StringBuilder(plain.Length);

        for (int i = 0; i < plain.Length; i++)
        {
            if (Allowed.Contains(plain[i])) { kept.Append(plain[i]); continue; }

            // The whole run of unwanted characters, as one separator.
            int end = i;
            while (end < plain.Length && !Allowed.Contains(plain[end])) end++;

            var dividing = kept.Length > 0 && end < plain.Length;
            if (dividing) kept.Append('-');

            i = end - 1;
        }

        return Squeeze(kept.ToString()).Trim('-');
    }

    /// <summary>
    /// Reduces any repeat of a punctuation character to one of it.
    /// </summary>
    /// <remarks>
    /// Letters and digits are left alone. Doubles are ordinary in words and
    /// in numbers, and the instruction is plainly about the marks between
    /// things rather than about the things themselves.
    /// </remarks>
    internal static string Squeeze(string text)
    {
        var result = new StringBuilder(text.Length);

        foreach (var c in text)
        {
            var repeat = result.Length > 0 && result[^1] == c;
            if (repeat && !char.IsLetterOrDigit(c)) continue;

            result.Append(c);
        }

        return result.ToString();
    }

    /// <summary>
    /// Brings accented and composed letters down to plain ones.
    /// </summary>
    /// <remarks>
    /// Rather than deleting them. Deleting turns a name into something that
    /// cannot be searched for or recognised — a letter missing from the
    /// middle of a word is worse than a letter that lost its accent.
    /// </remarks>
    internal static string Transliterate(string text)
    {
        var spelled = new StringBuilder(text.Length);
        foreach (var c in text)
        {
            if (Spelled.TryGetValue(c, out var replacement)) spelled.Append(replacement);
            else spelled.Append(c);
        }

        // Decomposed, so an accent becomes its own character and can be
        // dropped on its own, leaving the letter it was sitting on.
        var decomposed = spelled.ToString().Normalize(NormalizationForm.FormD);

        var stripped = new StringBuilder(decomposed.Length);
        foreach (var c in decomposed)
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                stripped.Append(c);

        return stripped.ToString().Normalize(NormalizationForm.FormC);
    }

    /// <summary>
    /// Arranges the parts into
    /// <c>category-activity-first_last-subject[tag]</c> as far as it can
    /// tell what they are.
    /// </summary>
    /// <remarks>
    /// Conservative on purpose. A word is only treated as a category because
    /// the vocabulary says so; nothing is inferred from position or spelling.
    /// With no vocabulary this still earns its place — it lifts a bracketed
    /// tag to the end and joins a recognised first and last name with the
    /// underscore reserved for them, which is most of the shape.
    ///
    /// Words it cannot place keep the order they were found in. Reordering
    /// on a guess would produce a name that looks deliberate and is wrong,
    /// which is harder to spot than one that is obviously still untidy.
    /// </remarks>
    internal static string Restructure(string cleaned, FilenameVocabulary vocabulary)
    {
        // Any bracketed groups, held aside and re-attached at the end in the
        // order they appeared.
        var tags = new List<string>();
        var body = System.Text.RegularExpressions.Regex.Replace(cleaned, @"\[([^\]]*)\]", m =>
        {
            if (m.Groups[1].Value.Length > 0) tags.Add(m.Groups[1].Value);
            return "-";
        });

        var words = body.Split('-', StringSplitOptions.RemoveEmptyEntries).ToList();
        if (words.Count == 0 && tags.Count == 0) return cleaned;

        // Struck out first, so a blacklisted word cannot take a slot or
        // push a real word into the description.
        if (vocabulary.Blacklist.Count > 0)
            words.RemoveAll(w => vocabulary.Blacklist.Contains(w, StringComparer.OrdinalIgnoreCase));

        // The first rule with something to say decides the order of what
        // leads the name. Tried top to bottom, so the order they are written
        // in is the order they are tried — and a rule carrying an otherwise
        // always has something to say, which ends the search.
        IReadOnlyList<string>? lead = null;

        foreach (var rule in vocabulary.Rules)
        {
            lead = rule.OrderFor(words);
            if (lead is not null) break;
        }

        var leading = new List<string>();

        if (lead is not null)
        {
            // Listed once, at the front, in the order the rule gives. A word
            // the rule names but the name does not have is added: that is
            // what lets a clip say which sport it belongs to without the
            // word ever having been typed into it.
            foreach (var word in lead)
            {
                words.RemoveAll(w => w.Equals(word, StringComparison.OrdinalIgnoreCase));
                if (!leading.Contains(word, StringComparer.OrdinalIgnoreCase)) leading.Add(word);
            }
        }

        // The name, and — just as important — what stood in front of it.
        var person = TakePerson(words, out var before, out var after);

        if (person is not null)
        {
            // A name is not first. With no rule to say what leads, the words
            // already standing in front of it are the best evidence there
            // is — a file reading "wedding-vows-sarah_williams-cries" is
            // already right, and pulling the name forward would break it on
            // the strength of an empty word list.
            if (leading.Count == 0)
            {
                while (leading.Count < 2 && before.Count > 0)
                {
                    leading.Add(before[0]);
                    before.RemoveAt(0);
                }
            }

            // Anything still in front joins what came after it, all of it
            // description, in the order it was found.
            words = before.Concat(after).ToList();
        }

        var ordered = new List<string>(leading);
        if (person is not null) ordered.Add(person);
        ordered.AddRange(words);

        var result = string.Join("-", ordered).Trim('-');
        foreach (var tag in tags) result += "[" + tag + "]";

        return result.Length == 0 ? cleaned : result;
    }


    /// <summary>
    /// Lifts out the name, which is whatever is already written as a
    /// <c>first_last</c> pair.
    /// </summary>
    /// <param name="before">What stood in front of the name.</param>
    /// <param name="after">What followed it.</param>
    /// <remarks>
    /// The underscore is the only evidence. There was once a list of first
    /// names to recognise as well, which is gone: a list of the people in
    /// somebody's library is a different and much more personal thing than a
    /// list of categories, and the underscore already marks a name
    /// unambiguously wherever one has been written properly.
    /// </remarks>
    private static string? TakePerson(List<string> words,
                                      out List<string> before, out List<string> after)
    {
        before = new List<string>();
        after = new List<string>();

        var index = words.FindIndex(IsPair);
        if (index < 0) return null;

        var person = words[index];

        before.AddRange(words.Take(index));
        after.AddRange(words.Skip(index + 1));

        words.RemoveAt(index);
        return person;
    }

    /// <summary>
    /// A joined name, as opposed to a stray underscore. Both halves have to
    /// exist, or "_" on its own would read as somebody's name.
    /// </summary>
    private static bool IsPair(string word)
    {
        var at = word.IndexOf('_');
        return at > 0 && at < word.Length - 1;
    }

    /// <summary>
    /// Works out what a whole folder's worth of renames would be, including
    /// which of them need looking at.
    /// </summary>
    /// <remarks>
    /// Collisions are reported rather than resolved. Appending a number
    /// would make two files that are probably copies of each other look like
    /// a deliberate numbered pair, and the user is about to be shown the
    /// list anyway — this is a decision they can make better than a rule
    /// can.
    /// </remarks>
    public static IReadOnlyList<RenameProposal> Propose(
        IEnumerable<string> fileNames, FilenameVocabulary? vocabulary = null)
    {
        var proposals = new List<RenameProposal>();
        var taken = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var original in fileNames)
        {
            var proposed = Apply(original, vocabulary);
            var note = "";

            if (EmptiesOut(original))
            {
                proposed = original;
                note = "No allowed characters left — not renamed";
            }

            taken.TryGetValue(proposed, out var seen);
            taken[proposed] = seen + 1;

            if (seen > 0 && !string.Equals(proposed, original, StringComparison.OrdinalIgnoreCase))
                note = "Another file already takes this name";

            proposals.Add(new RenameProposal(original, proposed, note));
        }

        return proposals;
    }
}
