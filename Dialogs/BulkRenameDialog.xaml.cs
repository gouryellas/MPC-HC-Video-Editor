using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using Microsoft.Win32;
using MpcHcVideoEditor.Helpers;
using MpcHcVideoEditor.Services;

namespace MpcHcVideoEditor.Dialogs;

/// <summary>One file in the preview.</summary>
public sealed class RenameRow : INotifyPropertyChanged
{
    private readonly Func<string, string> _rules;

    public RenameRow(string folder, RenameProposal proposal, Func<string, string> rules)
    {
        _rules = rules;
        Folder = folder;
        Original = proposal.Original;
        _proposed = proposal.Proposed;

        // The reason this row is awkward, if it is one, as the rules found
        // it. Kept apart from the clash note so that one cannot erase the
        // other: a name with nothing left of it after the rules is still
        // that, whatever else is going on in the folder.
        BaseNote = proposal.Note;
        _note = BaseNote;
    }

    /// <summary>Why the rules could not do anything useful, or empty.</summary>
    public string BaseNote { get; }

    public string Folder { get; }
    public string Original { get; }
    public string Path => System.IO.Path.Combine(Folder, Original);

    private string _proposed;

    /// <summary>
    /// What the file will be called. Settable, and corrected on the way in.
    /// </summary>
    /// <remarks>
    /// Typed names go through the same rules as generated ones, because the
    /// rules "still must be followed" — so the field cannot be used to put
    /// back a space or a capital. The rules are idempotent, so a name already
    /// in shape comes back untouched and the field does not fight the typist.
    /// </remarks>
    public string Proposed
    {
        get => _proposed;
        set
        {
            var corrected = _rules(value ?? "");
            if (corrected.Length == 0) corrected = Original;

            if (_proposed == corrected)
            {
                // Still raise it: the box may hold what was typed rather than
                // what was accepted, and needs putting back.
                OnPropertyChanged();
                return;
            }

            _proposed = corrected;
            OnPropertyChanged();
            Edited?.Invoke();
        }
    }

    private string _note;

    public string Note
    {
        get => _note;
        set { _note = value; OnPropertyChanged(); OnPropertyChanged(nameof(NoteVisibility)); }
    }

    public Visibility NoteVisibility =>
        string.IsNullOrEmpty(_note) ? Visibility.Collapsed : Visibility.Visible;

    private bool _canRename;

    /// <summary>
    /// Whether this row is worth acting on: something to change, and nothing
    /// in the way of changing it.
    /// </summary>
    public bool CanRename
    {
        get => _canRename;
        set
        {
            _canRename = value;
            if (!value) _isChecked = false;

            OnPropertyChanged();
            OnPropertyChanged(nameof(IsChecked));
            OnPropertyChanged(nameof(ActionVisibility));
        }
    }

    /// <summary>
    /// Whether this row offers anything to do.
    /// </summary>
    /// <remarks>
    /// A file whose name already follows the rules shows its name and
    /// nothing else — no tick box, no arrow, no field. There is no decision
    /// to make about it, and an empty row makes the ones that do need
    /// attention easier to find.
    ///
    /// A row blocked by a clash keeps its controls: that one has a decision
    /// in it, which is to type a different name.
    /// </remarks>
    public Visibility ActionVisibility =>
        CanRename || BaseNote.Length > 0 ? Visibility.Visible : Visibility.Collapsed;

    private bool _isChecked;

    public bool IsChecked
    {
        get => _isChecked;
        set
        {
            if (_isChecked == value) return;
            _isChecked = value && CanRename;
            OnPropertyChanged();
            Edited?.Invoke();
        }
    }

    /// <summary>Raised when the window needs to re-check the whole list.</summary>
    public Action? Edited { get; set; }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

/// <summary>
/// Renames a folder's files to the house shape, after showing exactly what
/// it would do.
/// </summary>
/// <remarks>
/// Nothing is renamed until the list has been seen and the button pressed.
/// Every row can be unchecked, and every proposed name can be typed over —
/// through the same rules, so an edit cannot produce a name the rules would
/// have refused.
/// </remarks>
public partial class BulkRenameDialog : Window
{
    private readonly SettingsService _settings;
    private readonly ObservableCollection<RenameRow> _rows = new();
    private string? _folder;

