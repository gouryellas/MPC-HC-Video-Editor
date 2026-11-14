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
    /// The six phrases a condition can open with, blank first.
    /// </summary>
    /// <remarks>
    /// A row arrives with nothing chosen and shows only this dropdown.
    /// The word slots are no use until the phrase has said what the words
    /// are for.
    /// </remarks>
    public static readonly ObservableCollection<string> ActionChoices =
        new(new[] { Blank }.Concat(RuleActions.All));

    public RuleSlot(string? word, ObservableCollection<string>? choices = null,
                    bool isOperator = false)
    {
        _word = string.IsNullOrWhiteSpace(word) ? Blank : word.Trim();
        Choices = choices ?? new ObservableCollection<string> { Blank };
        IsOperator = isOperator;
    }

    /// <summary>
    /// Whether this dropdown holds an and/or rather than a word.
    /// </summary>
    /// <remarks>
    /// A row is a run of dropdowns of two kinds, and the only thing that
    /// differs between them on screen is how wide they are: an "and" needs
    /// a fraction of the room a word does.
    /// </remarks>
    public bool IsOperator { get; }

    public double Width => IsOperator ? 76 : 150;

    /// <summary>What a right-click on this slot offers to take away.</summary>
    /// <remarks>
    /// Named for the kind of slot it is rather than "Delete". A row is
    /// several boxes side by side, and a menu that says only "delete" does
    /// not say which of them is about to go.
    /// </remarks>
    public string DeleteLabel => IsOperator ? "Delete this operator" : "Delete this word";

    /// <summary>
    /// An operator is still a dropdown — it offers "and" and "or" and
    /// nothing else — where a word is typed. One template holds both
    /// controls and these pick which is on screen.
    /// </summary>
    public Visibility WordBoxVisibility => IsOperator ? Visibility.Collapsed : Visibility.Visible;

    public Visibility PickerVisibility => IsOperator ? Visibility.Visible : Visibility.Collapsed;

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

/// <summary>
/// One line of a rule: a phrase, and the run of dropdowns after it.
/// </summary>
/// <remarks>
/// <para>
/// Not a fixed shape of three words and two gaps. Each of the three add
/// buttons puts exactly one dropdown on the end of this row and nothing
/// else — a phrase, an operator, or a word — so what a row contains is
/// whatever was asked for, in the order it was asked for.
/// </para>
/// <para>
/// The engine still reads a line as words with joiners between them, which
/// <see cref="Collect"/> works out from the run: the words in order, and
/// each word's joiner being the operator that happens to sit in front of
/// it.
/// </para>
/// </remarks>
public sealed class RuleLineRow : INotifyPropertyChanged
{
    /// <summary>The most words the engine reads from one line.</summary>
    public const int MostWords = 3;

    public RuleLineRow(RuleLine line)
    {
        // A blank phrase is kept blank. It is how a row that has just been
        // added says it is waiting to be told what it does.
        var action = string.IsNullOrWhiteSpace(line.Action)
            ? RuleSlot.Blank
            : RuleActions.All.FirstOrDefault(a => RuleActions.Is(a, line.Action))
              ?? RuleActions.IfFound;

        Action = new RuleSlot(action, RuleSlot.ActionChoices);
        Watch(Action);

        Parts.CollectionChanged += (_, e) =>
        {
            foreach (var part in e.NewItems?.OfType<RuleSlot>() ?? Enumerable.Empty<RuleSlot>())
                Watch(part);

            Bubble();
        };

        Rebuild(line);
    }

    /// <summary>The phrase this line opens with.</summary>
    public RuleSlot Action { get; }

    private int _number;

    /// <summary>
    /// Which line of the rule this is, counting from one.
    /// </summary>
    /// <remarks>
    /// Kept by the rule rather than worked out here, because it changes
    /// when a line above is deleted. It is on screen so that the buttons
    /// under the box can name the line they are about to act on.
    /// </remarks>
    public int Number
    {
        get => _number;
        set
        {
            if (_number == value) return;
            _number = value;
            OnPropertyChanged();
        }
    }

    private bool _isActive;

