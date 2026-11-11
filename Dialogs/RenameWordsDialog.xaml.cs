using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using MpcHcVideoEditor.Services;

namespace MpcHcVideoEditor.Dialogs;

/// <summary>
/// One dropdown in a rule — a word, a joiner, an action, or nothing.
/// </summary>
/// <remarks>
/// <para>
/// Its own object so each dropdown has something stable to bind to. The
/// alternative, binding to an index into a list, means every slot in a rule
/// is notified whenever any of them changes.
/// </para>
/// <para>
/// One class for all three kinds. They differ only in what they offer to
/// choose from, and the separate classes that used to exist had drifted into
/// different ideas of what "nothing chosen" meant.
/// </para>
/// </remarks>
public sealed class RuleSlot : INotifyPropertyChanged
{
    /// <summary>
    /// Nothing chosen, as a value a dropdown can hold and be set back to.
    /// </summary>
    /// <remarks>
    /// The empty string rather than null or a placeholder character. A slot
    /// has to be emptiable — without a value meaning "nothing" there is no
    /// way to take a word back out of a rule once it has been put in — and
    /// anything printable put there instead ends up being read as a word.
    /// </remarks>
    public const string Blank = "";

    /// <summary>The three joiner choices, shared: every gap offers the same.</summary>
    public static readonly ObservableCollection<string> JoinChoices = new() { Blank, "and", "or" };

    /// <summary>
    /// What a line can do. No blank among them: a line always does
    /// something, and one that did nothing would be a row to read past
    /// rather than a row to delete.
    /// </summary>
    public static readonly ObservableCollection<string> ActionChoices = new(RuleActions.All);

    public RuleSlot(string? word, ObservableCollection<string>? choices = null)
    {
        _word = string.IsNullOrWhiteSpace(word) ? Blank : word.Trim();
        Choices = choices ?? new ObservableCollection<string> { Blank };
    }

    private string _word;

    /// <summary>The chosen word, or <see cref="Blank"/> for none.</summary>
    public string Word
    {
        get => _word;
        set
        {
            var next = string.IsNullOrWhiteSpace(value) ? Blank : value.Trim();
            if (string.Equals(_word, next, StringComparison.Ordinal)) return;

            _word = next;
            OnPropertyChanged();
        }
    }

    /// <summary>Whether anything is chosen here.</summary>
    public bool IsSet => _word.Length > 0;

    /// <summary>Choices, edited in place; see RenameWordsDialog.Sync.</summary>
    public ObservableCollection<string> Choices { get; }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

/// <summary>One line of a rule, as the window edits it.</summary>
public sealed class RuleLineRow : INotifyPropertyChanged
{
    public RuleLineRow(RuleLine line)
    {
        var action = RuleActions.All.FirstOrDefault(a => RuleActions.Is(a, line.Action))
                     ?? RuleActions.IfFound;

        Action = new RuleSlot(action, RuleSlot.ActionChoices);
        Words = Slots(line.Words, 3);
        Joins = Slots(line.Joins, 2, RuleSlot.JoinChoices);

        // What a line looks like follows from what it does: a condition has
        // joiners in its gaps, an ordering has arrows, and the rest have
        // nothing at all.
        Action.PropertyChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(IsCondition));
            OnPropertyChanged(nameof(JoinVisibility));
            OnPropertyChanged(nameof(ArrowVisibility));
        };

        // Anything at all moving in this line is worth telling the rule
        // about: which of its buttons are live depends on whether a single
        // slot anywhere in it has been filled.
        foreach (var slot in Words.Concat(Joins).Append(Action))
            slot.PropertyChanged += (_, _) => Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Raised when any slot in this line changes.</summary>
    public event EventHandler? Changed;

    public RuleSlot Action { get; }

    /// <summary>
    /// Three slots, always — a line of one word is a line of three with two
    /// left empty, and a row of fixed shape is far easier to read down a
    /// list than one that changes width with what is in it.
    /// </summary>
    public IReadOnlyList<RuleSlot> Words { get; }

    /// <summary>Two gaps, read only when the line is a condition.</summary>
    public IReadOnlyList<RuleSlot> Joins { get; }

    public bool IsCondition => RuleActions.IsCondition(Action.Word);

    public Visibility JoinVisibility =>
        IsCondition ? Visibility.Visible : Visibility.Collapsed;

    public Visibility ArrowVisibility =>
        RuleActions.Is(Action.Word, RuleActions.Order) ? Visibility.Visible : Visibility.Collapsed;

