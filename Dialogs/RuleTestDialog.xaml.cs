using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Media;
using MpcHcVideoEditor.Services;

namespace MpcHcVideoEditor.Dialogs;

/// <summary>One example and what the rules make of it.</summary>
public sealed class RuleTestRow
{
    public RuleTestRow(string original, string result, bool fired)
    {
        Original = original;
        Result = result;

        // Green where the rule being tested actually did something, muted
        // where the name merely got tidied — so a rule that never fires is
        // obvious rather than looking like it half-worked.
        ResultBrush = fired
            ? Application.Current?.TryFindResource("ValueGreen") as Brush ?? Brushes.Gray
            : Application.Current?.TryFindResource("TextMuted") as Brush ?? Brushes.Gray;
    }

    public string Original { get; }
    public string Result { get; }
    public Brush ResultBrush { get; }
}

/// <summary>
/// Shows what a rule does, before a folder is renamed by it.
/// </summary>
/// <remarks>
/// A rule is three dropdowns and a sentence, and the sentence does not say
/// what the result looks like — whether the supplied words land in front,
/// what happens to the trigger word, where the rest of the name ends up.
/// This answers that by running it.
///
/// The whole vocabulary is used, not the one rule in isolation: what matters
/// is what the renamer will actually do, and that depends on the other lists
/// as well. The rule under test is only used to decide which examples to
/// build and whether to call the result a hit.
/// </remarks>
public partial class RuleTestDialog : Window
{
    private readonly FilenameVocabulary _vocabulary;
    private readonly RenameRule _rule;
    private readonly ObservableCollection<RuleTestRow> _rows = new();

    public RuleTestDialog(RenameRule rule, FilenameVocabulary vocabulary)
    {
        InitializeComponent();

        _rule = rule;
        _vocabulary = vocabulary;
        ResultList.ItemsSource = _rows;

        RuleText.Text = Describe(rule);

        foreach (var example in Examples(rule.Conditions.FirstOrDefault() ?? ""))
            _rows.Add(Run(example));
    }

    private static string Describe(RenameRule rule)
    {
        if (rule.Conditions.Count == 0) return "This rule has no word to look for yet.";

        var said = $"If {string.Join(" and ", rule.Conditions)} " +
                   (rule.Conditions.Count == 1 ? "is" : "are") + " found";

        if (rule.ThenOrder.Count > 0)
            said += $", lead with {string.Join("-", rule.ThenOrder)}";

        if (rule.ElseOrder.Count > 0)
            said += $"; otherwise lead with {string.Join("-", rule.ElseOrder)}";

        return said + ".";
    }

    /// <summary>
    /// Five names of the shapes files actually arrive in.
    /// </summary>
    /// <remarks>
    /// Each one exercises something different: plain, capitals and
    /// punctuation, a bracketed tag, a name already joined, and a name with
    /// the trigger word buried rather than leading. The last one is the
    /// interesting case — it is where a rule and the slot ordering can
    /// disagree.
    ///
    /// Made up on the spot rather than taken from the user's folder, which
    /// this window has no business reading.
    /// </remarks>
    private static IReadOnlyList<string> Examples(string word)
    {
        var w = word.Length > 0 ? word : "word";

        return new[]
        {
            $"{w} highlights.mp4",
            $"{w.ToUpperInvariant()}  season  opener!!.mkv",
            $"{w} rematch [take2].mp4",
            $"sarah_williams {w} interview.mp4",
            $"2024 season finale {w}.mp4"
        };
    }

    private RuleTestRow Run(string original)
    {
        var result = FilenameRules.Apply(original, _vocabulary);

        // "Fired" means the rule put something there that the plain rules
        // would not have — which is the only question this window is asked.
        var without = FilenameRules.Apply(original, new FilenameVocabulary
        {
            Words = _vocabulary.Words,
            Blacklist = _vocabulary.Blacklist,
            Rules = _vocabulary.Rules.Where(r => !ReferenceEquals(r, _rule)).ToList()
        });

        return new RuleTestRow(original, result, !string.Equals(result, without, StringComparison.Ordinal));
    }

    private void Own_Changed(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        // The typed name sits at the top, replacing the previous attempt
        // rather than piling up.
        var typed = OwnBox.Text?.Trim() ?? "";

        while (_rows.Count > 5) _rows.RemoveAt(0);
        if (typed.Length == 0) return;

        _rows.Insert(0, Run(typed));
    }
}
