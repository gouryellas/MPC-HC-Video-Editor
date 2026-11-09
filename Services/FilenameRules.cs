using System.Globalization;
using System.IO;
using System.Text;

namespace MpcHcVideoEditor.Services;

/// <summary>
/// "If this word is in the name, the category is that and the subject is
/// the other."
/// </summary>
/// <remarks>
/// What the word lists cannot express. A category is not always written in
/// the filename — a clip named after a team says which sport and which
/// league it belongs to without either word appearing. This is how that is
/// said: the trigger word is looked for, and the sections it implies are
/// supplied.
///
/// Either section may be left empty, which means "say nothing about this
/// one" rather than "clear it".
/// </remarks>
public sealed class RenameRule
{
    /// <summary>The conditional word that fires this rule.</summary>
    public string Word { get; set; } = "";

    /// <summary>The category it implies, or empty.</summary>
    public string Category { get; set; } = "";

    /// <summary>The subject it implies, or empty.</summary>
    public string Subject { get; set; } = "";

    public bool IsUsable => Word.Length > 0 && (Category.Length > 0 || Subject.Length > 0);
}

/// <summary>
/// Words that let a filename be read rather than guessed at.
/// </summary>
/// <remarks>
/// Kept in settings beside the executable, never in the repository. A
/// filename carries no marker saying which of its words is a category and
/// which is a subject; this is how the program is told.
/// </remarks>
public sealed class FilenameVocabulary
{
    /// <summary>The first section.</summary>
    public IReadOnlyCollection<string> Categories { get; init; } = Array.Empty<string>();

    /// <summary>The second — the event, or what the clip is of.</summary>
    public IReadOnlyCollection<string> Subjects { get; init; } = Array.Empty<string>();

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

    /// <summary>
    /// Words that stand for something other than themselves, and the rules
    /// that say what.
    /// </summary>
    public IReadOnlyCollection<string> Conditionals { get; init; } = Array.Empty<string>();

    public IReadOnlyList<RenameRule> Rules { get; init; } = Array.Empty<RenameRule>();

    public static readonly FilenameVocabulary Empty = new();

    public bool IsEmpty =>
        Categories.Count == 0 && Subjects.Count == 0 &&
        Blacklist.Count == 0 && Rules.Count == 0;
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

/// <summary>
/// Puts a filename into the house shape.
/// </summary>
/// <remarks>
/// <para>
/// Two jobs, and they are not equally certain. The character rules are
/// mechanical: given a name there is exactly one answer, and it can be
/// applied without asking anybody. The structure — category, activity, name,
/// subject — cannot be derived from a filename at all, because nothing in a
/// filename says which word is which. So that half reshapes what is already
/// there, uses a vocabulary where one is supplied, and otherwise leaves the
/// words in the order it found them for a person to correct.
/// </para>
/// <para>
/// Nothing here touches the disk.
/// </para>
/// </remarks>
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

        // A conditional word speaks for sections that are not written in the
        // name at all, so it is read before anything is looked for: a rule
        // saying the category is "football" settles the question whether or
        // not the word "football" appears.
        //
        // The trigger word itself stays where it is. It is usually the most
        // specific thing in the name — a team, a place, a person — and
        // deleting it would throw away what made the rule fire. The
        // blacklist is there for anybody who does want it gone.
        string? category = null;
        string? subject = null;

        foreach (var rule in vocabulary.Rules)
        {
            if (!rule.IsUsable) continue;
            if (!words.Contains(rule.Word, StringComparer.OrdinalIgnoreCase)) continue;

            // First rule to match wins each slot, so the order they are
            // written in is the order they are tried.
            if (category is null && rule.Category.Length > 0) category = rule.Category;
            if (subject is null && rule.Subject.Length > 0) subject = rule.Subject;
        }

        // Then whatever is written in the name, for any slot still open.
        category ??= Take(words, vocabulary.Categories);
        subject ??= Take(words, vocabulary.Subjects);

        // A rule that supplies a word already in the name would otherwise
        // leave it in twice.
        if (category is not null) words.RemoveAll(w => w.Equals(category, StringComparison.OrdinalIgnoreCase));
        if (subject is not null) words.RemoveAll(w => w.Equals(subject, StringComparison.OrdinalIgnoreCase));

        // The name, and — just as important — what stood in front of it.
        var person = TakePerson(words, out var before, out var after);

        if (person is not null)
        {
            // A name is third, not first. It reaches the front only when
            // there is no category and no subject, and words already in
            // front of it are the likeliest candidates for those: a file
            // reading "wedding-vows-sarah_williams-cries" is already right,
            // and pulling the name forward would break a correct name on
            // the strength of a vocabulary that happens to be empty.
            if (category is null && before.Count > 0)
            {
                category = before[0];
                before.RemoveAt(0);
            }

            if (subject is null && before.Count > 0)
            {
                subject = before[0];
                before.RemoveAt(0);
            }

            // Anything still in front joins what came after it, all of it
            // description, in the order it was found.
            words = before.Concat(after).ToList();
        }

        var ordered = new List<string>();
        if (category is not null) ordered.Add(category);
        if (subject is not null) ordered.Add(subject);
        if (person is not null) ordered.Add(person);
        ordered.AddRange(words);

        var result = string.Join("-", ordered).Trim('-');
        foreach (var tag in tags) result += "[" + tag + "]";

        return result.Length == 0 ? cleaned : result;
    }

    /// <summary>Removes and returns the first word the vocabulary knows.</summary>
    private static string? Take(List<string> words, IReadOnlyCollection<string> known)
    {
        if (known.Count == 0) return null;

        var index = words.FindIndex(w => known.Contains(w, StringComparer.OrdinalIgnoreCase));
        if (index < 0) return null;

        var word = words[index];
        words.RemoveAt(index);
        return word;
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