    public RuleLine Collect() =>
        new(Action.Word, Words.Select(s => s.Word), Joins.Select(s => s.Word));

    /// <summary>
    /// A fixed number of slots, by position.
    /// </summary>
    /// <remarks>
    /// Not compacted on the way in. The joiner in front of the second slot
    /// is the first joiner whether or not the first slot was filled, and a
    /// "change to" pairs with the box above it by position — closing a gap
    /// here would quietly move both onto the wrong word.
    /// </remarks>
    private static IReadOnlyList<RuleSlot> Slots(
        List<string> from, int count, ObservableCollection<string>? choices = null) =>
        Enumerable.Range(0, count)
            .Select(i => new RuleSlot(i < from.Count ? from[i] : RuleSlot.Blank, choices))
            .ToList();

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

/// <summary>
/// The one rule the window is editing.
/// </summary>
/// <remarks>
/// One of these at a time, not one per rule. A column of identical six-line
/// cards is a lot of screen saying very little — a rule, once written, is a
/// sentence, and a sentence belongs in a list. This is the box that sentence
/// is written in; everything already written sits below it as
/// <see cref="SavedRuleRow"/>.
/// </remarks>
public sealed class RenameRuleRow : INotifyPropertyChanged
{
    public RenameRuleRow(RenameRule rule)
    {
        _name = rule.Name;

        Lines.CollectionChanged += (_, e) =>
        {
            foreach (var line in e.NewItems?.OfType<RuleLineRow>() ?? Enumerable.Empty<RuleLineRow>())
                line.Changed += (_, _) => Rechecked();

            Rechecked();
        };

        // Exactly the lines the rule has, and none invented. A new rule
        // opens with nothing in it: the two lines that used to be there by
        // default were a guess at what was wanted, and a guess that has to
        // be cleared is worse than an empty box with a + on it.
        foreach (var line in rule.Lines) Lines.Add(new RuleLineRow(line));
    }

    public ObservableCollection<RuleLineRow> Lines { get; } = new();

    private string _name;

    /// <summary>What this rule is called, or empty.</summary>
    public string Name
    {
        get => _name;
        set
        {
            var next = (value ?? "").Trim();
            if (_name == next) return;

            _name = next;
            OnPropertyChanged();
            Rechecked();
        }
    }

    /// <summary>
    /// Whether any row has a word in it.
    /// </summary>
    /// <remarks>
    /// What the reset button waits for, and what says whether starting a
    /// new rule would throw anything away. The name is not counted: every
    /// rule is born with one, so counting it would make every box look
    /// worked-in from the moment it opened.
    /// </remarks>
    public bool HasInput => Lines.Any(l => l.Words.Any(s => s.IsSet));

    /// <summary>
    /// Whether the rule says enough to be run.
    /// </summary>
    /// <remarks>
    /// What the eye waits for. Pressing it on a half-written rule used to
    /// open a window to say it was half-written, which is a window to read
    /// and dismiss in place of a button that simply was not offered.
    /// </remarks>
    public bool IsTestable => Collect().IsUsable;

    /// <summary>
    /// An open eye when there is something to look at, a struck-out one
    /// when there is not.
    /// </summary>
    public string TestGlyph =>
        "pack://application:,,,/Assets/icons/"
        + (IsTestable ? "icon-eye_show.png" : "icon-eye_hide.png");

    /// <summary>
    /// What the line under the rows says.
    /// </summary>
    /// <remarks>
    /// Always there, so adding a row is always in the same place, and it
    /// doubles as the empty box's prompt rather than being a second
    /// control that appears and disappears.
    /// </remarks>
    public string AddRowText =>
        Lines.Count == 0 ? ") a row to get started." : ") another row";

    private void Rechecked()
    {
        OnPropertyChanged(nameof(HasInput));
        OnPropertyChanged(nameof(IsTestable));
        OnPropertyChanged(nameof(TestGlyph));
        OnPropertyChanged(nameof(AddRowText));
    }

    public RenameRule Collect() => new()
    {
        Name = _name,
        Lines = Lines.Select(l => l.Collect()).ToList()
    };

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

/// <summary>One row of a saved rule, read back.</summary>
/// <remarks>
/// A rule is one thing made of several rows, so the list shows the rows.
/// The sentence it used to show was accurate and flattened exactly the
/// structure that matters — which conditions are separate, what order the
/// actions happen in — into one run of commas.
/// </remarks>
public sealed class SavedLineRow
{
    public SavedLineRow(RuleLine line)
    {
        Action = line.Action;
        IsCondition = RuleActions.IsCondition(line.Action);
        Words = Read(line);
    }