    /// <summary>
    /// Whether this is the line being worked on.
    /// </summary>
    /// <remarks>
    /// The three add buttons act on one line, and until this existed that
    /// line was always the last one — so adding a word while part-way up a
    /// rule put it somewhere else entirely, with nothing on screen having
    /// said where it would go.
    /// </remarks>
    public bool IsActive
    {
        get => _isActive;
        set
        {
            if (_isActive == value) return;
            _isActive = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// Everything after the phrase, in the order it was put there.
    /// </summary>
    public ObservableCollection<RuleSlot> Parts { get; } = new();

    public IEnumerable<RuleSlot> WordParts => Parts.Where(p => !p.IsOperator);

    /// <summary>Lays out the run that a saved line amounts to.</summary>
    private void Rebuild(RuleLine line)
    {
        for (int i = 0; i < MostWords; i++)
        {
            var word = i < line.Words.Count ? line.Words[i] : RuleSlot.Blank;
            if (string.IsNullOrWhiteSpace(word)) continue;

            // The joiner that was saved in front of this word, if any.
            // Without this a condition written as "a or b" would reopen
            // looking like "a b", the or still in force and nowhere on
            // screen.
            var join = i >= 1 && i - 1 < line.Joins.Count ? line.Joins[i - 1] : RuleSlot.Blank;

            if (Parts.Count > 0 && !string.IsNullOrWhiteSpace(join)) Parts.Add(Operator(join));

            Parts.Add(Word(word));
        }

        // A phrase that governs words always has at least one after it —
        // it has to. "Delete the found words" governs none, and the empty
        // dropdown this used to put beside it was a slot that could not be
        // filled in, could not be read back, and sat there asking to be.
        if (Parts.Count == 0 && TakesWords) Parts.Add(Word(RuleSlot.Blank));
    }

    private static RuleSlot Word(string word) => new(word);

    private static RuleSlot Operator(string join) =>
        new(join, RuleSlot.JoinChoices, isOperator: true);

    /// <summary>Raised when anything in this line changes.</summary>
    /// <remarks>
    /// There is no "row emptied itself" event any more. A row that has
    /// lost its phrase or its words is unfinished, not gone — only the ✕
    /// at its end takes a row, so there is exactly one gesture that can.
    /// </remarks>
    public event EventHandler? Changed;

    private bool _settling;

    private void Watch(RuleSlot slot) =>
        slot.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(RuleSlot.Word)) Chose(slot);
            else Bubble();
        };

    /// <summary>
    /// Takes a dropdown being blanked as the removal it looks like.
    /// </summary>
    /// <remarks>
    /// A dropdown set back to nothing is a dropdown somebody is finished
    /// with, so it goes. Blanking the phrase takes the whole line, since
    /// nothing after it has anything left to belong to; blanking the last
    /// word does the same, since a phrase with nothing to act on says
    /// nothing either.
    /// </remarks>
    private void Chose(RuleSlot slot)
    {
        if (_settling) { Bubble(); return; }

        if (ReferenceEquals(slot, Action))
        {
            // Blanked, the row stays — holding its words, waiting to be
            // told what it does again. Only the ✕ takes a whole row, so
            // there is exactly one gesture that can.
            if (!Action.IsSet)
            {
                Bubble();
                return;
            }

            if (!TakesWords)
            {
                // A phrase that acts on words it does not name has nothing
                // after it at all — and if it was changed to from one that
                // did, the words that belonged to the old phrase go with
                // it rather than sitting there meaning nothing.
                _settling = true;
                try { Parts.Clear(); }
                finally { _settling = false; }
            }
            else if (Parts.Count == 0)
            {
                // Every other phrase brings out the word that has to
                // follow it.
                Parts.Add(Word(RuleSlot.Blank));
            }

            Bubble();
            return;
        }

        // A word is typed now, and typing passes through empty on the way
        // to every correction — backspacing out a typo must not make the
        // box vanish mid-edit. A word slot goes when it is asked to go:
        // the right-click menu, or the ✕ on its row.
        if (!slot.IsOperator || slot.IsSet) { Bubble(); return; }

        _settling = true;
        try { Parts.Remove(slot); }
        finally { _settling = false; }

        // An operator left hanging at either end, or doubled up because
        // the word between two of them has gone, joins nothing.
        Tidy();
        Bubble();
    }

    /// <summary>
    /// Takes one box out of the run, from the right-click menu.
    /// </summary>
    /// <remarks>
    /// Only the box. A row left with no words is unfinished and stays,
    /// because taking a whole row is the ✕'s job alone.
    /// </remarks>
    public void Remove(RuleSlot slot)
    {
        _settling = true;

        try
        {
            if (!Parts.Remove(slot)) return;
        }
        finally
        {
            _settling = false;
        }

        Tidy();
        Bubble();
    }

