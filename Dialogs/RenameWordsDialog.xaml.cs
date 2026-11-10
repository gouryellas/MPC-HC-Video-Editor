using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using MpcHcVideoEditor.Services;

namespace MpcHcVideoEditor.Dialogs;

/// <summary>
/// One slot in a rule — a word, or nothing.
/// </summary>
/// <remarks>
/// Its own object so each dropdown has something stable to bind to. The
/// alternative, binding to an index into a list, means every slot in a rule
/// is notified whenever any of them changes.
/// </remarks>
public sealed class RuleSlot : INotifyPropertyChanged
{
    public RuleSlot(string word, Action changed)
    {
        _word = word;
        _changed = changed;
    }

    private readonly Action _changed;
    private string _word;

    /// <summary>
    /// The chosen word. The blank entry at the top of every list comes back
    /// as null, which is how a slot is emptied again.
    /// </summary>
    public string? Word
    {
        get => _word.Length == 0 ? null : _word;
        set
        {
            _word = value ?? "";
            OnPropertyChanged();
            _changed();
        }
    }

    /// <summary>Choices, edited in place; see RenameWordsDialog.Sync.</summary>
    public ObservableCollection<string> Choices { get; } = new();

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

/// <summary>One rule, as the window edits it.</summary>
public sealed class RenameRuleRow : INotifyPropertyChanged
{
    public RenameRuleRow(RenameRule rule, Action changed)
    {
        Rule = rule;

        If = Slots(rule.If, changed);
        Then = Slots(rule.Then, changed);
        Else = Slots(rule.Else, changed);
    }

    public RenameRule Rule { get; }

    /// <summary>
    /// Three slots each, always — a rule of one word is a rule of three with
    /// two left empty, and a fixed row is far easier to read down a list of
    /// rules than one that changes width with its contents.
    /// </summary>
    public IReadOnlyList<RuleSlot> If { get; }
    public IReadOnlyList<RuleSlot> Then { get; }
    public IReadOnlyList<RuleSlot> Else { get; }

    private static IReadOnlyList<RuleSlot> Slots(List<string> from, Action changed)
    {
        var words = RenameRule.Used(from);

        return Enumerable.Range(0, 3)
            .Select(i => new RuleSlot(i < words.Count ? words[i] : "", changed))
            .ToList();
    }

    /// <summary>Writes the slots back into the rule they came from.</summary>
    public RenameRule Collect()
    {
        Rule.If = If.Select(s => s.Word ?? "").ToList();
        Rule.Then = Then.Select(s => s.Word ?? "").ToList();
        Rule.Else = Else.Select(s => s.Word ?? "").ToList();

        return Rule;
    }

    /// <summary>The rule in words, for the heading above each row.</summary>
    public string Summary
    {
        get
        {
            var ifs = If.Select(s => s.Word).Where(w => w is not null).ToList();
            if (ifs.Count == 0) return "If … is found";

            return "If " + string.Join(" and ", ifs) + " is found";
        }
    }

    public void Refreshed() => OnPropertyChanged(nameof(Summary));

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

/// <summary>
/// The words the bulk renamer notices, and the rules that order them.
/// </summary>
/// <remarks>
/// Two boxes of text and a list of rules. There were four boxes — categories,
/// subjects, conditionals, blacklist — and the first three turned out to be
/// the same thing said three times: which section a word belongs to is not a
/// property of the word, since the same word can lead one name and trail
/// another. The rules say that instead, and the list just says which words
/// are worth noticing.
/// </remarks>
public partial class RenameWordsDialog : Window
{
    /// <summary>
    /// What the blank entry at the top of every dropdown is called.
    /// </summary>
    /// <remarks>
    /// A real entry rather than an absence, because a slot has to be
    /// emptiable: without one there is no way to take a word back out of a
    /// rule once it has been put in.
    /// </remarks>
    private const string NoWord = "—";

    private readonly SettingsService _settings;
    private readonly ObservableCollection<RenameRuleRow> _rules = new();

    public RenameWordsDialog(SettingsService settings)
    {
        InitializeComponent();

        _settings = settings;
        RuleList.ItemsSource = _rules;

        WordsBox.Text = string.Join(Environment.NewLine, Merged(settings));
        BlacklistBox.Text = string.Join(Environment.NewLine, settings.Current.RenameBlacklist);

        foreach (var rule in settings.Current.RenameRules)
            _rules.Add(new RenameRuleRow(Copy(rule), RuleChanged));

        RefreshChoices();
    }

