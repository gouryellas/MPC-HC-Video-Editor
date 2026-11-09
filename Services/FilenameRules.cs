using System.Globalization;
using System.IO;
using System.Text;

namespace MpcHcVideoEditor.Services;

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
    public IReadOnlyCollection<string> Categories { get; init; } = Array.Empty<string>();
    public IReadOnlyCollection<string> Activities { get; init; } = Array.Empty<string>();

    /// <summary>First names, for recognising a <c>first_last</c> pair.</summary>
    public IReadOnlyCollection<string> FirstNames { get; init; } = Array.Empty<string>();

    public static readonly FilenameVocabulary Empty = new();

    public bool IsEmpty =>
        Categories.Count == 0 && Activities.Count == 0 && FirstNames.Count == 0;
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
    /// The character rules, in the only order that works.
    /// </summary>
    /// <remarks>
    /// Spaces are dealt with before the deletion step, because a space is
    /// not an allowed character and would otherwise be deleted rather than
    /// becoming a dash.
    ///
    /// Runs of dashes are collapsed at the end, which the rules do not
    /// mention but follow from them: deleting a comma out of "a , b" leaves
    /// "a--b", a doubling nobody asked for and which no rule would otherwise
    /// remove. Underscores are collapsed for the same reason.
    /// </remarks>
    internal static string Clean(string text)
    {
        var lowered = text.ToLowerInvariant();
        var plain = Transliterate(lowered);

        // Runs of spaces to one, then every space to a dash.
        var spaced = System.Text.RegularExpressions.Regex.Replace(plain, " {2,}", " ");
        var dashed = spaced.Replace(' ', '-');

        var kept = new StringBuilder(dashed.Length);
        foreach (var c in dashed)
            if (Allowed.Contains(c)) kept.Append(c);

        var result = System.Text.RegularExpressions.Regex.Replace(kept.ToString(), "-{2,}", "-");
        result = System.Text.RegularExpressions.Regex.Replace(result, "_{2,}", "_");

        return result.Trim('-');
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

        var category = Take(words, vocabulary.Categories);
        var activity = Take(words, vocabulary.Activities);

        // Joining a known first name to the word after it is done in place,
        // so a name that was already in the right position stays there.
        var joined = JoinName(words, vocabulary.FirstNames);

        // Nothing was recognised, so nothing is known about which word
        // belongs where. Leave the order exactly as found: a name that has
        // merely been tidied is obviously still untidy, where one that has
        // been shuffled on a guess looks deliberate and is wrong.
        var recognised = category is not null || activity is not null || joined;

        var ordered = new List<string>();

        if (recognised)
        {
            if (category is not null) ordered.Add(category);
            if (activity is not null) ordered.Add(activity);

            var person = TakePair(words);
            if (person is not null) ordered.Add(person);
        }

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
    /// Joins a known first name to the word after it, where it stands.
    /// </summary>
    /// <param name="firstNames">
    /// First names only. There are far more surnames than anyone will type
    /// into a settings file, and the first name is the half that repeats.
    /// </param>
    /// <returns>Whether a pair was made.</returns>
    /// <remarks>
    /// A known first name with no word after it is left alone rather than
    /// being called a person. On its own it is just a word, and promoting it
    /// to the name slot would move it past words that may well belong in
    /// front of it.
    /// </remarks>
    private static bool JoinName(List<string> words, IReadOnlyCollection<string> firstNames)
    {
        if (firstNames.Count == 0) return false;

        var index = words.FindIndex(w => firstNames.Contains(w, StringComparer.OrdinalIgnoreCase));
        if (index < 0 || index + 1 >= words.Count) return false;

        // Not onto something that is already a pair.
        if (IsPair(words[index + 1])) return false;

        words[index] = words[index] + "_" + words[index + 1];
        words.RemoveAt(index + 1);
        return true;
    }

    /// <summary>
    /// Lifts an existing <c>first_last</c> out, for placing in the name slot.
    /// </summary>
    /// <remarks>
    /// Only called once something else has been recognised, so there is a
    /// known order to place it into. With nothing recognised the pair stays
    /// where it was found.
    /// </remarks>
    private static string? TakePair(List<string> words)
    {
        var index = words.FindIndex(IsPair);
        if (index < 0) return null;

        var pair = words[index];
        words.RemoveAt(index);
        return pair;
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
