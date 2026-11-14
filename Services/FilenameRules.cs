using System.Globalization;
using System.IO;
using System.Text;

namespace MpcHcVideoEditor.Services;

/// <summary>
/// Whether two words in a filename mean the same thing.
/// </summary>
/// <remarks>
/// <para>
/// So that "wave" in the word list also catches waves, waving and waved
/// without each having to be typed out. Both sides are reduced to a stem and
/// the stems compared, rather than variants being generated from the list
/// word — generating only works one way round, and the variant is at least
/// as likely to be the one in the filename as the one in the list.
/// </para>
/// <para>
/// The stem is not a word and is not meant to be read: bounce, bounces,
/// bouncing, bounced, bouncy and bounciest all come down to "bounc", which
/// is the whole point of it.
/// </para>
/// </remarks>
public enum WordMatchMode
{
    /// <summary>The word as written, and nothing else.</summary>
    Precise,

    /// <summary>And its endings: wave catches waves, waved, waving.</summary>
    Variant,

    /// <summary>And the ones that change shape: go catches went and gone.</summary>
    Irregular
}

public sealed class WordMatch : IEqualityComparer<string>
{
    public static readonly WordMatch Exact = For(WordMatchMode.Precise);
    public static readonly WordMatch Variants = For(WordMatchMode.Variant);
    public static readonly WordMatch Irregulars = For(WordMatchMode.Irregular);

    public static WordMatch For(WordMatchMode mode, bool regex = false) => new(mode, regex);

    private WordMatch(WordMatchMode mode, bool regex)
    {
        _mode = mode;
        _regex = regex;
    }

    private readonly WordMatchMode _mode;
    private readonly bool _regex;

    public bool Equals(string? a, string? b)
    {
        if (a is null || b is null) return a is null && b is null;
        if (string.Equals(a, b, StringComparison.OrdinalIgnoreCase)) return true;

        // One of the two came from a word list and the other out of a
        // filename, and nothing here knows which way round. Both are
        // tried: a filename word used as a pattern can only ever match
        // itself, so the wrong guess costs nothing.
        if (_regex) return Matches(a, b) || Matches(b, a);

        if (_mode == WordMatchMode.Precise) return false;

        if (_mode == WordMatchMode.Irregular
            && Family(a) is { } family
            && family == Family(b))
        {
            return true;
        }

        return Stem(a) == Stem(b);
    }

    public int GetHashCode(string word) =>
        _regex
            // Patterns cannot hash to what they match, so everything
            // lands in one bucket and the comparison stays linear.
            ? 0
            : Key(word).GetHashCode();

    private string Key(string word) => _mode switch
    {
        WordMatchMode.Precise => word.ToLowerInvariant(),
        WordMatchMode.Irregular => Family(word) ?? Stem(word),
        _ => Stem(word)
    };

