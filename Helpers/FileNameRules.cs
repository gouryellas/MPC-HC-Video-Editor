using System.Text;

namespace MpcHcVideoEditor.Helpers;

/// <summary>
/// The project's filename policy: alphanumerics plus dash, underscore,
/// square bracket and parenthesis. No spaces, nothing else, and no character
/// from that punctuation set doubled up.
/// </summary>
/// <remarks>
/// Applied to the <em>name</em> portion only — never to the directory or the
/// extension. A source file is allowed to break these rules (we do not rename
/// the user's media); what must satisfy them is any name we write, so the
/// check happens on the way to an output path.
/// </remarks>
public static class FileNameRules
{
    /// <summary>Human-readable description, shown when input is rejected.</summary>
    public const string Description =
        "Filenames may contain letters, numbers, dashes (-), underscores (_), " +
        "square brackets ([ ]) and parentheses ( ). Spaces become dashes " +
        "automatically, and none of those punctuation characters may repeat " +
        "back to back.";

    /// <summary>
    /// The punctuation a filename may contain.
    /// </summary>
    /// <remarks>
    /// Curly braces were once allowed, so that a misspelled naming variable —
    /// <c>{nam}</c> for <c>{name}</c> — came out visibly wrong rather than
    /// being quietly stripped to <c>nam</c>. They are not any more: a brace is
    /// unwanted in a filename, and this is the one set of characters the
    /// program both writes and renames by, so it should say the same thing in
    /// both places.
    ///
    /// Nothing correct is lost. <see cref="NameTemplate"/> substitutes its
    /// variables before a name reaches here, so a pattern that is spelled
    /// right never produces a brace to begin with. Only a typo is affected,
    /// and it is now stripped rather than kept.
    /// </remarks>
    private const string Punctuation = "-_[]()";

    /// <summary>True if <paramref name="c"/> may appear in a filename at all.</summary>
    public static bool IsAllowedChar(char c) =>
        char.IsAsciiLetterOrDigit(c) || Punctuation.Contains(c);

    /// <summary>
    /// True if <paramref name="name"/> (a bare filename, no directory or
    /// extension) satisfies the policy.
    /// </summary>
    public static bool IsValid(string? name)
    {
        if (string.IsNullOrEmpty(name)) return false;

        for (int i = 0; i < name.Length; i++)
        {
            if (!IsAllowedChar(name[i])) return false;

            // No doubled punctuation: "--", "__", "[[", "))", …
            if (i > 0 && name[i] == name[i - 1] && Punctuation.Contains(name[i]))
                return false;
        }

        return true;
    }

    /// <summary>
    /// Says which characters make <paramref name="name"/> fail
    /// <see cref="IsValid"/>, one entry per distinct problem, in the order
    /// they first appear. Empty when the name is valid.
    /// </summary>
    /// <remarks>
    /// Exists for names that look fine and are not. A letter from another
    /// alphabet can be indistinguishable from an English one, so the entry
    /// says so rather than leaving the user to compare the name with itself.
    /// </remarks>
    public static IReadOnlyList<string> DescribeProblems(string? name)
    {
        var problems = new List<string>();
        if (string.IsNullOrEmpty(name)) return problems;

        for (int i = 0; i < name.Length; i++)
        {
            var c = name[i];
            string? entry = null;

            if (!IsAllowedChar(c))
            {
                if (char.IsWhiteSpace(c))
                    entry = "a space";
                else if (char.IsLetter(c))
                    entry = $"“{c}” (a letter from another alphabet, U+{(int)c:X4})";
                else if (char.IsDigit(c))
                    entry = $"“{c}” (a digit from another script, U+{(int)c:X4})";
                else if (char.IsControl(c) ||
                         char.GetUnicodeCategory(c) == System.Globalization.UnicodeCategory.Format)
                    entry = $"an invisible character (U+{(int)c:X4})";
                else
                    entry = $"“{c}”";
            }
            else if (i > 0 && c == name[i - 1] && Punctuation.Contains(c))
            {
                entry = $"“{c}{c}” (doubled)";
            }

            if (entry is not null && !problems.Contains(entry))
                problems.Add(entry);
        }

        return problems;
    }