    public string Action { get; }
    public string Words { get; }

    /// <summary>Drives the colour of the mark, as in the editor above.</summary>
    public bool IsCondition { get; }

    private static string Read(RuleLine line)
    {
        if (RuleActions.IsCondition(line.Action))
        {
            // With the joiners that were chosen, so a rule saying "or" does
            // not read back as "and".
            var terms = line.Terms;
            if (terms.Count == 0) return "";

            var text = terms[0].Word;
            for (int i = 1; i < terms.Count; i++)
                text += (terms[i].Or ? "  or  " : "  and  ") + terms[i].Word;

            return text;
        }

        // An ordering is a sequence and says so; everything else is a list.
        return RuleActions.Is(line.Action, RuleActions.Order)
            ? string.Join("  →  ", line.Used)
            : string.Join(",  ", line.Used);
    }
}

/// <summary>One rule that has been written, as a block in the list.</summary>
public sealed class SavedRuleRow : INotifyPropertyChanged
{
    public SavedRuleRow(RenameRule rule) => Rule = rule;

    public RenameRule Rule { get; }

    private RenameRule? _live;

    /// <summary>
    /// Shows what the box holds instead of what is saved.
    /// </summary>
    /// <remarks>
    /// So that renaming a rule in the box changes its line in the list as
    /// it is typed, rather than only once it is saved. Null puts the saved
    /// version back, which is what abandoning an edit has to do.
    /// </remarks>
    public void Preview(RenameRule? live)
    {
        _live = live;

        OnPropertyChanged(nameof(Name));
        OnPropertyChanged(nameof(Rows));
        OnPropertyChanged(nameof(NameVisibility));
    }

    private RenameRule Shown => _live ?? Rule;

    /// <summary>
    /// The rule's rows, as they read.
    /// </summary>
    /// <remarks>
    /// Rows with nothing in them are left out. They are a real state in
    /// the editor — a line added and not yet filled — but in a list of
    /// finished rules a blank row is a gap with no meaning.
    /// </remarks>
    public IReadOnlyList<SavedLineRow> Rows =>
        Shown.Lines.Where(l => l.Used.Count > 0).Select(l => new SavedLineRow(l)).ToList();

    /// <summary>What it is called, above the sentence, when it has a name.</summary>
    public string Name => Shown.Name;

    public Visibility NameVisibility =>
        Shown.Name.Length > 0 ? Visibility.Visible : Visibility.Collapsed;

    private string _number = "";

    public string Number
    {
        get => _number;
        set
        {
            if (_number == value) return;
            _number = value;
            OnPropertyChanged();
        }
    }

    private bool _editing;

    /// <summary>
    /// Whether this is the one currently open in the box above.
    /// </summary>
    /// <remarks>
    /// It stays in the list while being edited rather than being lifted out
    /// of it. Lifting it out means a rule that is nowhere if the edit is
    /// abandoned, and it also renumbers everything below for the duration.
    /// </remarks>
    public bool IsEditing
    {
        get => _editing;
        set
        {
            if (_editing == value) return;
            _editing = value;
            OnPropertyChanged();
        }
    }

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
    private readonly SettingsService _settings;
    private readonly Func<IReadOnlyList<string>>? _files;
    private readonly ObservableCollection<SavedRuleRow> _rules = new();

    /// <summary>The box, replaced outright each time it is reset.</summary>
    private RenameRuleRow _editor = new(new RenameRule());

    /// <summary>
    /// The rule in the list the box is editing, or null for a new one.
    /// </summary>
    private SavedRuleRow? _editing;

    /// <param name="files">
    /// The files a rule is tested against. Null when this window is opened
    /// from somewhere with no folder behind it, which the tester reports
    /// rather than guessing at.
    /// </param>
    public RenameWordsDialog(SettingsService settings,
                             Func<IReadOnlyList<string>>? files = null)
    {
        InitializeComponent();

        _settings = settings;
        _files = files;
        RuleList.ItemsSource = _rules;

        WordsBox.Text = string.Join(Environment.NewLine, Merged(settings));
        BlacklistBox.Text = string.Join(Environment.NewLine, settings.Current.RenameBlacklist);
        VariantsCheck.IsChecked = settings.Current.RenameMatchVariants;

        // Brought up to lines first, so a rule saved in the old shape opens
        // as the lines it means rather than as an empty rule.
        foreach (var rule in RenameRule.Upgrade(settings.Current.RenameRules))
            _rules.Add(new SavedRuleRow(rule));

        ResetBox();
        Renumber();
    }