    /// <summary>
    /// Whether the whole of <paramref name="word"/> is what
    /// <paramref name="pattern"/> describes.
    /// </summary>
    /// <remarks>
    /// Anchored, so "wave" is still the word wave and not a licence to
    /// match waveform. A pattern that will not compile is treated as
    /// matching nothing rather than taking the rename down — a half-typed
    /// expression in a word list is an ordinary thing to find.
    /// </remarks>
    private static bool Matches(string pattern, string word)
    {
        try
        {
            return System.Text.RegularExpressions.Regex.IsMatch(
                word, "^(?:" + pattern + ")$",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase
                | System.Text.RegularExpressions.RegexOptions.CultureInvariant,
                TimeSpan.FromMilliseconds(250));
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// The words that change shape instead of taking an ending.
    /// </summary>
    /// <remarks>
    /// <para>
    /// No rule reaches these — went is not go with a suffix — so they are
    /// listed. The first word of each line is what the rest answer to;
    /// every member is written out, including the base, so that any two
    /// of them meet at the same key.
    /// </para>
    /// <para>
    /// The common ones, not all of English. That is why this is a mode
    /// somebody turns on rather than how matching always works: a list
    /// like this is never finished, and the gaps in it are invisible
    /// until a file does not get renamed.
    /// </para>
    /// </remarks>
    private static readonly string[] Families =
    {
        "be am is are was were been being",
        "have has having had",
        "do does doing did done",
        "go goes going went gone",
        "get gets getting got gotten",
        "make makes making made",
        "take takes taking took taken",
        "see sees seeing saw seen",
        "come comes coming came",
        "run runs running ran",
        "give gives giving gave given",
        "know knows knowing knew known",
        "think thinks thinking thought",
        "find finds finding found",
        "tell tells telling told",
        "become becomes becoming became",
        "leave leaves leaving left",
        "feel feels feeling felt",
        "bring brings bringing brought",
        "begin begins beginning began begun",
        "keep keeps keeping kept",
        "hold holds holding held",
        "write writes writing wrote written",
        "stand stands standing stood",
        "hear hears hearing heard",
        "let lets letting",
        "mean means meaning meant",
        "set sets setting",
        "meet meets meeting met",
        "pay pays paying paid",
        "sit sits sitting sat",
        "speak speaks speaking spoke spoken",
        "lie lies lying lay lain",
        "lead leads leading led",
        "read reads reading",
        "grow grows growing grew grown",
        "lose loses losing lost",
        "fall falls falling fell fallen",
        "send sends sending sent",
        "build builds building built",
        "understand understands understanding understood",
        "draw draws drawing drew drawn",
        "break breaks breaking broke broken",
        "spend spends spending spent",
        "cut cuts cutting",
        "rise rises rising rose risen",
        "drive drives driving drove driven",
        "buy buys buying bought",
        "wear wears wearing wore worn",
        "choose chooses choosing chose chosen",
        "seek seeks seeking sought",
        "throw throws throwing threw thrown",
        "catch catches catching caught",
        "deal deals dealing dealt",
        "win wins winning won",
        "forget forgets forgetting forgot forgotten",
        "teach teaches teaching taught",
        "eat eats eating ate eaten",
        "fly flies flying flew flown",
        "ride rides riding rode ridden",
        "swim swims swimming swam swum",
        "sing sings singing sang sung",
        "ring rings ringing rang rung",
        "drink drinks drinking drank drunk",
        "sleep sleeps sleeping slept",
        "hide hides hiding hid hidden",
        "shoot shoots shooting shot",
        "fight fights fighting fought",
        "hang hangs hanging hung",
        "shake shakes shaking shook shaken",
        "wake wakes waking woke woken",
        "freeze freezes freezing froze frozen",
        "steal steals stealing stole stolen",
        "tear tears tearing tore torn",
        "blow blows blowing blew blown",
        "beat beats beating beaten",
        "bite bites biting bit bitten",
        "hit hits hitting",
        "hurt hurts hurting",
        "put puts putting",
        "shut shuts shutting",
        "cost costs costing",
        "feed feeds feeding fed",
        "sell sells selling sold",
        "swing swings swinging swung",
        "stick sticks sticking stuck",
        "strike strikes striking struck",
        "slide slides sliding slid",
        "spin spins spinning spun",
        "spread spreads spreading",
        "shine shines shining shone",
        "sweep sweeps sweeping swept",
        "dig digs digging dug",
        "light lights lighting lit",

        // And the nouns that do the same thing.
        "child children",
        "man men",
        "woman women",
        "person people",
        "foot feet",
        "tooth teeth",
        "goose geese",
        "mouse mice",
        "louse lice",
        "ox oxen",
        "leaf leaves",
        "knife knives",
        "wife wives",
        "life lives",
        "half halves",
        "shelf shelves",
        "wolf wolves",
        "calf calves",
        "loaf loaves",
        "thief thieves",
        "cactus cacti",
        "focus foci",
        "fungus fungi",
        "nucleus nuclei",
        "analysis analyses",
        "crisis crises",
        "thesis theses",
        "phenomenon phenomena",
        "criterion criteria",
        "index indices",
        "matrix matrices",
        "vertex vertices",
        "appendix appendices"
    };

    private static readonly Dictionary<string, string> Irregular = Build();

    private static Dictionary<string, string> Build()
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var family in Families)
        {
            var words = family.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            // First come, first served. A word in two families — lay and
            // lie overlap, as do leaves on a tree and leaves a room —
            // belongs to the one listed first rather than silently
            // rebinding the earlier one.
            foreach (var word in words) map.TryAdd(word, words[0]);
        }

        return map;
    }

    /// <summary>The family this word belongs to, or null.</summary>
    internal static string? Family(string word) =>
        Irregular.TryGetValue(word, out var key) ? key : null;

    /// <summary>The shortest a stem is allowed to get.</summary>
    /// <remarks>
    /// Below this a suffix is left alone. Taking the s off "bus" or the ing
    /// off "using" leaves something that will collide with anything.
    /// </remarks>
    private const int Floor = 3;