    /// <summary>Drops operators with nothing left on one side of them.</summary>
    private void Tidy()
    {
        _settling = true;

        try
        {
            for (int i = Parts.Count - 1; i >= 0; i--)
            {
                if (!Parts[i].IsOperator) continue;

                var wordBefore = i > 0 && !Parts[i - 1].IsOperator;
                var wordAfter = i + 1 < Parts.Count && !Parts[i + 1].IsOperator;

                if (!wordBefore || !wordAfter) Parts.RemoveAt(i);
            }
        }
        finally
        {
            _settling = false;
        }
    }

    public bool IsCondition => RuleActions.IsCondition(Action.Word);

    /// <summary>
    /// Whether this phrase has words after it at all.
    /// </summary>
    /// <remarks>
    /// "Delete the found words" does not: it acts on what the conditions
    /// above it already name. Its row is the phrase and nothing else.
    /// </remarks>
    public bool TakesWords => Action.IsSet && !RuleActions.Wordless(Action.Word);

    public bool CanAddWord => TakesWords && WordParts.Count() < MostWords;

    /// <summary>Whether an and/or would have words either side of it.</summary>
    public bool CanAddOperator =>
        TakesWords && Parts.Count > 0 && !Parts[^1].IsOperator
        && WordParts.Count() < MostWords;

    public void AddWord() => Parts.Add(Word(RuleSlot.Blank));

    public void AddOperator() => Parts.Add(Operator(RuleSlot.Blank));

    private void Bubble()
    {
        OnPropertyChanged(nameof(IsCondition));
        OnPropertyChanged(nameof(TakesWords));
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// The line as the engine reads it: words in order, each carrying
    /// whichever operator sat in front of it.
    /// </summary>
    public RuleLine Collect()
    {
        var words = new List<string>();
        var joins = new List<string>();
        var pending = RuleSlot.Blank;

        foreach (var part in Parts)
        {
            if (part.IsOperator) { pending = part.Word; continue; }

            if (words.Count > 0) joins.Add(pending);
            words.Add(part.Word);
            pending = RuleSlot.Blank;
        }

        return new RuleLine(Action.Word, words, joins);
    }

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

            // Numbered before anything is made current, so whatever the
            // add buttons go on to say about it says the right number.
            Renumber();

            // A row just added is the one being worked on — that is what
            // adding it was for. Otherwise the current row holds, unless
            // it was the row that just left.
            if (e.NewItems?.OfType<RuleLineRow>().LastOrDefault() is { } fresh) Active = fresh;
            else if (_active is null || !Lines.Contains(_active)) Active = Lines.LastOrDefault();

            Rechecked();
        };