    /// <summary>
    /// Empties the box and points it at a new rule.
    /// </summary>
    /// <remarks>
    /// A fresh object rather than the old one wiped clean. Wiping means
    /// walking every slot of every line and hoping none was missed; this
    /// cannot leave anything behind.
    /// </remarks>
    private void ResetBox(RenameRule? from = null, SavedRuleRow? editing = null)
    {
        _editing = editing;

        // A new rule is born named. That is the prepopulated "Rule #N",
        // sitting in a field that is already editable — no dialog to
        // dismiss for something that is on screen and can be typed over.
        _editor = new RenameRuleRow(from ?? new RenameRule { Name = NextName() });

        // The line in the list follows the box while that line is the one
        // being edited. Anything at all moving in the box comes through
        // here, so a name typed into the heading shows below immediately
        // rather than waiting for a save.
        _editor.PropertyChanged += (_, _) => _editing?.Preview(_editor.Collect());

        foreach (var row in _rules)
        {
            row.IsEditing = ReferenceEquals(row, editing);

            // Back to what is saved. Abandoning an edit has to put the
            // line back, not leave the list showing a name nobody kept.
            if (!row.IsEditing) row.Preview(null);
        }

        editing?.Preview(_editor.Collect());

        // On the panel that holds both the control row and the box: the
        // icons are no longer inside the box they act on.
        RuleEditor.DataContext = _editor;
        RefreshChoices();
    }

    /// <summary>The name a brand new rule arrives with.</summary>
    private string NextName() => "Rule #" + (_rules.Count + 1);

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

    /// <summary>
    /// Edited on a copy, so abandoning an edit leaves the original alone.
    /// </summary>
    /// <remarks>
    /// The name comes with it. Without that, opening a named rule for
    /// editing showed an unnamed one, and saving it put the name back to
    /// nothing — a rename by accident.
    /// </remarks>
    private static RenameRule Copy(RenameRule rule) => new()
    {
        Name = rule.Name,
        Lines = rule.Lines.Select(l => new RuleLine(l.Action, l.Words, l.Joins)).ToList()
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

    private void Words_Changed(object sender, TextChangedEventArgs e)
    {
        RefreshChoices();
        Recount();
    }

    private void Renumber()
    {
        for (int i = 0; i < _rules.Count; i++) _rules[i].Number = (i + 1).ToString();

        NoRulesText.Visibility = _rules.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        Recount();
    }

    /// <summary>
    /// Puts the count of each list in its own heading.
    /// </summary>
    /// <remarks>
    /// Rather than on a summary line of its own, where it was the same fact
    /// said away from the thing it counts. A heading reading "(0)" is the
    /// answer to why nothing is being renamed, and it is sitting on the box
    /// that needs filling in.
    /// </remarks>
    private void Recount()
    {
        if (WordsHeading is null) return;   // during InitializeComponent

        WordsHeading.Text = $"Words ({Words(WordsBox.Text).Count})";
        BlacklistHeading.Text = $"Blacklisted ({Words(BlacklistBox.Text).Count})";
        RulesHeading.Text = $"Rules ({_rules.Count})";
    }

    private static string Plural(int n, string noun) =>
        n == 1 ? $"1 {noun}" : $"{n} {noun}s";

    /// <summary>
    /// Hands every word slot the current word list, blank entry first.
    /// </summary>
    private void RefreshChoices()
    {
        var words = Words(WordsBox.Text);

        foreach (var slot in _editor.Lines.SelectMany(l => l.Words))
        {
            var choices = new List<string> { RuleSlot.Blank };
            choices.AddRange(words);

            // A word already chosen but since deleted from the list stays
            // available, so a rule does not quietly empty itself while
            // somebody is mid-edit in the box above.
            if (slot.IsSet && !words.Contains(slot.Word, StringComparer.OrdinalIgnoreCase))
                choices.Add(slot.Word);

            Sync(slot.Choices, choices);
        }
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

    private void AddLine_Click(object sender, RoutedEventArgs e)
    {
        _editor.Lines.Add(new RuleLineRow(new RuleLine(RuleActions.IfFound)));
        RefreshChoices();
    }

    /// <summary>
    /// Starts a fresh rule, keeping whatever the box already earned.
    /// </summary>
    /// <remarks>
    /// A finished rule in the box is filed rather than lost, because
    /// pressing "new" having just written one is not a request to throw it
    /// away. An unfinished one cannot be filed — it would sit in the list
    /// saying nothing — so it goes, and the status line says so rather
    /// than a dialog asking permission for a half-filled dropdown.
    /// </remarks>
    private void NewRule_Click(object sender, RoutedEventArgs e)
    {
        var rule = _editor.Collect();

        if (rule.IsUsable)
        {
            File(rule);
            ResetBox();
            Renumber();
            Write($"Saved {Named(rule)} and started a new one.");
            return;
        }

        var lost = _editor.HasInput;

        ResetBox();
        Renumber();

        Said(lost
            ? "Started a new rule. The unfinished one in the box was discarded."
            : "Started a new rule.", ok: true);
    }

    /// <summary>
    /// Takes one row out of the box, and the box out with the last of them.
    /// </summary>
    /// <remarks>
    /// Deleting the final row empties the rule of everything it said, so
    /// what is left is not a rule being written — it is a blank one still
    /// carrying a name, and still pointed at whichever saved rule it was
    /// opened from. Replaced whole rather than cleared in parts, so no
    /// slot, name or link can survive it.
    /// </remarks>
    private void RemoveLine_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: RuleLineRow line }) return;

