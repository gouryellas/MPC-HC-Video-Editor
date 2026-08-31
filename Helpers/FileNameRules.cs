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
        "square brackets ([ ]), parentheses ( ) and curly braces ({ }). Spaces " +
        "become dashes automatically, and none of those punctuation characters " +
        "may repeat back to back.";

    /// <summary>
    /// The punctuation a filename may contain.
    /// </summary>
    /// <remarks>
    /// Curly braces are allowed so a naming tag can use them, and so a
    /// misspelled filename variable — <c>{nam}</c> for <c>{name}</c> — comes
    /// out visibly wrong rather than silently stripped to <c>nam</c>. Windows
    /// has no objection to braces in a filename; only this project's own policy
    /// ever did.
    /// </remarks>
    private const string Punctuation = "-_[](){}";

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
