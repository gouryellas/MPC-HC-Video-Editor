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
        "square brackets ([ ]) and parentheses ( ). Spaces are not allowed, and " +
        "none of those punctuation characters may repeat back to back.";

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
    /// bracket suffix that belongs to the naming style. <c>"clip[done]"</c>
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