        _editor.Lines.Remove(line);

        if (_editor.Lines.Count == 0) ResetBox();
    }

    /// <summary>
    /// Files the rule in the box and clears it for the next one.
    /// </summary>
    /// <remarks>
    /// Written to settings as it is filed. "Saved" is what the button says,
    /// and a rule that went into the list but not into the file would be
    /// gone again next time the program opened.
    /// </remarks>
    /// <summary>
    /// Writes the box into the list, and stays on it.
    /// </summary>
    /// <remarks>
    /// Saving no longer clears the box. With a New rule button beside it
    /// there is something that means "done, next", so this one can mean
    /// only what it says: the rule goes into the list, and the box carries
    /// on editing the very entry it just made, so pressing it twice
    /// updates rather than duplicates.
    ///
    /// Unreachable on an unfinished rule, which is what the button being
    /// dead is for.
    /// </remarks>
    private void SaveRule_Click(object sender, RoutedEventArgs e)
    {
        var rule = _editor.Collect();
        if (!rule.IsUsable) return;

        var first = _editing is null;

        File(rule);
        Renumber();

        Write(first
            ? $"Saved {Named(rule)}. {Plural(_rules.Count, "rule")} in the list."
            : $"Updated {Named(rule)}.");
    }

    /// <summary>
    /// Puts a rule in the list, in place if it is already there.
    /// </summary>
    /// <remarks>
    /// Back where it came from, not onto the end: the first rule to give
    /// an ordering wins, so a rule's place in the list is part of what it
    /// means. The box is left editing whatever row it ends up as.
    /// </remarks>
    private void File(RenameRule rule)
    {
        var row = new SavedRuleRow(rule);

        if (_editing is null) _rules.Add(row);
        else _rules[_rules.IndexOf(_editing)] = row;

        _editing = row;
        row.IsEditing = true;
    }

    private static string Named(RenameRule rule) =>
        rule.Name.Length > 0 ? $"\"{rule.Name}\"" : "the rule";

    /// <summary>
    /// Puts the box back to the last saved version of what it is editing,
    /// or empties it if it was never saved.
    /// </summary>
    private void ClearRule_Click(object sender, RoutedEventArgs e)
    {
        var was = _editing;

        if (was is null)
        {
            ResetBox();
            Renumber();
            Said("Cleared.", ok: true);
            return;
        }

        ResetBox(Copy(was.Rule), was);
        Renumber();
        Said($"Put {Named(was.Rule)} back as it was saved.", ok: true);
    }