    /// <summary>
    /// A word with its inflections taken off.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Deliberately not a full stemmer. The published ones are built for
    /// searching prose and are aggressive enough to fold unrelated words
    /// together; this handles the endings that actually turn up in
    /// filenames — plural, third person, participle, past, comparative,
    /// superlative — and nothing else.
    /// </para>
    /// <para>
    /// Irregulars are not covered. Ran will not match run and swam will not
    /// match swim: catching those needs a dictionary, and a dictionary that
    /// is nearly complete is worse than none, because the gaps are
    /// invisible.
    /// </para>
    /// </remarks>
    internal static string Stem(string word)
    {
        var w = word.ToLowerInvariant();
        if (w.Length <= Floor) return w;

        // Until nothing more comes off. One pass is not enough and, worse,
        // is not symmetric: "rendered" loses its -ed and stops at "render",
        // while "render" loses its -er and reaches "rend", so the two no
        // longer match each other. A word has to arrive at the same place
        // whichever end of its family it is written from.
        for (int pass = 0; pass < 4; pass++)
        {
            var shorter = Shorten(w);
            if (shorter == w) break;

            w = shorter;
        }

        // Running doubles its n and stopped doubles its p. Undone before
        // the vowel below, so "runn" is "run" and not "runn" minus nothing.
        if (w.Length > Floor && w[^1] == w[^2] && !IsVowel(w[^1])) w = w[..^1];

        // One trailing e, y or i, and only one. It is what brings bounce,
        // bouncy and the bounci- of bounciest together. Cascading instead
        // would take movie down to mov and fold it into move.
        if (w.Length > Floor && (w[^1] is 'e' or 'y' or 'i')) w = w[..^1];

        return w;
    }

    /// <summary>Takes off whichever one ending the word has, if any.</summary>
    private static string Shorten(string w)
    {
        // Longest endings first: "bounciest" is -iest, not -est of "bounci"
        // followed by nothing, and "parties" is -ies rather than -s.
        if (Cut(w, "iest", out var cut)) return cut;
        if (Cut(w, "ies", out cut)) return cut;

        // -es only where it had to be: after a hiss, where a bare -s cannot
        // be pronounced. Boxes, dishes, watches, classes.
        if (w.EndsWith("es", StringComparison.Ordinal)
            && w.Length > Floor + 2
            && (w[..^2].EndsWith("s", StringComparison.Ordinal)
                || w[..^2].EndsWith("x", StringComparison.Ordinal)
                || w[..^2].EndsWith("z", StringComparison.Ordinal)
                || w[..^2].EndsWith("ch", StringComparison.Ordinal)
                || w[..^2].EndsWith("sh", StringComparison.Ordinal)))
        {
            return w[..^2];
        }

        if (Cut(w, "ing", out cut)) return cut;
        if (Cut(w, "est", out cut)) return cut;
        if (Cut(w, "ed", out cut)) return cut;
        if (Cut(w, "er", out cut)) return cut;

        // A bare -s last, and never off a word that ends in one already:
        // pass is not the plural of pa.
        if (w.EndsWith("s", StringComparison.Ordinal)
            && !w.EndsWith("ss", StringComparison.Ordinal)
            && w.Length > Floor)
        {
            return w[..^1];
        }

        return w;
    }

    private static bool Cut(string w, string ending, out string cut)
    {
        cut = w;

        if (!w.EndsWith(ending, StringComparison.Ordinal)) return false;
        if (w.Length - ending.Length < Floor) return false;

        cut = w[..^ending.Length];
        return true;
    }

    private static bool IsVowel(char c) => c is 'a' or 'e' or 'i' or 'o' or 'u';
}

/// <summary>
/// The six things a line of a rule can say.
/// </summary>
/// <remarks>
/// Written out rather than an enum because they are also the entries in a
/// dropdown, and a enum with a separate table of display names is two places
/// to change and one of them gets forgotten.
/// </remarks>
public static class RuleActions
{
    public const string IfFound = "if these are found";
    public const string IfNotFound = "if these are NOT found";
    public const string Order = "the order is";
    public const string Delete = "delete these";
    public const string DeleteFound = "delete the found words";
    public const string Change = "change these";
    public const string ChangeTo = "change to";

    public static readonly IReadOnlyList<string> All = new[]
    {
        IfFound, IfNotFound, Order, Delete, DeleteFound, Change, ChangeTo
    };

    /// <summary>
    /// Whether this phrase stands on its own, with no words after it.
    /// </summary>
    /// <remarks>
    /// Only one does. It acts on the words the conditions above it
    /// already name, which is the point of it: a rule that recognises
    /// twenty makes of car and then has to list all twenty again to
    /// strike them out is the same list written twice, and the second
    /// copy is the one that falls out of step.
    /// </remarks>
    public static bool Wordless(string? action) => Is(action, DeleteFound);

