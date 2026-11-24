using System.IO;
using System.Net.Http;
using System.Text;
using MpcHcVideoEditor.Helpers;

namespace MpcHcVideoEditor.Services;

/// <summary>One downloadable language list.</summary>
/// <param name="Code">The two-letter code in its file name.</param>
/// <param name="Name">What the Settings list calls it.</param>
public sealed record LanguagePack(string Code, string Name)
{
    /// <summary>The list's file name, locally and on the release.</summary>
    public string FileName => $"translations-{Code}.txt";
}

/// <summary>
/// Word lists that turn foreign words in a filename into English: the user's
/// own <c>translations.txt</c> beside the executable, and any language lists
/// downloaded from Settings into the <c>translations</c> folder beside it.
/// </summary>
/// <remarks>
/// <para>
/// Read by Rename files (Simple and Custom rules alike) before any other
/// rule, and by every operation that would otherwise ask for a new output
/// name because the name has characters that are not allowed.
/// </para>
/// <para>
/// One entry per line, the foreign word first and the English after a comma:
/// <c>perro,dog</c>. A line starting with <c>#</c> is a comment, and blank
/// lines are ignored. Either side may be several words, so
/// <c>buenos dias,good morning</c> replaces the pair as a unit. Matched on
/// whole words only, ignoring case and accents; the longest phrase wins.
/// The user's own file is read first, so its entries beat a downloaded list's.
/// </para>
/// <para>
/// Every file is mirrored under %APPDATA%, like settings and bookmarks, and
/// put back when it is missing. Upgrading by replacing the program folder
/// would otherwise take the user's own list with it, and that one cannot be
/// downloaded again.
/// </para>
/// <para>
/// Matching is local. Downloading a language fetches that list and nothing
/// else; no filename is ever sent anywhere.
/// </para>
/// </remarks>
public static class Translations
{
    /// <summary>The user's own list, beside the executable.</summary>
    public const string FileName = "translations.txt";

    /// <summary>The languages offered in Settings, in the order offered.</summary>
    public static readonly IReadOnlyList<LanguagePack> Packs = new[]
    {
        new LanguagePack("es", "Spanish"),
        new LanguagePack("ru", "Russian"),
        new LanguagePack("fr", "French"),
    };

    /// <summary>
    /// Where the lists are downloaded from: a release of their own, so they
    /// can be updated without a new version and stay put across versions.
    /// Published as a pre-release so the update check, which asks for the
    /// latest release, never mistakes it for a version.
    /// </summary>
    private const string DownloadBase =
        UpdateCheckService.RepositoryUrl + "/releases/download/translations/";

    /// <summary>The user's own list.</summary>
    public static string FilePath => Path.Combine(PortablePaths.AppFolder, FileName);

    /// <summary>The folder downloaded lists go in. Not created on access.</summary>
    public static string PacksFolder => Path.Combine(PortablePaths.AppFolder, "translations");

    private static string BackupFile => Path.Combine(PortablePaths.BackupFolder, FileName);
    private static string BackupPacksFolder => Path.Combine(PortablePaths.BackupFolder, "translations");

    private const string Template =
        "# Translations used by Rename files, and by any operation that would\r\n" +
        "# otherwise ask for a new name because of characters that are not allowed.\r\n" +
        "#\r\n" +
        "# One entry per line: the foreign word, a comma, then the English.\r\n" +
        "# Either side may be more than one word. Lines starting with # are ignored.\r\n" +
        "# Matching ignores case and accents, and only replaces whole words.\r\n" +
        "# Entries here win over any language downloaded in Settings.\r\n" +
        "#\r\n" +
        "# Examples:\r\n" +
        "# perro,dog\r\n" +
        "# buenos dias,good morning\r\n";

    private static readonly HttpClient Http = CreateClient();

    private static HttpClient CreateClient()
    {
        var http = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
        http.DefaultRequestHeaders.UserAgent.ParseAdd($"MPC-HC-Video-Editor/{AppVersion.Display}");
        return http;
    }

    private static readonly object Gate = new();
    private static string _stamp = "";
    private static bool _restored;
    private static Dictionary<string, string> _entries = new();
    private static int _longest;

    /// <summary>Whether a language's list is on this machine.</summary>
    public static bool IsInstalled(LanguagePack pack) =>
        File.Exists(Path.Combine(PacksFolder, pack.FileName));

    /// <summary>How many entries an installed list holds, or 0.</summary>
    public static int CountEntries(LanguagePack pack)
    {
        try
        {
            var path = Path.Combine(PacksFolder, pack.FileName);
            return File.Exists(path) ? File.ReadLines(path).Count(IsEntry) : 0;
        }
        catch (Exception ex)
        {
            Diagnostics.Swallowed("Translations.CountEntries", ex);
            return 0;
        }
    }