    /// <summary>Opens a rule from the list in the box.</summary>
    private void EditRule_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: SavedRuleRow row }) return;

        ResetBox(Copy(row.Rule), row);
    }

    private void RemoveRule_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: SavedRuleRow row }) return;

        _rules.Remove(row);

        // Only when the box was editing the one that has just gone. The
        // box is not reset merely because the list emptied: somebody
        // halfway through writing a new rule while clearing out the old
        // ones would lose it.
        if (ReferenceEquals(_editing, row)) ResetBox();

        Renumber();

        Write(_rules.Count == 0
            ? "Removed the last rule. The list is empty."
            : $"Removed a rule. {Plural(_rules.Count, "rule")} left.");
    }

    private void TestRule_Click(object sender, RoutedEventArgs e)
    {
        // Whichever was pressed: the one in the box, or one in the list.
        var subject = sender is Button { Tag: SavedRuleRow row } ? row : null;

        Test(subject);
    }

    /// <summary>
    /// Runs one rule against the folder, using the window as it stands.
    /// </summary>
    /// <remarks>
    /// The rule handed over is the same object the vocabulary holds, not a
    /// second copy of it: the tester tells what this rule did from what the
    /// folder looks like without it, and it finds "without it" by reference.
    ///
    /// The box's rule is tried on top of the list, in the place it would
    /// take if it were saved now — so testing before filing shows what
    /// filing it would actually do.
    /// </remarks>
    private void Test(SavedRuleRow? saved)
    {
        var box = _editor.Collect();

        // The list as it would stand if the box were saved right now: the
        // rule being edited replaced by what is in the box, or a new one
        // added on the end where saving would put it.
        var rules = _rules
            .Select(row => ReferenceEquals(row, _editing) ? box : row.Rule)
            .ToList();

        if (_editing is null) rules.Add(box);

        // Pressing test on the rule being edited means the edited version,
        // not the one still in the list behind it.
        var subject = saved is null || ReferenceEquals(saved, _editing) ? box : saved.Rule;

        var vocabulary = new FilenameVocabulary
        {
            Words = Words(WordsBox.Text),
            Blacklist = Words(BlacklistBox.Text),
            Rules = rules.Where(r => r.IsUsable).ToList(),
            MatchVariants = VariantsCheck.IsChecked == true
        };

        new RuleTestDialog(subject, vocabulary, _files?.Invoke() ?? Array.Empty<string>())
        {
            Owner = this
        }.ShowDialog();
    }

    /// <summary>
    /// Writes the word lists and the list of rules, and stays open.
    /// </summary>
    /// <remarks>
    /// Saving no longer means leaving. Rules reach the file as they are
    /// filed; this is for the two word boxes, which have no button of their
    /// own and would otherwise only be written when a rule happened to be.
    /// </remarks>
    private void SaveAll_Click(object sender, RoutedEventArgs e) =>
        Write($"Saved everything — {Plural(_rules.Count, "rule")}.");

    private bool _saved;
    private DispatcherTimer? _savedTimer;

    /// <summary>
    /// Writes the word lists and the list of rules.
    /// </summary>
    /// <remarks>
    /// Driven off the list, never off the box. Whatever is half-typed up
    /// there is not a rule yet, and nothing that has not been filed should
    /// reach the file.
    /// </remarks>
    private void Write(string said, bool ok = true)
    {
        _settings.Current.RenameWords = Words(WordsBox.Text);
        _settings.Current.RenameBlacklist = Words(BlacklistBox.Text);
        _settings.Current.RenameMatchVariants = VariantsCheck.IsChecked == true;

        // Folded in, so they are not read again.
        _settings.Current.RenameCategories = new List<string>();
        _settings.Current.RenameSubjects = new List<string>();
        _settings.Current.RenameConditionals = new List<string>();

        _settings.Current.RenameRules = _rules.Select(r => r.Rule).ToList();
        _settings.Save();

        _saved = true;
        Said(said, ok);
        Recount();
    }

    /// <summary>Says what happened, briefly.</summary>
    private void Said(string text, bool ok)
    {
        SavedText.Text = text;
        SavedText.SetResourceReference(System.Windows.Controls.TextBlock.ForegroundProperty,
                                       ok ? "StatusOk" : "StatusWarn");

        _savedTimer?.Stop();
        _savedTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
        _savedTimer.Tick += (_, _) =>
        {
            _savedTimer?.Stop();
            SavedText.Text = "";
        };
        _savedTimer.Start();
    }

    /// <summary>
    /// Closes, telling the caller whether anything was written.
    /// </summary>
    /// <remarks>
    /// The folder preview behind this window is rebuilt on a true, and a
    /// preview built from rules that were never saved would not match what
    /// a rename would actually do.
    /// </remarks>
    private void Close_Click(object sender, RoutedEventArgs e) => DialogResult = _saved;

    /// <summary>
    /// Escape leaves, the same as Close.
    /// </summary>
    /// <remarks>
    /// By hand rather than with IsCancel, which forces the result to false
    /// and would throw away the fact that something was saved.
    /// </remarks>
    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        base.OnPreviewKeyDown(e);

        if (e.Key != Key.Escape) return;

        _savedTimer?.Stop();
        DialogResult = _saved;
        e.Handled = true;
    }
}