    public static bool Is(string? action, string what) =>
        string.Equals(action, what, StringComparison.OrdinalIgnoreCase);

    /// <summary>Whether this line asks a question rather than doing something.</summary>
    public static bool IsCondition(string? action) =>
        Is(action, IfFound) || Is(action, IfNotFound);

    /// <summary>
    /// Whether this line does something, as opposed to asking or to not
    /// having been chosen yet.
    /// </summary>
    /// <remarks>
    /// Named rather than taken as "anything that is not a condition". A row
    /// is added with no action on it and gets one afterwards, so "not a
    /// condition" is also true of a row that says nothing at all — and a
    /// rule made only of those would have counted as finished.
    /// </remarks>
    public static bool IsDoing(string? action) =>
        Is(action, Order) || Is(action, Delete) || Is(action, DeleteFound)
        || Is(action, Change) || Is(action, ChangeTo);
}

/// <summary>
/// One line of a rule: an action, and up to three words for it to act on.
/// </summary>
public sealed class RuleLine
{
    public string Action { get; set; } = RuleActions.IfFound;

    /// <summary>Up to three words. Empties are gaps, not words.</summary>
    public List<string> Words { get; set; } = new();

    /// <summary>
    /// What sits between the words: "and" or "or", one for each gap. Read
    /// only on a condition — in an ordering or a deletion there is nothing
    /// for a joiner to mean.
    /// </summary>
    public List<string> Joins { get; set; } = new();

    public RuleLine() { }

    public RuleLine(string action, IEnumerable<string>? words = null,
                    IEnumerable<string>? joins = null)
    {
        Action = action;
        Words = words?.ToList() ?? new List<string>();
        Joins = joins?.ToList() ?? new List<string>();
    }

    /// <summary>Just the words, without the gaps left by unused slots.</summary>
    public IReadOnlyList<string> Used => RenameRule.Used(Words);

    /// <summary>One slot by position, or null if it was left blank.</summary>
    public string? Slot(int index) =>
        index >= 0 && index < Words.Count && !string.IsNullOrWhiteSpace(Words[index])
            ? Words[index].Trim()
            : null;

    /// <summary>
    /// The condition as it is read: each word after the first carrying the
    /// joiner written in front of it.
    /// </summary>
    /// <remarks>
    /// Paired against the raw slots rather than the used ones, so a gap left
    /// in the middle does not shift the joiners along. The joiner before
    /// slot two is the first one whether or not slot one was filled in.
    /// </remarks>
    public IReadOnlyList<(string Word, bool Or)> Terms
    {
        get
        {
            var terms = new List<(string, bool)>();

            for (int i = 0; i < Words.Count && i < 3; i++)
            {
                if (string.IsNullOrWhiteSpace(Words[i])) continue;

                var joiner = i > 0 && i - 1 < Joins.Count
                             && string.Equals(Joins[i - 1], "or", StringComparison.OrdinalIgnoreCase);

                terms.Add((Words[i].Trim(), terms.Count > 0 && joiner));
            }

            return terms;
        }
    }

    /// <summary>
    /// Whether this condition is satisfied by the words of a filename.
    /// </summary>
    public bool Holds(IReadOnlyCollection<string> words, WordMatch match)
    {
        // Folded left to right, with no precedence: "a and b or c" is read
        // as "(a and b) or c". Giving "and" the tighter grip would be the
        // arithmetic convention, but nobody writing three dropdowns is
        // thinking in boolean algebra — they are reading a sentence.
        var terms = Terms;
        if (terms.Count == 0) return true;

        var found = words.Contains(terms[0].Word, match);

        for (int i = 1; i < terms.Count; i++)
        {
            var present = words.Contains(terms[i].Word, match);
            found = terms[i].Or ? found || present : found && present;
        }

        return RuleActions.Is(Action, RuleActions.IfNotFound) ? !found : found;
    }
}

/// <summary>
/// "When the name says this, do that to it."
/// </summary>
/// <remarks>
/// <para>
/// One word list cannot say how its words relate. A rule is where that is
/// written, as lines: the conditions it asks about the name, and what to do
/// when they all hold — put words in a given order, delete them, or swap one
/// for another.
/// </para>
/// <para>
/// A word in an ordering that is not in the filename is added — which is the
/// point of it. A clip named after a team can be made to say which sport and
/// which league it belongs to without either word ever having been typed
/// into the name.
/// </para>
/// </remarks>
public sealed class RenameRule
{
    /// <summary>
    /// What the person who wrote it calls it, or empty.
    /// </summary>
    /// <remarks>
    /// Free text, and never read by the renamer. A list of rules read back
    /// as sentences is accurate and still hard to find anything in once it
    /// is twenty long; a name is how somebody points at the one they meant.
    /// </remarks>
    public string Name { get; set; } = "";