    /// <summary>
    /// Downloads a language's list into <see cref="PacksFolder"/> and its
    /// backup. Throws with a readable message when it cannot.
    /// </summary>
    public static async Task DownloadAsync(LanguagePack pack, CancellationToken ct = default)
    {
        string text;
        try
        {
            text = await Http.GetStringAsync(DownloadBase + pack.FileName, ct);
        }
        catch (HttpRequestException ex)
        {
            throw new InvalidOperationException(
                $"Could not download the {pack.Name} list ({ex.Message}).", ex);
        }

        // A page of HTML or an empty body is not a list, and writing it would
        // leave a language that claims to be installed and translates nothing.
        if (!text.Split('\n').Any(IsEntry))
            throw new InvalidOperationException(
                $"The {pack.Name} list that came back was empty or not a word list.");

        Directory.CreateDirectory(PacksFolder);
        File.WriteAllText(Path.Combine(PacksFolder, pack.FileName), text, new UTF8Encoding(false));

        try
        {
            Directory.CreateDirectory(BackupPacksFolder);
            File.WriteAllText(Path.Combine(BackupPacksFolder, pack.FileName), text, new UTF8Encoding(false));
        }
        catch (Exception ex)
        {
            Diagnostics.Swallowed("Translations.Download.Backup", ex);
        }

        Invalidate();
    }

    /// <summary>Removes a language's list, and its backup with it.</summary>
    public static void Remove(LanguagePack pack)
    {
        // The backup goes first. Removed from the folder alone, it would be
        // restored at the next start as if the folder had been replaced.
        foreach (var path in new[]
                 {
                     Path.Combine(BackupPacksFolder, pack.FileName),
                     Path.Combine(PacksFolder, pack.FileName),
                 })
        {
            if (File.Exists(path)) File.Delete(path);
        }

        Invalidate();
    }

    /// <summary>
    /// The user's own list, created from the commented template when it does
    /// not exist yet, so there is something to open and fill in.
    /// </summary>
    public static string EnsureUserFile()
    {
        lock (Gate)
        {
            RestoreOnce();
            if (!File.Exists(FilePath))
                File.WriteAllText(FilePath, Template, new UTF8Encoding(false));
            return FilePath;
        }
    }

    private static void Invalidate()
    {
        lock (Gate) _stamp = "";
    }

    /// <summary>
    /// <paramref name="text"/> with every listed word or phrase replaced by
    /// its English. Returned unchanged when no list has anything in it.
    /// </summary>
    public static string Apply(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;

        Dictionary<string, string> entries;
        int longest;
        lock (Gate)
        {
            Refresh();
            entries = _entries;
            longest = _longest;
        }
        if (entries.Count == 0) return text;

        // Split into alternating runs: words (letters and digits) and the
        // separators between them, kept exactly so they can be put back.
        var tokens = new List<(string Text, bool IsWord)>();
        var run = new StringBuilder();
        bool? inWord = null;
        foreach (var c in text)
        {
            var isWord = IsWordChar(c);
            if (inWord is not null && inWord != isWord)
            {
                tokens.Add((run.ToString(), inWord.Value));
                run.Clear();
            }
            run.Append(c);
            inWord = isWord;
        }
        if (run.Length > 0) tokens.Add((run.ToString(), inWord!.Value));

        var result = new StringBuilder(text.Length);
        int i = 0;
        while (i < tokens.Count)
        {
            if (!tokens[i].IsWord)
            {
                result.Append(tokens[i].Text);
                i++;
                continue;
            }

            // Longest phrase first: words at i, i+2, i+4… with the separators
            // between them standing for the spaces in the entry.
            var matched = false;
            for (int count = longest; count >= 1 && !matched; count--)
            {
                var words = new List<string>();
                for (int j = i; j < tokens.Count && words.Count < count; j += 2)
                {
                    if (!tokens[j].IsWord) break;
                    words.Add(tokens[j].Text);
                }
                if (words.Count != count) continue;

                if (entries.TryGetValue(Key(words), out var english))
                {
                    result.Append(MatchCase(tokens[i].Text, english));
                    i += count * 2 - 1;
                    matched = true;
                }
            }

            if (!matched)
            {
                result.Append(tokens[i].Text);
                i++;
            }
        }

        return result.ToString();
    }

    /// <summary>
    /// Puts back, once per run, whatever the backup holds that the program
    /// folder does not: the case of a folder replaced by a new version.
    /// </summary>
    private static void RestoreOnce()
    {
        if (_restored) return;
        _restored = true;

        try
        {
            if (!File.Exists(FilePath) && File.Exists(BackupFile))
                File.Copy(BackupFile, FilePath);

            if (Directory.Exists(BackupPacksFolder))
            {
                foreach (var pack in Packs)
                {
                    var backup = Path.Combine(BackupPacksFolder, pack.FileName);
                    var local = Path.Combine(PacksFolder, pack.FileName);
                    if (!File.Exists(backup) || File.Exists(local)) continue;

                    Directory.CreateDirectory(PacksFolder);
                    File.Copy(backup, local);
                }
            }
        }
        catch (Exception ex)
        {
            Diagnostics.Swallowed("Translations.Restore", ex);
        }
    }