    public BulkRenameDialog(SettingsService settings, string? startIn = null)
    {
        InitializeComponent();

        _settings = settings;
        RowList.ItemsSource = _rows;

        if (!string.IsNullOrWhiteSpace(startIn) && Directory.Exists(startIn))
            SetFolder(startIn!);
    }

    private FilenameVocabulary Vocabulary => new()
    {
        Categories = _settings.Current.RenameCategories,
        Subjects = _settings.Current.RenameSubjects,
        Blacklist = _settings.Current.RenameBlacklist,
        Conditionals = _settings.Current.RenameConditionals,
        Rules = _settings.Current.RenameRules
    };

    private void SetFolder(string folder)
    {
        _folder = folder;
        FolderText.Text = folder;
    }

    private void ChooseFolder_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFolderDialog { Title = "Folder to rename files in" };
        if (!string.IsNullOrEmpty(_folder)) dlg.InitialDirectory = _folder;

        if (dlg.ShowDialog(this) == true) SetFolder(dlg.FolderName);
    }

    private void Words_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new RenameWordsDialog(_settings) { Owner = this };
        if (dlg.ShowDialog() == true && _rows.Count > 0) Preview_Click(sender, e);
    }

    private static readonly string[] VideoExtensions =
        { ".mp4", ".mkv", ".avi", ".mov", ".wmv", ".webm", ".mpeg", ".mpg", ".ts", ".m4v", ".flv", ".m2ts" };

    private void Preview_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_folder) || !Directory.Exists(_folder))
        {
            Show("Choose a folder first.");
            return;
        }

        _rows.Clear();

        try
        {
            var search = RecurseCheck.IsChecked == true
                ? SearchOption.AllDirectories
                : SearchOption.TopDirectoryOnly;

            var files = Directory.EnumerateFiles(_folder, "*", search);

            if (VideosOnlyCheck.IsChecked == true)
                files = files.Where(f => VideoExtensions.Contains(
                    Path.GetExtension(f), StringComparer.OrdinalIgnoreCase));

            // Grouped by folder, because a collision is only a collision
            // inside one folder — the same name in two folders is fine.
            foreach (var group in files.GroupBy(f => Path.GetDirectoryName(f) ?? ""))
            {
                var names = group.Select(Path.GetFileName).OfType<string>()
                                 .OrderBy(n => n, StringComparer.OrdinalIgnoreCase).ToList();

                foreach (var proposal in FilenameRules.Propose(names, Vocabulary))
                {
                    var row = new RenameRow(group.Key, proposal,
                                            n => FilenameRules.Apply(n, Vocabulary));
                    row.Edited = Recheck;
                    _rows.Add(row);
                }
            }
        }
        catch (Exception ex)
        {
            Diagnostics.Swallowed("BulkRename.Preview", ex);
            Show("The folder could not be read: " + ex.Message);
            return;
        }

        // Which rows are workable has to be settled before any of them can
        // be ticked: a row only knows it can be renamed once the whole
        // folder has been looked at for clashes.
        Recheck();

        foreach (var row in _rows) row.IsChecked = row.CanRename;
        Recheck();

        if (_rows.Count == 0) Show("No files found in that folder.");
        else if (_rows.All(r => !r.CanRename)) Show("");
        else EmptyText.Visibility = Visibility.Collapsed;
    }

    private void Show(string message)
    {
        EmptyText.Text = message.Length > 0
            ? message
            : "Every file here already follows the rules.";

        EmptyText.Visibility = _rows.Count == 0 || message.Length > 0
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    /// <summary>
    /// Re-reads the whole list for names that now clash.
    /// </summary>
    /// <remarks>
    /// Over everything rather than the row that changed: typing a name can
    /// both create a clash and clear one somewhere else, and a list this size
    /// is nothing to walk.
    ///
    /// A file keeping its own name counts as taken — renaming another file
    /// onto it would overwrite it.
    /// </remarks>
    /// <summary>
    /// Guards against this calling itself.
    /// </summary>
    /// <remarks>
    /// Clearing a row's CanRename unchecks it, and unchecking it asks for
    /// another pass — so a single call could re-enter partway through its own
    /// loop and finish on a half-computed answer. The footer showed it:
    /// "5 can be renamed" beside four rows that could be.
    /// </remarks>
    private bool _rechecking;

    private void Recheck()
    {
        if (_rechecking) return;
        _rechecking = true;

        try { RecheckOnce(); }
        finally { _rechecking = false; }
    }

    private void RecheckOnce()
    {
        foreach (var folder in _rows.GroupBy(r => r.Folder))
        {
            var taken = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            foreach (var row in folder)
            {
                var name = row.IsChecked ? row.Proposed : row.Original;
                taken.TryGetValue(name, out var count);
                taken[name] = count + 1;
            }

            foreach (var row in folder)
            {
                var unchanged = string.Equals(row.Proposed, row.Original, StringComparison.Ordinal);
                var clashes = !unchanged
                              && taken.TryGetValue(row.Proposed, out var n) && n > 1;

                // Recomputed from scratch every time rather than edited in
                // place, so a note can never outlive the thing it described.
                row.Note = clashes
                    ? "That name is already taken in this folder"
                    : row.BaseNote;

                row.CanRename = !unchanged && !clashes && row.BaseNote.Length == 0;
            }
        }

        var ready = _rows.Count(r => r.IsChecked);
        RenameButton.IsEnabled = ready > 0;

        SummaryText.Text = _rows.Count == 0
            ? ""
            : $"{_rows.Count} file(s) · {_rows.Count(r => r.CanRename)} can be renamed · {ready} checked";
    }

    private void SelectAll_Click(object sender, RoutedEventArgs e)
    {
        foreach (var row in _rows) row.IsChecked = row.CanRename;
        Recheck();
    }

    private void DeselectAll_Click(object sender, RoutedEventArgs e)
    {
        foreach (var row in _rows) row.IsChecked = false;
        Recheck();
    }

    /// <summary>
    /// Renames what is checked, reporting anything that would not move.
    /// </summary>
    /// <remarks>
    /// A move onto an existing file is refused outright rather than
    /// overwriting. The preview flags clashes among the proposed names, but
    /// it cannot know about a file that appeared since — and losing somebody's
    /// video to a rename is not a recoverable mistake.
    /// </remarks>
    private async void Rename_Click(object sender, RoutedEventArgs e)
    {
        var rows = _rows.Where(r => r.IsChecked).ToList();
        if (rows.Count == 0) return;

        var answer = MessageBox.Show(this,
            $"Rename {rows.Count} file(s)?\n\nThe files are renamed where they are. " +
            "Nothing is moved, copied or deleted.",
            "Rename files", MessageBoxButton.YesNo, MessageBoxImage.Question,
            MessageBoxResult.No);

        if (answer != MessageBoxResult.Yes) return;

        RenameButton.IsEnabled = false;

        var failures = new List<string>();
        var done = 0;

        await Task.Run(() =>
        {
            foreach (var row in rows)
            {
                var target = Path.Combine(row.Folder, row.Proposed);

                try
                {
                    if (File.Exists(target))
                    {
                        failures.Add($"{row.Original} — {row.Proposed} already exists");
                        continue;
                    }

                    File.Move(row.Path, target);
                    done++;
                }
                catch (Exception ex)
                {
                    failures.Add($"{row.Original} — {ex.Message}");
                }
            }
        });

        foreach (var row in rows.Take(rows.Count))
            if (!failures.Any(f => f.StartsWith(row.Original + " —", StringComparison.Ordinal)))
                _rows.Remove(row);

        Recheck();

        if (failures.Count > 0)
            MessageBox.Show(this,
                $"{done} renamed. These were left alone:\n\n" +
                string.Join("\n", failures.Take(12)),
                "Rename files", MessageBoxButton.OK, MessageBoxImage.Warning);

        if (_rows.Count == 0) Show("Done.");
    }
}