    /// <summary>The rule, as lines read top to bottom.</summary>
    public List<RuleLine> Lines { get; set; } = new();

    /// <summary>
    /// The shape rules were written in before there were lines.
    /// </summary>
    /// <remarks>
    /// Still settable, and still what the tests are written against, because
    /// a saved rule in somebody's settings file is in this shape and must
    /// keep working. <see cref="Upgrade"/> turns them into lines; nothing
    /// below this point reads them.
    /// </remarks>
    public List<string> If { get; set; } = new();
    public List<string> Joins { get; set; } = new();
    public List<string> Then { get; set; } = new();
    public List<string> Else { get; set; } = new();

    /// <summary>Just the words, without the gaps left by unused slots.</summary>
    public static IReadOnlyList<string> Used(IEnumerable<string>? slots) =>
        (slots ?? Array.Empty<string>())
        .Where(w => !string.IsNullOrWhiteSpace(w))
        .Select(w => w.Trim())
        .ToList();

    private IEnumerable<RuleLine> Conditionals =>
        Lines.Where(l => RuleActions.IsCondition(l.Action) && l.Terms.Count > 0);

    private IEnumerable<RuleLine> Doings =>
        Lines.Where(l => RuleActions.IsDoing(l.Action)
                         && (l.Used.Count > 0 || RuleActions.Wordless(l.Action)));

    /// <summary>Every word the conditions look for, in the order written.</summary>
    public IReadOnlyList<string> Conditions =>
        Conditionals.SelectMany(l => l.Used).ToList();

    /// <summary>
    /// Whether this rule says anything. A condition with nothing behind it
    /// is a sentence that stops halfway, and so is an action with nothing to
    /// say when it applies.
    /// </summary>
    public bool IsUsable => Conditionals.Any() && Doings.Any();

    /// <summary>
    /// Whether every condition in this rule holds. Several conditions are
    /// read as all of them having to be true; "or" belongs inside one line.
    /// </summary>
    public bool Holds(IReadOnlyCollection<string> words, WordMatch match) =>
        IsUsable && Conditionals.All(l => l.Holds(words, match));

    /// <summary>
    /// The first ordering this rule gives, or null if it gives none.
    /// </summary>
    public IReadOnlyList<string>? Order =>
        Doings.FirstOrDefault(l => RuleActions.Is(l.Action, RuleActions.Order))?.Used;

    /// <summary>
    /// Carries out the deletions and the swaps, in the order they are
    /// written.
    /// </summary>
    /// <remarks>
    /// A "change these" line is paired with the next "change to" line below
    /// it, slot against slot: the word in the first box becomes the word in
    /// the first box underneath. Pairing by position rather than by
    /// compacting both lists is what makes the two rows line up on screen
    /// mean what they look like they mean.
    /// </remarks>
    public void Edit(List<string> words, WordMatch match)
    {
        for (int i = 0; i < Lines.Count; i++)
        {
            var line = Lines[i];

            if (RuleActions.Is(line.Action, RuleActions.Delete))
            {
                foreach (var word in line.Used)
                    Strike(words, word, match);
            }
            else if (RuleActions.Is(line.Action, RuleActions.DeleteFound))
            {
                // Every word this rule was looking for, struck out of the
                // name it was found in. Only the "found" conditions: a
                // "NOT found" one names words that are, by the fact the
                // rule applied at all, not there to delete.
                foreach (var word in Lines
                             .Where(l => RuleActions.Is(l.Action, RuleActions.IfFound))
                             .SelectMany(l => l.Used))
                {
                    Strike(words, word, match);
                }
            }
            else if (RuleActions.Is(line.Action, RuleActions.Change))
            {
                var into = Lines.Skip(i + 1)
                    .FirstOrDefault(l => RuleActions.Is(l.Action, RuleActions.ChangeTo));

                if (into is null) continue;

                for (int slot = 0; slot < 3; slot++)
                {
                    var from = line.Slot(slot);
                    var to = into.Slot(slot);

                    // A blank on either side is a pair that was never
                    // finished. Treating it as a deletion would be a second
                    // way to spell "delete these", and a surprising one.
                    if (from is null || to is null) continue;

                    // The replacement is written as the rule spells it, not
                    // as the filename did: swapping "hilites" for
                    // "highlights" should give the word in the rule, even
                    // when what was found was "hiliting".
                    for (int w = 0; w < words.Count; w++)
                        if (match.Equals(words[w], from)) words[w] = to;
                }
            }
        }
    }