    /// <summary>
    /// Rereads the lists when any of them has changed, backing up the user's
    /// own list as it goes.
    /// </summary>
    private static void Refresh()
    {
        try
        {
            RestoreOnce();

            if (!File.Exists(FilePath))
                File.WriteAllText(FilePath, Template, new UTF8Encoding(false));

            // The user's list first, so its entries win; then the languages
            // in the order Settings offers them.
            var files = new List<string> { FilePath };
            files.AddRange(Packs.Select(p => Path.Combine(PacksFolder, p.FileName)).Where(File.Exists));

            var stamp = string.Join("|", files.Select(f => f + "@" + File.GetLastWriteTimeUtc(f).Ticks));
            if (stamp == _stamp) return;

            BackUpUserFile();

            var entries = new Dictionary<string, string>(StringComparer.Ordinal);
            var longest = 0;

            foreach (var file in files)
            {
                foreach (var raw in File.ReadLines(file, Encoding.UTF8))
                {
                    if (!TryParse(raw, out var foreign, out var english)) continue;

                    var words = Words(foreign);
                    if (words.Count == 0) continue;

                    // The first entry for a word wins, so a duplicate further
                    // down — or in a later file — does not overrule it.
                    if (entries.TryAdd(Key(words), english))
                        longest = Math.Max(longest, words.Count);
                }
            }

            _entries = entries;
            _longest = longest;
            _stamp = stamp;
        }
        catch (Exception ex)
        {
            // An unreadable or read-only folder must not stop an operation; it
            // just runs without translations.
            Diagnostics.Swallowed("Translations.Refresh", ex);
        }
    }

    /// <summary>
    /// Copies the user's list to the backup when it differs, so the copy is
    /// never older than the last edit the program has seen.
    /// </summary>
    private static void BackUpUserFile()
    {
        try
        {
            var text = File.ReadAllText(FilePath);

            // The untouched template is not worth keeping, and copying it would
            // overwrite a real list backed up from an earlier install.
            if (text == Template) return;
            if (File.Exists(BackupFile) && File.ReadAllText(BackupFile) == text) return;

            Directory.CreateDirectory(PortablePaths.BackupFolder);
            File.WriteAllText(BackupFile, text, new UTF8Encoding(false));
        }
        catch (Exception ex)
        {
            Diagnostics.Swallowed("Translations.Backup", ex);
        }
    }

    private static bool IsEntry(string raw) => TryParse(raw, out _, out _);

    private static bool TryParse(string raw, out string foreign, out string english)
    {
        foreign = english = "";
        var line = raw.Trim();
        if (line.Length == 0 || line.StartsWith('#')) return false;

        var comma = line.IndexOf(',');
        if (comma <= 0) return false;

        foreign = line[..comma].Trim();
        english = line[(comma + 1)..].Trim();
        return foreign.Length > 0 && english.Length > 0;
    }

    private static bool IsWordChar(char c) =>
        char.IsLetterOrDigit(c) ||
        System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c)
            == System.Globalization.UnicodeCategory.NonSpacingMark;

    private static List<string> Words(string text)
    {
        var words = new List<string>();
        var word = new StringBuilder();
        foreach (var c in text)
        {
            if (IsWordChar(c))
            {
                word.Append(c);
            }
            else if (word.Length > 0)
            {
                words.Add(word.ToString());
                word.Clear();
            }
        }
        if (word.Length > 0) words.Add(word.ToString());
        return words;
    }

    /// <summary>
    /// The matching form of a word sequence: lower case, accents dropped,
    /// single spaces between.
    /// </summary>
    private static string Key(IEnumerable<string> words) =>
        string.Join(' ', words.Select(Fold));

    private static string Fold(string word)
    {
        var decomposed = word.ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(decomposed.Length);
        foreach (var c in decomposed)
            if (System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c)
                != System.Globalization.UnicodeCategory.NonSpacingMark)
                sb.Append(c);
        return sb.ToString().Normalize(NormalizationForm.FormC);
    }

    /// <summary>
    /// Keeps a capitalised word capitalised, so the rules after this see the
    /// same word shape they would have seen in the original.
    /// </summary>
    private static string MatchCase(string original, string english)
    {
        if (original.Length > 1 && original.All(c => !char.IsLetter(c) || char.IsUpper(c)))
            return english.ToUpperInvariant();
        if (char.IsUpper(original[0]) && english.Length > 0)
            return char.ToUpperInvariant(english[0]) + english[1..];
        return english;
    }
}