        // Exactly the lines the rule has, and none invented. A new rule
        // opens with nothing in it: the two lines that used to be there by
        // default were a guess at what was wanted, and a guess that has to
        // be cleared is worse than an empty box with a + on it.
        foreach (var line in rule.Lines) Lines.Add(new RuleLineRow(line));
    }

    public ObservableCollection<RuleLineRow> Lines { get; } = new();

    private RuleLineRow? _active;

    /// <summary>
    /// The line the add buttons act on.
    /// </summary>
    /// <remarks>
    /// One at a time, and marked as such on screen. Clicking anywhere on a
    /// line makes it the current one; a line that has just been added
    /// becomes it without being clicked.
    /// </remarks>
    public RuleLineRow? Active
    {
        get => _active;
        set
        {
            if (ReferenceEquals(_active, value)) return;

            if (_active is not null) _active.IsActive = false;
            _active = value;
            if (_active is not null) _active.IsActive = true;

            OnPropertyChanged();
            OnPropertyChanged(nameof(Target));
        }
    }

    /// <summary>
    /// Where the next word or operator will go, as the add lines say it.
    /// </summary>
    /// <remarks>
    /// Empty while there is no line to add to, so the sentence reads
    /// "Add (+) words" and the button says for itself why it cannot.
    /// </remarks>
    public string Target => _active is null ? "" : $" to line {_active.Number}";

    private void Renumber()
    {
        for (int i = 0; i < Lines.Count; i++) Lines[i].Number = i + 1;

        OnPropertyChanged(nameof(Target));
    }

    private string _name;

    /// <summary>
    /// What this rule is called, exactly as it is being typed.
    /// </summary>
    /// <remarks>
    /// Not trimmed here. The field writes through on every keystroke, so
    /// trimming in the setter meant a typed space was removed and written
    /// back before the next letter arrived — which made a two-word name
    /// impossible. Tidied in <see cref="Collect"/> instead, once, when
    /// the typing is over.
    /// </remarks>
    public string Name
    {
        get => _name;
        set
        {
            var next = value ?? "";
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
    public bool HasInput => Lines.Any(l => l.Action.IsSet || l.Parts.Any(p => p.IsSet));

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

    private void Rechecked()
    {
        OnPropertyChanged(nameof(HasInput));
        OnPropertyChanged(nameof(IsTestable));
        OnPropertyChanged(nameof(TestGlyph));
    }

    public RenameRule Collect() => new()
    {
        Name = _name.Trim(),
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

    /// <summary>
    /// The phrase at its shortest, for the one-line summary.
    /// </summary>
    /// <remarks>
    /// A list meant to hold twenty rules cannot spend a line and a half
    /// on each saying "if these are found". Shortened rather than coded:
    /// "if not" is still English, where "!" would need learning.
    /// </remarks>
    public string Short => Action switch
    {
        RuleActions.IfFound => "if",
        RuleActions.IfNotFound => "if not",
        RuleActions.Order => "order",
        RuleActions.Delete => "delete",
        RuleActions.DeleteFound => "delete found",
        RuleActions.Change => "change",
        RuleActions.ChangeTo => "to",
        _ => Action
    };

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
        OnPropertyChanged(nameof(Summary));
        OnPropertyChanged(nameof(NameVisibility));
    }

    /// <summary>
    /// The whole rule on one line, a segment per row.
    /// </summary>
    /// <remarks>
    /// <para>
    /// One line each, so a folder's worth of rules can be seen at once
    /// rather than scrolled through. The rows are kept as separate
    /// segments with a dot between them rather than run together into a
    /// sentence: a rule's shape — which questions it asks, what it does
    /// when they hold — is the thing worth seeing at a glance, and prose
    /// buries it in commas.
    /// </para>
    /// <para>
    /// What is lost is the detail of a long rule, which runs off the end.
    /// That is what the pencil is for: the list is the index, the box
    /// above is where a rule is read in full.
    /// </para>
    /// </remarks>
    public string Summary =>
        string.Join("   ·   ", Rows.Select(r => (r.Short + " " + r.Words).TrimEnd()));

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
        Shown.Lines
            .Where(l => (RuleActions.IsCondition(l.Action) || RuleActions.IsDoing(l.Action))
                        && (l.Used.Count > 0 || RuleActions.Wordless(l.Action)))
            .Select(l => new SavedLineRow(l))
            .ToList();

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
/// The blacklist the bulk renamer scrubs with, and the rules that order
/// what is left.
/// </summary>
/// <remarks>
/// One box of text and a list of rules. There were five boxes once —
/// categories, subjects, conditionals, a word list, the blacklist — and
/// all but the last turned out to be the rules said somewhere else: a
/// rule carries its own words, typed straight into it, so a list to pick
/// them from was a second copy of every word and a detour on the way to
/// using one. The blacklist stays because the engine reads it directly —
/// it is the one list that acts on every file by itself.
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

        BlacklistBox.Text = string.Join(Environment.NewLine, settings.Current.RenameBlacklist);
        MatchModeBox.SelectedIndex = (int)settings.Current.RenameWordMatchMode;
        RegexCheck.IsChecked = settings.Current.RenameWordsAreRegex;
        RegexCheck.Checked += Regex_Changed;
        RegexCheck.Unchecked += Regex_Changed;

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

        // A row can now take itself out — blanking its action, or its last
        // word — so the last row leaving is not always a press of the ✕.
        // Either way an empty box is a box with nothing left to be editing.
        //
        // Guarded on the editor still being this one, or resetting would
        // see the fresh empty box it just made and do it again.
        var mine = _editor;

        mine.Lines.CollectionChanged += (_, _) =>
        {
            if (mine.Lines.Count > 0 || !ReferenceEquals(_editor, mine)) return;

            Dispatcher.BeginInvoke(() =>
            {
                if (ReferenceEquals(_editor, mine) && mine.Lines.Count == 0) ResetBox();
            }, DispatcherPriority.Background);
        };

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
    }

    /// <summary>The name a brand new rule arrives with.</summary>
    private string NextName() => "Rule #" + (_rules.Count + 1);

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
    private static List<string> Words(string? text, bool regex)
    {
        var lines = (text ?? "")
            .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Trim());

        // An expression is left exactly as typed. Running it through the
        // filename rules would strip the characters it is made of — a
        // dot, a star, a backslash are none of them allowed in a name —
        // and leave something that still looks like a pattern and is
        // not.
        if (!regex) lines = lines.Select(line => FilenameRules.Clean(line));

        return lines
            .Where(word => word.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(word => word, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>
    /// The blacklist's lines, under the one Regex switch the rules use.
    /// </summary>
    private List<string> BlacklistList() =>
        Words(BlacklistBox.Text, RegexCheck?.IsChecked == true);

    private static WordMatchMode Mode(ComboBox box) =>
        (WordMatchMode)Math.Max(0, box.SelectedIndex);

    private void MatchMode_Changed(object sender, SelectionChangedEventArgs e) => Recount();

    private void Regex_Changed(object sender, RoutedEventArgs e) => Recount();

    private void Words_Changed(object sender, TextChangedEventArgs e) => Recount();

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
        if (BlacklistHeading is null) return;   // during InitializeComponent

        BlacklistHeading.Text = $"Blacklisted ({BlacklistList().Count})";
        RulesHeading.Text = $"Rules ({_rules.Count})";
    }

    private static string Plural(int n, string noun) =>
        n == 1 ? $"1 {noun}" : $"{n} {noun}s";

    /// <summary>
    /// Adds a condition — a phrase and the words it governs, on its own
    /// row.
    /// </summary>
    /// <remarks>
    /// The phrase is left blank rather than pre-set. The row is one
    /// dropdown until it is told what it is, which is both less to look at
    /// and what brings out the first word afterwards.
    /// </remarks>
    private void AddCondition_Click(object sender, RoutedEventArgs e) =>
        _editor.Lines.Add(new RuleLineRow(new RuleLine(RuleSlot.Blank)));

    /// <summary>
    /// Adds one and/or dropdown to the condition being written, and
    /// nothing else.
    /// </summary>
    /// <remarks>
    /// Nothing else: a word does not come with it. Each of the three
    /// buttons puts exactly one dropdown on the end of the row, which is
    /// what makes the row whatever was asked for rather than a fixed
    /// shape with gaps in it.
    /// </remarks>
    private void AddOperator_Click(object sender, RoutedEventArgs e)
    {
        if (Writing() is not { } line) return;

        if (!line.CanAddOperator)
        {
            Said(line.Parts.Count == 0 || line.Parts[^1].IsOperator
                 ? $"An operator goes between two words — add a word to line {line.Number} first."
                 : $"Line {line.Number} holds three words at most.", ok: false);
            return;
        }

        line.AddOperator();
    }

    /// <summary>
    /// Adds one word dropdown to the condition being written.
    /// </summary>
    /// <remarks>
    /// With no operator in front of it, for a condition that is a list of
    /// words rather than a sentence of them. Nothing is implied by
    /// leaving the operator out: the joiner stays blank, which the engine
    /// has always read as "and".
    /// </remarks>
    private void AddWords_Click(object sender, RoutedEventArgs e)
    {
        if (Writing() is not { } line) return;

        if (!line.CanAddWord)
        {
            Said($"Line {line.Number} holds three words at most.", ok: false);
            return;
        }

        line.AddWord();
    }

    /// <summary>
    /// The condition being written — the one marked current.
    /// </summary>
    /// <remarks>
    /// The line clicked last, not the last line. Those were the same thing
    /// until rows could be worked on out of order, at which point adding a
    /// word part-way up a rule put it on the bottom row instead.
    ///
    /// Words and operators belong to a condition, so there has to be one
    /// for them to belong to; where there is not, it says so rather than
    /// quietly doing nothing.
    /// </remarks>
    private RuleLineRow? Writing()
    {
        var line = _editor.Active;

        if (line is null)
        {
            Said("Add a condition first — words and operators belong to one.", ok: false);
            return null;
        }

        if (!line.Action.IsSet)
        {
            Said($"Choose what line {line.Number} does first.", ok: false);
            return null;
        }

        if (!line.TakesWords)
        {
            Said($"\"{line.Action.Word}\" on line {line.Number} takes no words of its own.", ok: false);
            return null;
        }

        return line;
    }

    /// <summary>
    /// Makes the line under the pointer the one the add buttons act on.
    /// </summary>
    /// <remarks>
    /// On the way down, so it has happened before the dropdown that was
    /// clicked gets the click — and without handling the event, which is
    /// still the dropdown's.
    /// </remarks>
    private void Row_MouseDown(object sender, MouseButtonEventArgs e) => Working(sender);

    /// <summary>The same, for a line reached by keyboard.</summary>
    private void Row_GotFocus(object sender, RoutedEventArgs e) => Working(sender);

    private void Working(object sender)
    {
        if ((sender as FrameworkElement)?.DataContext is RuleLineRow line) _editor.Active = line;
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
        if (sender is Button { Tag: RuleLineRow line }) DropLine(line);
    }

    private void DropLine(RuleLineRow line)
    {
        _editor.Lines.Remove(line);

        if (_editor.Lines.Count == 0) ResetBox();
    }

    /// <summary>
    /// Whatever was right-clicked, as the thing the row stands for.
    /// </summary>
    /// <remarks>
    /// A context menu is not in the visual tree, so it is given the
    /// DataContext of the control it was opened on — see the templates.
    /// Its items inherit from it in the ordinary way.
    /// </remarks>
    private static T? RightClicked<T>(object sender) where T : class =>
        (sender as MenuItem)?.DataContext as T;

    /// <summary>
    /// Takes one dropdown out of its row, from the right-click menu.
    /// </summary>
    /// <remarks>
    /// Which row it belongs to is asked of the rows rather than carried on
    /// the dropdown, for the same reason the drag does it: a part's place
    /// in a row changes as words are added and cleared.
    /// </remarks>
    private void DeleteSlot_Click(object sender, RoutedEventArgs e)
    {
        if (RightClicked<RuleSlot>(sender) is not { } slot) return;

        _editor.Lines.FirstOrDefault(l => l.Parts.Contains(slot))?.Remove(slot);
    }

    /// <summary>
    /// Clears a row's phrase, from its right-click menu.
    /// </summary>
    /// <remarks>
    /// Clears, not removes: every row has exactly one phrase box, so
    /// deleting it means putting it back to blank. The row and its words
    /// stay — taking a whole row is the ✕'s job alone.
    /// </remarks>
    private void DeleteCondition_Click(object sender, RoutedEventArgs e)
    {
        if (RightClicked<RuleLineRow>(sender) is { } line)
            line.Action.Word = RuleSlot.Blank;
    }


    /// <summary>
    /// Files the rule in the box, then empties the box.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Written to settings as it is filed. "Saved" is what the button
    /// says, and a rule that went into the list but not into the file
    /// would be gone again next time the program opened.
    /// </para>
    /// <para>
    /// Updating an existing rule and writing a new one both end the same
    /// way, on an empty box ready for the next rule. Clearing after one
    /// and not the other would make the same button mean two things.
    /// </para>
    /// <para>
    /// Unreachable on an unfinished rule, which is what the button being
    /// dead is for.
    /// </para>
    /// </remarks>
    private void SaveRule_Click(object sender, RoutedEventArgs e)
    {
        var rule = _editor.Collect();
        if (!rule.IsUsable) return;

        var first = _editing is null;

        File(rule);
        ResetBox();
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
            Blacklist = BlacklistList(),
            Rules = rules.Where(r => r.IsUsable).ToList(),
            MatchMode = Mode(MatchModeBox),
            UseRegex = RegexCheck.IsChecked == true,
            // Precise on purpose and not offered otherwise: these words
            // are deleted rather than recognised, so a match that reaches
            // further than written takes a word out of the name with
            // nothing left to say it went. The Regex switch is the one
            // exception — an expression says exactly what it matches,
            // which is its own kind of precise.
            BlacklistMatchMode = WordMatchMode.Precise,
            BlacklistUseRegex = RegexCheck.IsChecked == true
        };

        var report = new RuleTestDialog(subject, vocabulary,
                                        _files?.Invoke() ?? Array.Empty<string>())
        {
            Owner = this
        };

        // A list of renames is a window; "nothing changed" is a sentence,
        // and a sentence is a toast.
        if (report.FoundAnything) report.ShowDialog();
        else
        {
            Helpers.Toast.Show(this, report.Outcome);
            report.Close();
        }
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
        // The word list itself is gone — rules carry their own words now,
        // typed straight into them — but whatever was saved in it is left
        // in the settings file untouched rather than wiped by a window
        // that no longer shows it.
        _settings.Current.RenameBlacklist = BlacklistList();
        _settings.Current.RenameWordMatchMode = Mode(MatchModeBox);
        _settings.Current.RenameWordsAreRegex = RegexCheck.IsChecked == true;
        _settings.Current.RenameBlacklistMatchMode = WordMatchMode.Precise;
        _settings.Current.RenameBlacklistIsRegex = RegexCheck.IsChecked == true;

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