    /// <summary>
    /// The word list, with anything left in the three lists it replaced.
    /// </summary>
    /// <remarks>
    /// Categories, subjects and conditionals were folded into one list.
    /// Dropping what somebody had already typed into them because the shape
    /// changed underneath would be the wrong way to make that change.
    /// </remarks>
    private static List<string> Merged(SettingsService settings) =>
        settings.Current.RenameWords
            .Concat(settings.Current.RenameCategories)
            .Concat(settings.Current.RenameSubjects)
            .Concat(settings.Current.RenameConditionals)
            .Where(w => !string.IsNullOrWhiteSpace(w))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(w => w, StringComparer.OrdinalIgnoreCase)
            .ToList();

    private static RenameRule Copy(RenameRule rule) => new()
    {
        If = rule.If.ToList(),
        Then = rule.Then.ToList(),
        Else = rule.Else.ToList()
    };

    /// <summary>
    /// Splits a box into words, cleaned the same way a filename is.
    /// </summary>
    /// <remarks>
    /// Through the filename rules, because these are matched against words
    /// taken out of a cleaned filename — a word typed as "Wedding Party"
    /// would otherwise never match anything, and would fail silently, which
    /// is the worst way for a word list to be wrong.
    /// </remarks>
    private static List<string> Words(string? text) =>
        (text ?? "")
        .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
        .Select(line => FilenameRules.Clean(line.Trim()))
        .Where(word => word.Length > 0)
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .OrderBy(word => word, StringComparer.OrdinalIgnoreCase)
        .ToList();

    private void Words_Changed(object sender, TextChangedEventArgs e) => RefreshChoices();

    private void RuleChanged()
    {
        foreach (var row in _rules) row.Refreshed();
    }

    /// <summary>
    /// Hands every slot the current word list.
    /// </summary>
    private void RefreshChoices()
    {
        var words = Words(WordsBox.Text);

        foreach (var row in _rules)
            foreach (var slot in row.If.Concat(row.Then).Concat(row.Else))
            {
                var choices = new List<string> { NoWord };
                choices.AddRange(words);

                // A word already chosen but since deleted from the list stays
                // available, so a rule does not quietly empty itself while
                // somebody is mid-edit in the box above.
                if (slot.Word is { } chosen && !words.Contains(chosen, StringComparer.OrdinalIgnoreCase))
                    choices.Add(chosen);

                Sync(slot.Choices, choices);
            }

        NoRulesText.Visibility = _rules.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        RuleChanged();
    }

    /// <summary>
    /// Brings a collection to match a list without replacing it.
    /// </summary>
    /// <remarks>
    /// Handing a ComboBox a new ItemsSource clears its SelectedItem, and a
    /// two-way binding then writes that null straight back into the rule — so
    /// every rule emptied itself the moment anything was typed in a word box.
    /// Editing the contents leaves the selection alone.
    /// </remarks>
    private static void Sync(ObservableCollection<string> live, IReadOnlyList<string> wanted)
    {
        for (int i = live.Count - 1; i >= 0; i--)
            if (!wanted.Contains(live[i], StringComparer.Ordinal)) live.RemoveAt(i);

        for (int i = 0; i < wanted.Count; i++)
            if (!live.Contains(wanted[i], StringComparer.Ordinal)) live.Insert(i, wanted[i]);
    }

    private void AddRule_Click(object sender, RoutedEventArgs e)
    {
        _rules.Add(new RenameRuleRow(new RenameRule(), RuleChanged));
        RefreshChoices();
    }

    private void RemoveRule_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: RenameRuleRow row }) _rules.Remove(row);
        RefreshChoices();
    }

    /// <summary>
    /// Runs one rule against made-up names, using the window as it stands.
    /// </summary>
    private void TestRule_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: RenameRuleRow row }) return;

        var rule = row.Collect();

        var vocabulary = new FilenameVocabulary
        {
            Words = Words(WordsBox.Text),
            Blacklist = Words(BlacklistBox.Text),
            Rules = _rules.Select(r => r.Collect()).Where(r => r.IsUsable).ToList()
        };

        new RuleTestDialog(rule, vocabulary) { Owner = this }.ShowDialog();
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        _settings.Current.RenameWords = Words(WordsBox.Text);
        _settings.Current.RenameBlacklist = Words(BlacklistBox.Text);

        // Folded in, so they are not read again.
        _settings.Current.RenameCategories = new List<string>();
        _settings.Current.RenameSubjects = new List<string>();
        _settings.Current.RenameConditionals = new List<string>();

        // Half-finished rules are dropped rather than saved and ignored: a
        // rule that says nothing would sit in the list looking like it did
        // something.
        _settings.Current.RenameRules = _rules
            .Select(r => r.Collect())
            .Where(r => r.IsUsable)
            .ToList();

        _settings.Save();

        DialogResult = true;
        Close();
    }
}
