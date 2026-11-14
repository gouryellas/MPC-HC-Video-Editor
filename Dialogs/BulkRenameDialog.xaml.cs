using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
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
        !Done && (CanRename || BaseNote.Length > 0)
            ? Visibility.Visible
            : Visibility.Collapsed;

    private bool _done;

    /// <summary>
    /// Whether this file has been renamed.
    /// </summary>
    /// <remarks>
    /// The row stays in the list afterwards rather than vanishing. A list
    /// that empties itself as it works tells you only that something
    /// happened; one that marks each row green says which, and leaves the
    /// old name beside the new for as long as the window is open.
    ///
    /// Its controls go: the tick box, the field and the arrow all offered
    /// a decision that has now been made.
    /// </remarks>
    public bool Done
    {
        get => _done;
        set
        {
            if (_done == value) return;

            _done = value;
            _isChecked = false;

            OnPropertyChanged();
            OnPropertyChanged(nameof(IsChecked));
            OnPropertyChanged(nameof(ActionVisibility));
            OnPropertyChanged(nameof(DoneVisibility));
        }
    }

    public Visibility DoneVisibility => _done ? Visibility.Visible : Visibility.Collapsed;

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

        _tick.Tick += (_, _) => ShowWorking();

        // The handler is attached in XAML and fires on this, which is
        // harmless: there is no preview to rebuild yet and the setting is
        // written back as the value it already had.
        ModelBox.SelectedIndex = settings.Current.RenameUseCustomRules ? 1 : 0;

        if (!string.IsNullOrWhiteSpace(startIn) && Directory.Exists(startIn))
            SetFolder(startIn!);
    }

    /// <summary>
    /// What the renamer is allowed to know, which is what the two modes
    /// differ by.
    /// </summary>
    /// <remarks>
    /// Simple is not a lesser set of rules — it is the character rules on
    /// their own, with nothing to recognise. Lowercase, no spaces, the
    /// allowed characters, a bracketed tag lifted to the end and a
    /// first_last name joined up: everything that needs no word list to
    /// work out. Custom hands over the lists and rules as well, and the
    /// same code does the rest.
    /// </remarks>
    private FilenameVocabulary Vocabulary =>
        ModelBox.SelectedIndex != 1
            ? FilenameVocabulary.Empty
            : new FilenameVocabulary
            {
                Blacklist = _settings.Current.RenameBlacklist,
                Rules = _settings.Current.RenameRules,
                MatchMode = _settings.Current.RenameWordMatchMode,
                UseRegex = _settings.Current.RenameWordsAreRegex,
                BlacklistMatchMode = _settings.Current.RenameBlacklistMatchMode,
                BlacklistUseRegex = _settings.Current.RenameBlacklistIsRegex
            };

    /// <summary>
    /// Remembers the choice, and redoes the preview it just invalidated.
    /// </summary>
    private void Model_Changed(object sender, SelectionChangedEventArgs e)
    {
        // Fires once during construction, before the rest of the window
        // exists.
        if (_settings is null) return;

        _settings.Current.RenameUseCustomRules = ModelBox.SelectedIndex == 1;
        _settings.Save();

        // A preview on screen was built under the other model and is now
        // showing names that are not the ones a rename would apply.
        if (_rows.Count > 0) Generate_Click(sender, e);
    }

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
        // Handed the folder as a function rather than as a list. The window
        // stays open while rules are written and tested, and the checkboxes
        // above can be changed behind it — read when a test is run, it is
        // whatever the folder is at that moment.
        var dlg = new RenameWordsDialog(_settings, FilesInFolder) { Owner = this };
        if (dlg.ShowDialog() == true && _rows.Count > 0) Generate_Click(sender, e);
    }

    private static readonly string[] VideoExtensions =
        { ".mp4", ".mkv", ".avi", ".mov", ".wmv", ".webm", ".mpeg", ".mpg", ".ts", ".m4v", ".flv", ".m2ts" };

    /// <summary>
    /// The files the chosen folder offers, as the two checkboxes filter it.
    /// </summary>
    /// <remarks>
    /// One place, so the rule tester is looking at exactly the files the
    /// preview would build from. A tester working off a slightly different
    /// list is worse than no tester.
    /// </remarks>
    private IReadOnlyList<string> FilesInFolder()
    {
        if (string.IsNullOrWhiteSpace(_folder) || !Directory.Exists(_folder))
            return Array.Empty<string>();

        try
        {
            var search = RecurseCheck.IsChecked == true
                ? SearchOption.AllDirectories
                : SearchOption.TopDirectoryOnly;

            var files = Directory.EnumerateFiles(_folder, "*", search);

            if (VideosOnlyCheck.IsChecked == true)
                files = files.Where(f => VideoExtensions.Contains(
                    Path.GetExtension(f), StringComparer.OrdinalIgnoreCase));

            return files.ToList();
        }
        catch
        {
            // A folder that cannot be read is an empty one as far as a
            // preview is concerned; Generate reports the error properly.
            return Array.Empty<string>();
        }
    }

    private CancellationTokenSource? _running;

    /// <summary>Whether a read is going, which is what the button means.</summary>
    private bool _scanning;

    /// <summary>Drives the moving dots and the elapsed time.</summary>
    private readonly System.Windows.Threading.DispatcherTimer _tick = new()
    {
        Interval = TimeSpan.FromMilliseconds(450)
    };

    private readonly System.Diagnostics.Stopwatch _clock = new();
    private int _dots;
    private int _done;
    private int _fileTotal;

    /// <summary>What the finished read found, for the status line.</summary>
    private bool _finished;
    private string _tookTime = "";

    /// <summary>
    /// Reads the folder and proposes a new name for everything in it.
    /// </summary>
    /// <remarks>
    /// Off the UI thread and cancellable, because a recursive folder of
    /// thousands is a real wait and a window that cannot be stopped or
    /// even repainted during it is the wrong answer. The rules themselves
    /// are pure, so they run on the worker; only the rows come back.
    /// </remarks>
    private async void Generate_Click(object sender, RoutedEventArgs e)
    {
        // The same button, mid-read, is the stop button.
        if (_scanning)
        {
            _running?.Cancel();
            return;
        }

        if (string.IsNullOrWhiteSpace(_folder) || !Directory.Exists(_folder))
        {
            Show("Choose a folder first.");
            return;
        }

        _running?.Cancel();
        _running = new CancellationTokenSource();
        var ct = _running.Token;

        // Last run's outcome refers to a list that is about to be
        // replaced.
        ResultPanel.Visibility = Visibility.Collapsed;
        _rows.Clear();
        _finished = false;

        ShowScanning(true);
        EmptyText.Visibility = Visibility.Collapsed;

        ProgressPanel.Visibility = Visibility.Visible;
        _dots = 0;
        _done = 0;
        _fileTotal = 0;
        _clock.Restart();
        _tick.Start();
        ShowWorking();

        try
        {
            var recursive = RecurseCheck.IsChecked == true;
            var videosOnly = VideosOnlyCheck.IsChecked == true;
            var folder = _folder!;
            var vocabulary = Vocabulary;

            var search = recursive
                ? SearchOption.AllDirectories
                : SearchOption.TopDirectoryOnly;

            // The walk first, on its own, so the count the bar divides by
            // is known before the bar is shown filling against it.
            var files = await Task.Run(() =>
            {
                var found = Directory.EnumerateFiles(folder, "*", search);

                if (videosOnly)
                    found = found.Where(f => VideoExtensions.Contains(
                        Path.GetExtension(f), StringComparer.OrdinalIgnoreCase));

                return found.ToList();
            }, ct);

            _fileTotal = files.Count;

            // Grouped by folder, because a collision is only a collision
            // inside one folder — the same name in two folders is fine.
            var built = await Task.Run(() =>
            {
                var made = new List<(string Folder, RenameProposal Proposal)>();

                foreach (var group in files.GroupBy(f => Path.GetDirectoryName(f) ?? ""))
                {
                    ct.ThrowIfCancellationRequested();

                    var names = group.Select(Path.GetFileName).OfType<string>()
                                     .OrderBy(n => n, StringComparer.OrdinalIgnoreCase).ToList();

                    foreach (var proposal in FilenameRules.Propose(names, vocabulary))
                    {
                        made.Add((group.Key, proposal));

                        // Read by the timer rather than pushed per file:
                        // the rules are fast enough that reporting each
                        // one would cost more than the work.
                        _done = made.Count;
                    }
                }

                return made;
            }, ct);

            foreach (var (folderOf, proposal) in built)
            {
                var row = new RenameRow(folderOf, proposal,
                                        n => FilenameRules.Apply(n, vocabulary));
                row.Edited = Recheck;
                _rows.Add(row);
            }

            // Which rows are workable has to be settled before any of them
            // can be ticked: a row only knows it can be renamed once the
            // whole folder has been looked at for clashes.
            Recheck();

            foreach (var row in _rows) row.IsChecked = row.CanRename;

            _tookTime = Elapsed;
            _finished = true;
            Recheck();

            if (_rows.Count == 0) Show("No files found in that folder.");
            else if (_rows.All(r => !r.CanRename)) Show("");
            else EmptyText.Visibility = Visibility.Collapsed;
        }
        catch (OperationCanceledException)
        {
            Say("", "Stopped.", Elapsed);

            if (_rows.Count == 0)
            {
                EmptyText.Text = "Stopped before anything was read.";
                EmptyText.Visibility = Visibility.Visible;
            }
        }
        catch (Exception ex)
        {
            Diagnostics.Swallowed("BulkRename.Generate", ex);
            Say("", "The folder could not be read.", Elapsed);
            Show("The folder could not be read: " + ex.Message);
        }
        finally
        {
            _tick.Stop();
            _clock.Stop();
            ShowScanning(false);
        }
    }

    /// <summary>The read's own clock, written the way the status bar writes one.</summary>
    private string Elapsed => JobProgress.FormatElapsed(_clock.Elapsed);

    /// <summary>
    /// Turns the one button into whichever of its two jobs applies.
    /// </summary>
    private void ShowScanning(bool scanning)
    {
        _scanning = scanning;
        ScanButton.Content = scanning ? "Stop" : "Scan";

        if (scanning)
        {
            ScanButton.Background = new SolidColorBrush(Color.FromRgb(0xB3, 0x31, 0x2C));
            ScanButton.BorderBrush = new SolidColorBrush(Color.FromRgb(0x8E, 0x26, 0x22));
            ScanButton.Foreground = Brushes.White;

            // Tag is the hover shade the filled template reads. Left
            // alone, a red Stop would hover to the theme's own colour.
            ScanButton.Tag = new SolidColorBrush(Color.FromRgb(0xC9, 0x3A, 0x34));
        }
        else
        {
            ScanButton.SetResourceReference(BackgroundProperty, "PrimaryButton");
            ScanButton.SetResourceReference(BorderBrushProperty, "PrimaryButtonBorder");
            ScanButton.SetResourceReference(ForegroundProperty, "OnAccent");
            ScanButton.SetResourceReference(TagProperty, "PrimaryButtonBorder");
        }
    }

    /// <summary>
    /// What the read has to say for itself right now: how many of the
    /// folder's files it has named, or moving dots while it is still
    /// finding out how many there are.
    /// </summary>
    private void ShowWorking()
    {
        // Three dots down to none, then round again.
        var dots = new string('.', 3 - _dots % 4);
        _dots++;

        if (_fileTotal > 0)
        {
            var done = Math.Clamp(_done, 0, _fileTotal);

            Say($"Reading names{dots}", $"Step 2/{Steps} - {done:N0} of {_fileTotal:N0}", Elapsed);
            return;
        }

        // No count on the walk: how many files there are is the thing it
        // is finding out.
        Say($"Finding files{dots}", $"Step 1/{Steps}", Elapsed);
    }

    /// <summary>
    /// How many steps a read has: the walk, then the naming.
    /// </summary>
    private const int Steps = 2;

    /// <summary>
    /// Writes the status line: a stage in the fixed slot, the figures
    /// beside it, the clock against the right edge. A stage of its own
    /// takes the whole line, which is what the finished sentence and the
    /// stopped one want.
    /// </summary>
    private void Say(string stage, string info, string elapsed = "")
    {
        StageText.Width = stage.Length == 0 ? 0 : 158;
        StageText.Text = stage;
        InfoText.Text = info;
        ElapsedText.Text = elapsed;
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
                // A row already renamed holds the name it now has, which
                // is the one a later row would be clashing with.
                var name = row.Done || row.IsChecked ? row.Proposed : row.Original;
                taken.TryGetValue(name, out var count);
                taken[name] = count + 1;
            }

            foreach (var row in folder)
            {
                if (row.Done) continue;

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
        var renamed = _rows.Count(r => r.Done);

        RenameButton.IsEnabled = ready > 0;

        // The one line that says it all, in the place the progress line
        // was — the same slot the duplicates window finishes in.
        if (_finished)
        {
            Say("",
                $"Finished! Read {_rows.Count} file(s).   " +
                $"{_rows.Count(r => r.CanRename)} can be renamed - {ready} selected" +
                (renamed > 0 ? $" - {renamed} renamed" : ""),
                _tookTime);
        }
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
    /// Renames what is selected, reporting anything that would not move.
    /// </summary>
    /// <remarks>
    /// A move onto an existing file is refused outright rather than
    /// overwriting. The preview flags clashes among the proposed names, but
    /// it cannot know about a file that appeared since — and losing somebody's
    /// video to a rename is not a recoverable mistake.
    /// </remarks>
    /// <summary>
    /// Renames a file to a different spelling of its own name.
    /// </summary>
    /// <remarks>
    /// By way of a temporary name. A direct move would be asking the file
    /// system to move a file onto itself, which it may refuse — there is
    /// no reliable answer across versions of Windows, and two moves that
    /// always work beat one that usually does.
    /// </remarks>
    private static void ChangeCase(string from, string to)
    {
        var folder = Path.GetDirectoryName(to) ?? "";
        var step = Path.Combine(folder, Path.GetRandomFileName());

        while (File.Exists(step)) step = Path.Combine(folder, Path.GetRandomFileName());

        File.Move(from, step);

        try
        {
            File.Move(step, to);
        }
        catch
        {
            // Put it back rather than leave it under a made-up name.
            try { File.Move(step, from); } catch { }
            throw;
        }
    }

    private async void Rename_Click(object sender, RoutedEventArgs e)
    {
        var rows = _rows.Where(r => r.IsChecked).ToList();
        if (rows.Count == 0) return;

        // No confirmation. The preview is the confirmation: nothing here
        // is checked that was not looked at first, and the button says
        // what it does.
        RenameButton.IsEnabled = false;

        var failures = new List<string>();
        var done = 0;

        await Task.Run(() =>
        {
            foreach (var row in rows)
            {
                var target = Path.Combine(row.Folder, row.Proposed);

                // Whether the target is a different file or this one under
                // another spelling. Windows matches filenames without
                // regard to case, so lowercasing a name finds itself:
                // every rename that only changes capitalisation was being
                // reported as a collision with the file being renamed.
                var itself = string.Equals(row.Path, target, StringComparison.OrdinalIgnoreCase);

                try
                {
                    if (!itself && File.Exists(target))
                    {
                        failures.Add($"{row.Original} — {row.Proposed} already exists");
                        continue;
                    }

                    if (string.Equals(row.Path, target, StringComparison.Ordinal))
                    {
                        done++;
                        continue;
                    }

                    if (itself) ChangeCase(row.Path, target);
                    else File.Move(row.Path, target);

                    done++;
                }
                catch (Exception ex)
                {
                    failures.Add($"{row.Original} — {ex.Message}");
                }
            }
        });

        // Marked rather than removed. The row keeps the old name beside
        // the new one and turns green, so the list reads as a record of
        // what was done instead of emptying itself as it goes.
        foreach (var row in rows)
            if (!failures.Any(f => f.StartsWith(row.Original + " —", StringComparison.Ordinal)))
                row.Done = true;

        Recheck();
        Report(done, failures);
    }

    /// <summary>
    /// Puts the outcome in the window, under the list it refers to.
    /// </summary>
    /// <remarks>
    /// Rather than in a box over it. The interesting part of a rename is
    /// which files were left alone and why, and that is worth reading
    /// beside the list rather than through a dialog that has to be
    /// dismissed before the list can be seen again.
    /// </remarks>
    private void Report(int done, IReadOnlyList<string> failures)
    {
        ResultHeadline.Text = failures.Count == 0
            ? $"{Count(done)} renamed."
            : $"{Count(done)} renamed, {failures.Count} left alone:";

        ResultHeadline.SetResourceReference(TextBlock.ForegroundProperty,
            failures.Count == 0 ? "StatusOk" : "StatusWarn");

        ResultPanel.SetResourceReference(Border.BorderBrushProperty,
            failures.Count == 0 ? "StatusOk" : "StatusWarn");

        ResultList.ItemsSource = failures;
        ResultScroll.Visibility = failures.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
        ResultPanel.Visibility = Visibility.Visible;
    }

    private static string Count(int n) => n == 1 ? "1 file" : $"{n} files";

    private void DismissResult_Click(object sender, RoutedEventArgs e) =>
        ResultPanel.Visibility = Visibility.Collapsed;

    /// <summary>
    /// Stops a read that is still going when the window is closed.
    /// </summary>
    protected override void OnClosed(EventArgs e)
    {
        _running?.Cancel();
        _tick.Stop();
        base.OnClosed(e);
    }
}