    /// <summary>
    /// Deletes a word from the name, wherever the cleanup put it.
    /// </summary>
    /// <remarks>
    /// Including inside a joined pair. The cleanup reads a single
    /// underscore between two words as a first_last name, and it cannot
    /// always be right — "ab_clip" is two words to the person who named
    /// the file. The conditions see the original and say the word is
    /// there; a deletion that then cannot find it in the welded word
    /// would fire and change nothing, silently. So a pair gives up the
    /// half that matches and the other half carries on as a word of its
    /// own.
    /// </remarks>
    private static void Strike(List<string> words, string word, WordMatch match)
    {
        for (int i = words.Count - 1; i >= 0; i--)
        {
            if (match.Equals(words[i], word)) { words.RemoveAt(i); continue; }

            if (!words[i].Contains('_')) continue;

            var halves = words[i].Split('_');
            if (!halves.Any(half => match.Equals(half, word))) continue;

            var kept = halves.Where(half => !match.Equals(half, word)).ToList();

            if (kept.Count == 0) words.RemoveAt(i);
            else words[i] = string.Join("_", kept);
        }
    }

    /// <summary>
    /// The rule read back as a sentence.
    /// </summary>
    /// <remarks>
    /// Built from the lines rather than written once per action, so a line
    /// added to the dropdown and forgotten here still gets said — badly,
    /// perhaps, but said.
    ///
    /// Here rather than in the window because two places show it now: the
    /// list of rules already written, and the tester.
    /// </remarks>
    public string Description
    {
        get
        {
            if (Conditions.Count == 0) return "This rule has no word to look for yet.";

            var said = new List<string>();

            for (int i = 0; i < Lines.Count; i++)
            {
                var line = Lines[i];
                var words = line.Used;

                if (RuleActions.Wordless(line.Action))
                {
                    said.Add("delete the words it looked for");
                    continue;
                }

                if (words.Count == 0) continue;

                if (RuleActions.IsCondition(line.Action))
                {
                    // Read back with the joiners that were actually chosen: a
                    // rule saying "or" and described as "and" is worse than no
                    // description at all.
                    var terms = line.Terms;
                    var condition = terms[0].Word;

                    for (int t = 1; t < terms.Count; t++)
                        condition += (terms[t].Or ? " or " : " and ") + terms[t].Word;

                    var verb = terms.Count == 1 ? " is " : " are ";
                    var how = RuleActions.Is(line.Action, RuleActions.IfNotFound)
                        ? "not found" : "found";

                    said.Add((said.Count == 0 ? "If " : "and ") + condition + verb + how);
                }
                else if (RuleActions.Is(line.Action, RuleActions.Order))
                {
                    said.Add("lead with " + string.Join("-", words));
                }
                else if (RuleActions.Is(line.Action, RuleActions.Delete))
                {
                    said.Add("delete " + string.Join(", ", words));
                }
                else if (RuleActions.Is(line.Action, RuleActions.Change))
                {
                    var into = Lines.Skip(i + 1)
                        .FirstOrDefault(l => RuleActions.Is(l.Action, RuleActions.ChangeTo));

                    var pairs = new List<string>();
                    for (int slot = 0; slot < 3; slot++)
                    {
                        var from = line.Slot(slot);
                        var to = into?.Slot(slot);
                        if (from is not null && to is not null) pairs.Add($"{from} to {to}");
                    }

                    if (pairs.Count > 0) said.Add("change " + string.Join(", ", pairs));
                }
            }

            return string.Join(", ", said) + ".";
        }
    }