    /// <summary>
    /// Letters and digits from other scripts that are unmistakably one
    /// English letter or digit, mapped to it.
    /// </summary>
    /// <remarks>
    /// Only look-alikes: a Cyrillic "о" is an "o" to anyone reading the name,
    /// a Cyrillic "б" is read as a 6, and a "ц" as a "u". A letter with no
    /// English twin, such as "ж", is left out on purpose and still goes to the prompt,
    /// because any substitute for it would be a guess.
    ///
    /// Accented Latin letters are not listed here; they are handled by
    /// stripping the accent in <see cref="NormalizeLookalikes"/>.
    /// </remarks>
    private static readonly Dictionary<char, char> Lookalikes = new()
    {
        // Cyrillic capitals
        ['А'] = 'A', ['В'] = 'B', ['Е'] = 'E', ['Ё'] = 'E', ['З'] = '3', ['І'] = 'I',
        ['Ј'] = 'J', ['К'] = 'K', ['М'] = 'M', ['Н'] = 'H', ['О'] = 'O', ['Р'] = 'P',
        ['С'] = 'C', ['Ѕ'] = 'S', ['Т'] = 'T', ['У'] = 'Y', ['Ү'] = 'Y', ['Х'] = 'X', ['Ц'] = 'U',
        ['Ԛ'] = 'Q', ['Ԝ'] = 'W', ['Ӏ'] = 'I',
        // Cyrillic small letters
        ['а'] = 'a', ['б'] = '6', ['е'] = 'e', ['ё'] = 'e', ['з'] = '3', ['і'] = 'i',
        ['ј'] = 'j', ['к'] = 'k', ['о'] = 'o', ['р'] = 'p', ['с'] = 'c', ['ѕ'] = 's',
        ['у'] = 'y', ['х'] = 'x', ['ц'] = 'u', ['һ'] = 'h', ['ԁ'] = 'd', ['ԛ'] = 'q', ['ԝ'] = 'w',
        ['ӏ'] = 'l',
        // Greek capitals
        ['Α'] = 'A', ['Β'] = 'B', ['Ε'] = 'E', ['Ζ'] = 'Z', ['Η'] = 'H', ['Ι'] = 'I',
        ['Κ'] = 'K', ['Μ'] = 'M', ['Ν'] = 'N', ['Ο'] = 'O', ['Ρ'] = 'P', ['Τ'] = 'T',
        ['Υ'] = 'Y', ['Χ'] = 'X',
        // Greek small letters
        ['ο'] = 'o', ['ν'] = 'v', ['ι'] = 'i', ['κ'] = 'k', ['ρ'] = 'p', ['α'] = 'a',
        // Latin letters with a stroke rather than an accent, which
        // decomposition does not separate
        ['ø'] = 'o', ['Ø'] = 'O', ['đ'] = 'd', ['Đ'] = 'D', ['ł'] = 'l', ['Ł'] = 'L',
        ['ı'] = 'i', ['ħ'] = 'h', ['Ħ'] = 'H', ['ŧ'] = 't', ['Ŧ'] = 'T',
    };

    /// <summary>
    /// Replaces letters and digits from other scripts with their English
    /// equivalents, a word at a time, and only where every one in the word has
    /// an obvious equivalent.
    /// </summary>
    /// <remarks>
    /// Three kinds of character have an obvious equivalent:
    /// <list type="bullet">
    ///   <item>an accented English letter, which loses the accent ("é" → "e");</item>
    ///   <item>a full-width or other compatibility form ("Ａ" → "A", "５" → "5");</item>
    ///   <item>a digit from another script, by its value ("٣" → "3"), and the
    ///   look-alike letters in <see cref="Lookalikes"/>.</item>
    /// </list>
    /// The whole-word condition is what keeps this to the obvious. A stray
    /// Cyrillic "о" in an English word is plainly an "o", but "Привет" is a
    /// Russian word, and turning its look-alikes into English while leaving
    /// the rest would produce something that is neither. So a word containing
    /// any letter with no equivalent is left exactly as it was, and goes on to
    /// the prompt or the renamer's own rules as before.
    ///
    /// Fixed silently, like spaces: there is nothing for the user to decide.
    /// </remarks>
    public static string NormalizeLookalikes(string? name)
    {
        if (string.IsNullOrEmpty(name)) return name ?? string.Empty;
        if (name.All(c => c < 128)) return name;

        var sb = new StringBuilder(name.Length);
        var word = new StringBuilder();
        var plain = new StringBuilder();
        var convertible = true;

        void Flush()
        {
            sb.Append(convertible ? plain : word);
            word.Clear();
            plain.Clear();
            convertible = true;
        }

        foreach (var c in name)
        {
            if (!char.IsLetterOrDigit(c) && !IsMark(c))
            {
                Flush();
                sb.Append(c);
                continue;
            }

            word.Append(c);
            if (TryPlain(c, out var english)) plain.Append(english);
            else if (IsMark(c)) { /* an accent written separately: dropped */ }
            else convertible = false;
        }

        Flush();
        return sb.ToString();
    }

