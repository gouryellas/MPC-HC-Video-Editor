using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using MpcHcVideoEditor.Services;

namespace MpcHcVideoEditor.Dialogs;

/// <summary>
/// One rule, with the word lists it can choose from.
/// </summary>
/// <remarks>
/// The lists live on the row rather than on the window because each dropdown
/// binds to its own row. They are replaced wholesale whenever a word box is
/// edited, so a word typed into Categories can be picked in a rule without
/// saving and reopening.
/// </remarks>
public sealed class RenameRuleRow : INotifyPropertyChanged
{
    public RenameRuleRow(RenameRule rule) => Rule = rule;

    public RenameRule Rule { get; }

    public string Word
    {
        get => Rule.Word;
        set { Rule.Word = value ?? ""; OnPropertyChanged(); }
    }

    public string Category
    {
        get => Rule.Category;
        set { Rule.Category = value ?? ""; OnPropertyChanged(); }
    }

    public string Subject
    {
        get => Rule.Subject;
        set { Rule.Subject = value ?? ""; OnPropertyChanged(); }
    }

    /// <summary>
    /// The choices each dropdown offers.
    /// </summary>
    /// <remarks>
    /// Collections that are filled and emptied in place, never replaced.
    /// Handing a ComboBox a new ItemsSource clears its SelectedItem, and a
    /// two-way binding then writes that null straight back into the rule —
    /// so every rule emptied itself the moment anything was typed in a word
    /// box. Editing the contents leaves the selection alone.
    /// </remarks>
    public ObservableCollection<string> Conditionals { get; } = new();
    public ObservableCollection<string> Categories { get; } = new();
    public ObservableCollection<string> Subjects { get; } = new();

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

/// <summary>
/// The words the bulk renamer recognises, and the rules built from them.
/// </summary>
/// <remarks>
/// Four boxes of text rather than four managed lists. The content is a
/// handful of words a person keeps in their head anyway, and a text box can
/// be pasted into, sorted in an editor and read at a glance — none of which
/// a grid of rows with add and remove buttons does better.
///
/// The rules are the exception, and have to be rows: each one is three
/// choices made from those lists, and typing it as text would mean inventing
/// a syntax and then explaining it.
/// </remarks>
public partial class RenameWordsDialog : Window
{
    private readonly SettingsService _settings;
    private readonly ObservableCollection<RenameRuleRow> _rules = new();

    public RenameWordsDialog(SettingsService settings)
    {
        InitializeComponent();

        _settings = settings;
        RuleList.ItemsSource = _rules;

        CategoriesBox.Text = string.Join(Environment.NewLine, settings.Current.RenameCategories);
        SubjectsBox.Text = string.Join(Environment.NewLine, settings.Current.RenameSubjects);
        ConditionalsBox.Text = string.Join(Environment.NewLine, settings.Current.RenameConditionals);
        BlacklistBox.Text = string.Join(Environment.NewLine, settings.Current.RenameBlacklist);

        foreach (var rule in settings.Current.RenameRules)
            _rules.Add(new RenameRuleRow(new RenameRule
            {
                Word = rule.Word,
                Category = rule.Category,
                Subject = rule.Subject
            }));

        RefreshChoices();
    }

    /// <summary>
    /// Splits a box into words, cleaned the same way a filename is.
    /// </summary>
    /// <remarks>
    /// Through the filename rules, because these are matched against words
    /// taken out of a cleaned filename — a category typed as "Wedding" or
    /// "wedding party" would otherwise never match anything, and would fail
    /// silently, which is the worst way for a word list to be wrong.
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

    /// <summary>
    /// Hands every rule the current contents of the three lists it draws on.
    /// </summary>
    /// <remarks>
    /// A choice already made is kept even when its word has just been typed
    /// out of the list above — the alternative is a rule silently emptying
    /// itself because somebody was mid-edit in another box.
    /// </remarks>
    private void RefreshChoices()
    {
        var conditionals = Words(ConditionalsBox.Text);
        var categories = Words(CategoriesBox.Text);
        var subjects = Words(SubjectsBox.Text);

        foreach (var row in _rules)
        {
            Sync(row.Conditionals, Keep(conditionals, row.Word));
            Sync(row.Categories, Keep(categories, row.Category));
            Sync(row.Subjects, Keep(subjects, row.Subject));
        }

        NoRulesText.Visibility = _rules.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <summary>
    /// Brings a collection to match a list without replacing it.
    /// </summary>
    /// <remarks>
    /// Removals first, then insertions at the right place, so a selected
    /// item that is staying is never removed and re-added — which would
    /// clear the selection just as surely as replacing the whole list.
    /// </remarks>
    private static void Sync(ObservableCollection<string> live, IReadOnlyList<string> wanted)
    {
        for (int i = live.Count - 1; i >= 0; i--)
            if (!wanted.Contains(live[i], StringComparer.Ordinal)) live.RemoveAt(i);

        for (int i = 0; i < wanted.Count; i++)
            if (!live.Contains(wanted[i], StringComparer.Ordinal)) live.Insert(i, wanted[i]);
    }

    /// <summary>The list, plus a choice already made that is not in it.</summary>
    private static IReadOnlyList<string> Keep(List<string> words, string chosen)
    {
        if (chosen.Length == 0 || words.Contains(chosen, StringComparer.OrdinalIgnoreCase))
            return words;

        return words.Concat(new[] { chosen }).ToList();
    }

    private void AddRule_Click(object sender, RoutedEventArgs e)
    {
        _rules.Add(new RenameRuleRow(new RenameRule()));
        RefreshChoices();
    }

    /// <summary>
    /// Runs one rule against made-up names, using the window as it stands.
    /// </summary>
    /// <remarks>
    /// Built from the boxes rather than from the saved settings, so a rule
    /// can be tried before it is committed — which is the only time the
    /// answer is worth having.
    /// </remarks>
    private void TestRule_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: RenameRuleRow row }) return;

        var vocabulary = new FilenameVocabulary
        {
            Categories = Words(CategoriesBox.Text),
            Subjects = Words(SubjectsBox.Text),
            Conditionals = Words(ConditionalsBox.Text),
            Blacklist = Words(BlacklistBox.Text),
            Rules = _rules.Select(r => r.Rule).Where(r => r.IsUsable).ToList()
        };

        new RuleTestDialog(row.Rule, vocabulary) { Owner = this }.ShowDialog();
    }

    private void RemoveRule_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: RenameRuleRow row }) _rules.Remove(row);
        RefreshChoices();
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        _settings.Current.RenameCategories = Words(CategoriesBox.Text);
        _settings.Current.RenameSubjects = Words(SubjectsBox.Text);
        _settings.Current.RenameConditionals = Words(ConditionalsBox.Text);
        _settings.Current.RenameBlacklist = Words(BlacklistBox.Text);

        // Half-finished rules are dropped rather than saved and ignored: a
        // rule that says nothing would sit in the list looking like it did
        // something.
        _settings.Current.RenameRules = _rules
            .Select(r => r.Rule)
            .Where(r => r.IsUsable)
            .ToList();

        _settings.Save();

        DialogResult = true;
        Close();
    }
}