    /// <summary>
    /// Brings rules written in the old shape up to lines.
    /// </summary>
    /// <remarks>
    /// A rule carrying an "otherwise" ordering becomes two rules, because
    /// the otherwise is now said as its own rule asking the opposite
    /// question. One rule cannot hold both halves: the conditions of a rule
    /// all have to hold at once, and "found" and "not found" never do.
    /// </remarks>
    public static List<RenameRule> Upgrade(IEnumerable<RenameRule>? rules)
    {
        var upgraded = new List<RenameRule>();

        foreach (var rule in rules ?? Array.Empty<RenameRule>())
        {
            if (rule.Lines.Count > 0) { upgraded.Add(rule); continue; }

            if (Used(rule.Then).Count > 0)
            {
                upgraded.Add(new RenameRule
                {
                    Lines =
                    {
                        new RuleLine(RuleActions.IfFound, rule.If, rule.Joins),
                        new RuleLine(RuleActions.Order, rule.Then)
                    }
                });
            }

            if (Used(rule.Else).Count > 0)
            {
                upgraded.Add(new RenameRule
                {
                    Lines =
                    {
                        new RuleLine(RuleActions.IfNotFound, rule.If, rule.Joins),
                        new RuleLine(RuleActions.Order, rule.Else)
                    }
                });
            }
        }

        return upgraded;
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

    /// <summary>
    /// The rules, always as lines.
    /// </summary>
    /// <remarks>
    /// Brought up to the current shape on the way in rather than at every
    /// place that reads them, so a rule saved before lines existed behaves
    /// the same as one written today and nothing downstream has to know
    /// there were ever two shapes.
    /// </remarks>
    public IReadOnlyList<RenameRule> Rules
    {
        get => _rules;
        init => _rules = RenameRule.Upgrade(value);
    }

    private readonly IReadOnlyList<RenameRule> _rules = Array.Empty<RenameRule>();

    /// <summary>
    /// How closely a word in a list has to match one in a filename.
    /// </summary>
    /// <remarks>
    /// Variant by default, because the alternative is typing wave,
    /// waves, waving and waved into a box four times and still missing
    /// one. Precise is there for anyone who hits a pair of words it folds
    /// together wrongly; Irregular is there for the verbs no rule can
    /// reach. <see cref="WordMatch"/> says where the edges of each are.
    /// </remarks>
    public WordMatchMode MatchMode { get; init; } = WordMatchMode.Variant;

    /// <summary>
    /// Whether the words are regular expressions rather than words.
    /// </summary>
    /// <remarks>
    /// When they are, the mode above does not apply: an expression says
    /// for itself what it will and will not match, and layering a
    /// stemmer over it would make it say something else.
    /// </remarks>
    public bool UseRegex { get; init; }

    /// <summary>How a word list word is compared against a filename word.</summary>
    public WordMatch Match => WordMatch.For(MatchMode, UseRegex);

    /// <summary>
    /// The same two settings again, for the blacklist alone.
    /// </summary>
    /// <remarks>
    /// Separate, and Precise by default, because the two lists are not
    /// the same risk. A loose match in the word list recognises a word
    /// that was not quite meant; a loose match in the blacklist deletes
    /// one. "render" reaching "rendered" is useful; reaching "rendering"
    /// when a file is called that on purpose is a word gone from the
    /// name with nothing to say it went.
    /// </remarks>
    public WordMatchMode BlacklistMatchMode { get; init; } = WordMatchMode.Precise;

    public bool BlacklistUseRegex { get; init; }

    public WordMatch BlacklistMatch => WordMatch.For(BlacklistMatchMode, BlacklistUseRegex);

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
    /// <summary>
    /// Everything a name may contain, besides the extension's dot.
    /// </summary>
    /// <remarks>
    /// No underscore. It is handled as a separator instead — see
    /// <see cref="Clean"/> — so that it behaves like a space everywhere
    /// except the one place it means something.
    /// </remarks>
    private const string Allowed = "0123456789abcdefghijklmnopqrstuvwxyz()[]-";

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

        cleaned = Restructure(cleaned, vocabulary ?? FilenameVocabulary.Empty, stem);

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
    /// The underscore is a separator too, with one exception: a single one
    /// standing between two kept characters survives, because that is the
    /// only mark a first_last name has. Everywhere else — leading,
    /// trailing, doubled, or in company with a space — it is a dash like
    /// anything else. So "_leading" loses it, "a__b" becomes "a-b", and
    /// "sarah_williams" keeps the one that means something.
    ///
    /// A name is a pair, though, and only a pair. Any underscore that did
    /// not join — dropped off the front, doubled, or the one that already
    /// made a pair — says the file writes its separators as underscores,
    /// and the next single one is read accordingly. Without this,
    /// "_pb_title" lost its leading mark and then welded what was left
    /// into one word "pb_title" that no word list would ever find.
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

        // Whether an underscore here could still be a name-join. Cleared
        // by any underscore that was not one — a leading one that was
        // dropped, a doubled one, one riding with a space — and by a join
        // itself, since a name is two words and a chain of them is a
        // separator style, not a longer name.
        var joinable = true;

        for (int i = 0; i < plain.Length; i++)
        {
            if (Allowed.Contains(plain[i])) { kept.Append(plain[i]); continue; }

            // The whole run of unwanted characters, as one separator.
            int end = i;
            while (end < plain.Length && !Allowed.Contains(plain[end])) end++;

            var dividing = kept.Length > 0 && end < plain.Length;
            var underscored = plain.IndexOf('_', i, end - i) >= 0;

            if (dividing)
            {
                var joins = joinable && end - i == 1 && plain[i] == '_';
                kept.Append(joins ? '_' : '-');
                joinable = !joins && !underscored;
            }
            else if (underscored)
            {
                // Dropped, but still evidence: a name that opens with an
                // underscore writes its separators as underscores, and
                // the next single one is not half of anybody's name.
                joinable = false;
            }

            i = end - 1;
        }

        return Squeeze(kept.ToString()).Trim('-', '_');
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
    /// The words of a name as it was typed, before anything touches it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// What the rule conditions are asked about. Split at every character
    /// that is not a letter or digit, so whatever the file used for
    /// separators — the underscores "_ab_clip" is fenced with among
    /// them — cannot weld two of its words into one that no word list
    /// would ever hold.
    /// </para>
    /// <para>
    /// A first_last pair is added whole as well as in halves, so a
    /// condition may name the person or either of their names and be
    /// right every time.
    /// </para>
    /// </remarks>
    internal static List<string> OriginalWords(string stem)
    {
        var plain = Transliterate(stem.ToLowerInvariant());
        var words = new List<string>();
        string? previous = null;

        for (int i = 0; i < plain.Length; i++)
        {
            if (!char.IsLetterOrDigit(plain[i])) continue;

            int start = i;
            while (i < plain.Length && char.IsLetterOrDigit(plain[i])) i++;
            var word = plain[start..i];

            // Joined to the word before by exactly one underscore: the
            // pair goes in too.
            if (previous is not null && plain[start - 1] == '_'
                && start >= 2 && char.IsLetterOrDigit(plain[start - 2]))
            {
                words.Add(previous + "_" + word);
            }

            words.Add(word);
            previous = word;
        }

        return words;
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
    internal static string Restructure(string cleaned, FilenameVocabulary vocabulary,
                                       string? original = null)
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

        // The conditions are asked about the name as it was typed, so
        // neither the cleanup nor the blacklist can make a word invisible
        // to the rules. The cleanup, because its judgement calls — which
        // underscore is a name and which a separator — could weld two of
        // the original's words into one that no list would ever hold, and
        // a rule has to be able to ask about the name its author was
        // looking at. The blacklist, because "if temp is NOT found" is
        // otherwise always true for anybody who also blacklisted "temp" —
        // which is exactly the person who would write that rule — and it
        // is true silently, which is the worst way for a rule to be wrong.
        var asFound = original is null ? new List<string>(words) : OriginalWords(original);
        var match = vocabulary.Match;

        // The rules first, all of them. Every rule that holds gets to
        // delete and to swap, top to bottom, each seeing what the one
        // above it left behind. The ordering is the exception: the first
        // rule that gives one decides, because a name can only lead one
        // way and the rule written first is the one meant to win. Edits
        // are applied to both lists so a swap the rule above made is what
        // the rule below asks about.
        IReadOnlyList<string>? lead = null;

        foreach (var rule in vocabulary.Rules)
        {
            if (!rule.Holds(asFound, match)) continue;

            rule.Edit(words, match);
            rule.Edit(asFound, match);
            lead ??= rule.Order;
        }

        // Then the general scrub, once the rules have all had their say.
        // On its own terms, not the word list's: see
        // FilenameVocabulary.BlacklistMatch. Before the ordering and the
        // slots below, so a blacklisted word cannot take a slot or push a
        // real word into the description.
        if (vocabulary.Blacklist.Count > 0)
            words.RemoveAll(w => vocabulary.Blacklist.Contains(w, vocabulary.BlacklistMatch));

        var leading = new List<string>();

        if (lead is not null)
        {
            // Listed once, at the front, in the order the rule gives. A word
            // the rule names but the name does not have is added: that is
            // what lets a clip say which sport it belongs to without the
            // word ever having been typed into it.
            // Taken out wherever it was found and put back as the rule
            // spells it, so a name carrying "waves" leads with the "wave"
            // the rule names rather than with both.
            foreach (var word in lead)
            {
                words.RemoveAll(w => match.Equals(w, word));
                if (!leading.Contains(word, match)) leading.Add(word);
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