    /// <summary>True for an accent written as its own character.</summary>
    private static bool IsMark(char c) =>
        System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c)
            == System.Globalization.UnicodeCategory.NonSpacingMark;

    /// <summary>
    /// The English letter or digit <paramref name="c"/> obviously stands for,
    /// if there is one. Plain English letters and digits are their own.
    /// </summary>
    private static bool TryPlain(char c, out char english)
    {
        english = c;
        if (char.IsAsciiLetterOrDigit(c)) return true;

        if (Lookalikes.TryGetValue(c, out english)) return true;

        if (char.IsDigit(c))
        {
            var value = (int)char.GetNumericValue(c);
            if (value is >= 0 and <= 9) { english = (char)('0' + value); return true; }
        }

        // Decompose and accept it only when it comes down to one plain English
        // letter or digit once the accents are dropped. "é" gives "e"; "ж"
        // decomposes to itself and has no equivalent.
        var parts = c.ToString().Normalize(NormalizationForm.FormKD)
            .Where(p => !IsMark(p))
            .ToArray();

        if (parts.Length == 1 && char.IsAsciiLetterOrDigit(parts[0]))
        {
            english = parts[0];
            return true;
        }

        english = c;
        return false;
    }

    /// <summary>
    /// Replaces whitespace with dashes, and does nothing else.
    /// </summary>
    /// <remarks>
    /// Spaces are the one policy violation that is fixed silently — they are
    /// in most media filenames, the correction is obvious, and there is
    /// nothing for the user to decide. Everything else still gets asked
    /// about, so this is deliberately not <see cref="Sanitize"/>: a name with
    /// dots or ampersands comes back from here still invalid, and goes on to
    /// prompt.
    ///
    /// A whitespace run collapses to a single dash, and no dash is added
    /// beside one that is already there, so "My - Video" gives "My-Video"
    /// rather than the doubled dash that would itself be invalid.
    ///
    /// A name with no whitespace is returned untouched — including one that
    /// is invalid for some other reason, such as "clip--final", which must
    /// still be put to the user rather than quietly repaired.
    /// </remarks>
    public static string NormalizeSpaces(string? name)
    {
        if (string.IsNullOrEmpty(name)) return name ?? string.Empty;
        if (!name.Any(char.IsWhiteSpace)) return name;

        var sb = new StringBuilder(name.Length);

        int i = 0;
        while (i < name.Length)
        {
            if (!char.IsWhiteSpace(name[i]))
            {
                sb.Append(name[i]);
                i++;
                continue;
            }

            // Consume the whole run, so "a   b" gives one dash rather than three.
            while (i < name.Length && char.IsWhiteSpace(name[i])) i++;

            // A dash on either side of the run already separates the words.
            var dashBefore = sb.Length > 0 && sb[^1] == '-';
            var dashAfter = i < name.Length && name[i] == '-';
            if (!dashBefore && !dashAfter) sb.Append('-');
        }

        // Leading and trailing dashes here are artefacts of the substitution,
        // never something the user typed.
        return sb.ToString().Trim('-');
    }

    /// <summary>
    /// The recommended correction for <paramref name="name"/>: spaces become
    /// dashes, anything still disallowed is dropped, and runs of the same
    /// punctuation character collapse to one. Returns the input unchanged when
    /// it is already valid.
    /// </summary>
    public static string Sanitize(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "output";

        name = NormalizeLookalikes(name);
        var sb = new StringBuilder(name.Length);

        foreach (var raw in name)
        {
            // Whitespace becomes a dash so word boundaries survive; every
            // other disallowed character is simply dropped.
            var c = char.IsWhiteSpace(raw) ? '-' : raw;
            if (!IsAllowedChar(c)) continue;

            // Collapse a repeat of the same punctuation character.
            if (sb.Length > 0 && sb[^1] == c && Punctuation.Contains(c)) continue;

            sb.Append(c);
        }

        // Leading/trailing dashes read as artefacts of the substitution.
        var result = sb.ToString().Trim('-');
        return result.Length == 0 ? "output" : result;
    }

    /// <summary>
    /// Splits a filename into the part the user may rename and the trailing
    /// bracket suffix that belongs to the naming tag. <c>"clip[done]"</c>
    /// gives <c>("clip", "[done]")</c>; a name with no suffix gives an empty
    /// second element.
    /// </summary>
    public static (string Stem, string Suffix) SplitSuffix(string nameWithoutExtension)
    {
        if (!nameWithoutExtension.EndsWith(']')) return (nameWithoutExtension, string.Empty);

        var open = nameWithoutExtension.LastIndexOf('[');
        return open <= 0
            ? (nameWithoutExtension, string.Empty)
            : (nameWithoutExtension[..open], nameWithoutExtension[open..]);
    }
}
