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

/// <summary>One file in a group, with its checkbox.</summary>
public sealed class DuplicateRow : INotifyPropertyChanged
{
    public DuplicateRow(DuplicateFile file) => File = file;

    public DuplicateFile File { get; }

    public string Name => File.Name;
    public string Folder => File.Folder;
    public string Path => File.Path;

    /// <summary>
    /// Size, and running time when it is known.
    /// </summary>
    /// <remarks>
    /// The length is shown because it is half the reason a near-identical
    /// group is a group at all — two files within a kilobyte of each other
    /// prove nothing on their own, and seeing the same duration twice is
    /// what makes the grouping obvious rather than mysterious. Blank for the
    /// exact pass, which never needs to ask.
    /// </remarks>
    public string Size => File.Seconds > 0
        ? $"{Models.Bookmark.FormatTime(File.Seconds)}   {File.SizeDisplay}"
        : File.SizeDisplay;

    private bool _isChecked;

    /// <summary>
    /// Whether this file is marked for deletion. Every copy but the first in
    /// a group is checked when a scan finishes, which is the answer somebody
    /// looking at a list of duplicates almost always wants — and the one
    /// that still leaves a copy of everything.
    /// </summary>
    public bool IsChecked
    {
        get => _isChecked;
        set
        {
            if (_isChecked == value) return;
            _isChecked = value;
            OnPropertyChanged();
            CheckedChanged?.Invoke();
        }
    }

    /// <summary>Raised so the window can keep its footer in step.</summary>
    public Action? CheckedChanged { get; set; }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

/// <summary>A group of matching files, as the list shows it.</summary>
public sealed class DuplicateGroupRow
{
    public DuplicateGroupRow(DuplicateGroup group)
    {
        // The scanner hands them over largest first, which is what makes the
        // default — keep the first, check the rest — keep the best copy
        // without having to say so on every row.
        Files = new ObservableCollection<DuplicateRow>(
            group.Files.Select(f => new DuplicateRow(f)));

        // Three headings and only three, keyed to the two outside facts:
        // size on disk and running time. Both agreeing — the exact pass's
        // byte-equal groups among them, since equal bytes cannot differ
        // in either — is "identical"; otherwise the heading names the one
        // that agrees. A group the exact pass made was never timed, and
        // equal bytes answer for the length by themselves.
        var count = group.Files.Count;
        var sizes = group.Files.Max(f => f.Bytes) - group.Files.Min(f => f.Bytes)
                    <= Math.Max(4096, group.Files.Max(f => f.Bytes) / 1000);
        var timed = group.Files.All(f => f.Seconds > 0);
        var lengths = timed
                      && group.Files.Max(f => f.Seconds) - group.Files.Min(f => f.Seconds) <= 2.0;

        Heading = sizes && (lengths || !timed)
            ? $"{count} identical files"
            : sizes
                ? $"{count} files size-matched, but the length differs"
                : $"{count} files length-matched, but size differs";

        // Certainty and likelihood should not look the same. The first two
        // are facts about the files; a footage match is a judgement from
        // sampled frames, and is coloured as the caution it is.
        HeadingBrush = group.Kind == DuplicateKind.SameFootage
            ? Brush("ValueYellow")
            : Brush("ValueGreen");

        Recoverable = $"{group.RecoverableBytes / 1024.0 / 1024:0.#} MB recoverable";
    }

    public ObservableCollection<DuplicateRow> Files { get; }
    public string Heading { get; }
    public Brush HeadingBrush { get; }
    public string Recoverable { get; }

    private static Brush Brush(string key) =>
        Application.Current?.TryFindResource(key) as Brush ?? Brushes.Gray;
}

/// <summary>
/// Finds files in a folder that are copies of each other, and offers to be
/// rid of the ones you pick.
/// </summary>
/// <remarks>
/// Every copy but the first in a group is checked when a scan finishes, so
/// pressing Delete once leaves exactly one of everything. Nothing is deleted
/// without a count being put to the user first.
/// </remarks>
public partial class DuplicatesDialog : Window
{
    private readonly DuplicateScanner _scanner;
    private readonly ObservableCollection<DuplicateGroupRow> _groups = new();
    private CancellationTokenSource? _running;
    private string? _folder;

    /// <summary>Drives the moving dots and the elapsed time.</summary>
    private readonly System.Windows.Threading.DispatcherTimer _tick = new()
    {
        Interval = TimeSpan.FromMilliseconds(450)
    };

    private readonly System.Diagnostics.Stopwatch _clock = new();
    private int _dots;

    public DuplicatesDialog(FFmpegService ffmpeg, string? startIn = null)
    {
        InitializeComponent();

        _scanner = new DuplicateScanner(ffmpeg);
        GroupList.ItemsSource = _groups;

        _tick.Tick += (_, _) => ShowWorking();

        if (!string.IsNullOrWhiteSpace(startIn) && Directory.Exists(startIn))
            SetFolder(startIn!);
    }

    private void SetFolder(string folder)
    {
        _folder = folder;
        FolderText.Text = folder;
    }

    private void ChooseFolder_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFolderDialog { Title = "Folder to scan for duplicates" };
        if (!string.IsNullOrEmpty(_folder)) dlg.InitialDirectory = _folder;

        if (dlg.ShowDialog(this) == true) SetFolder(dlg.FolderName);
    }

    /// <summary>Whether a scan is going, which is what the button means.</summary>
    private bool _scanning;

    /// <summary>What the last finished scan found, for the summary line.</summary>
    private int _foundCopies;
    private string _tookTime = "";
    private bool _finished;

    private async void Scan_Click(object sender, RoutedEventArgs e)
    {
        // The same button, mid-scan, is the stop button.
        if (_scanning)
        {
            _running?.Cancel();
            return;
        }

        if (string.IsNullOrWhiteSpace(_folder) || !Directory.Exists(_folder))
        {
            EmptyText.Text = "Choose a folder first.";
            EmptyText.Visibility = Visibility.Visible;
            return;
        }

        _running?.Cancel();
        _running = new CancellationTokenSource();
        var ct = _running.Token;

        _groups.Clear();
        _finished = false;
        Refresh();

        ShowScanning(true);
        EmptyText.Visibility = Visibility.Collapsed;

        ProgressPanel.Visibility = Visibility.Visible;
        _progress = null;
        _fileTotal = 0;
        _dots = 0;
        StartPass(1);
        _clock.Restart();
        _tick.Start();
        ShowWorking();

        // Held and painted on the timer's beat rather than on every
        // report: the scanner reports once per file, and a label that
        // rewrites itself hundreds of times a second is unreadable where
        // one that ticks twice a second is a count going up.
        //
        // A pass can report under more than one stage name, and a change
        // of name is a step of its own — so the step number is the pass's
        // base plus however many names have gone by inside it.
        var progress = new Progress<DuplicateProgress>(p =>
        {
            if (_lastStage is not null && p.Stage != _lastStage) _passOffset++;
            _lastStage = p.Stage;
            _progress = p;
        });

        try
        {
            var recursive = RecurseCheck.IsChecked == true;
            var videosOnly = VideosCheck.IsChecked == true;
            var folder = _folder!;

            var files = await Task.Run(
                () => DuplicateScanner.FindVideos(folder, recursive, ct, videosOnly), ct);

            _fileTotal = files.Count;

            var found = new List<DuplicateGroup>();

            // Narrowing, pass by pass. Each one only looks at what the ones
            // before it could not account for — a file already known to be a
            // copy is not worth probing, let alone decoding.
            // Each pass's first step number. The exact pass reports under
            // one name, the near pass under two, the deep pass under two
            // — five in all, three when the deep one is not asked for.
            StartPass(1);
            found.AddRange(await _scanner.FindIdenticalAsync(files, progress, ct));

            StartPass(2);
            found.AddRange(await _scanner.FindNearIdenticalAsync(Rest(files, found), progress, ct));

            if (FootageCheck.IsChecked == true)
            {
                StartPass(4);
                found.AddRange(await _scanner.FindSameFootageAsync(Rest(files, found), progress, ct));
            }

            foreach (var group in found) Add(group);

            var copies = found.Sum(g => g.Files.Count);

            if (copies == 0)
            {
                Say("", "Finished! Didn't find any.", Elapsed);
            }
            else
            {
                // Everything the line used to say in two places, said
                // once — and kept in step with the selection from here
                // on by Refresh.
                _foundCopies = copies;
                _tookTime = Elapsed;
                _finished = true;
            }

            EmptyText.Text = $"No duplicates found among {files.Count} file(s).";
            EmptyText.Visibility = _groups.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }
        catch (OperationCanceledException)
        {
            Say("", "Stopped.", Elapsed);

            if (_groups.Count == 0)
            {
                EmptyText.Text = "Stopped before anything was found.";
                EmptyText.Visibility = Visibility.Visible;
            }
        }
        catch (Exception ex)
        {
            Diagnostics.Swallowed("DuplicatesDialog.Scan", ex);
            Say("", "The scan could not finish.");
            EmptyText.Text = "The scan could not finish: " + ex.Message;
            EmptyText.Visibility = Visibility.Visible;
        }
        finally
        {
            _tick.Stop();
            _clock.Stop();
            ShowScanning(false);
            Refresh();
        }
    }

    /// <summary>
    /// Turns the one button into whichever of its two jobs applies.
    /// </summary>
    /// <remarks>
    /// Red while it would stop something, the same red the rename
    /// window's Run carries, and back to the theme's own colour when it
    /// would start one.
    /// </remarks>
    private void ShowScanning(bool scanning)
    {
        _scanning = scanning;
        ScanButton.Content = scanning ? "Stop" : "Scan";

        if (scanning)
        {
            ScanButton.Background = new SolidColorBrush(Color.FromRgb(0xB3, 0x31, 0x2C));
            ScanButton.BorderBrush = new SolidColorBrush(Color.FromRgb(0x8E, 0x26, 0x22));
            ScanButton.Foreground = System.Windows.Media.Brushes.White;

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

    /// <summary>What no pass has accounted for yet.</summary>
    private static IReadOnlyList<string> Rest(IReadOnlyList<string> all, List<DuplicateGroup> found)
    {
        var settled = found.SelectMany(g => g.Files).Select(f => f.Path)
                           .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return all.Where(f => !settled.Contains(f)).ToList();
    }

    /// <summary>
    /// The scan's own clock, written the way the status bar writes one.
    /// </summary>
    private string Elapsed => JobProgress.FormatElapsed(_clock.Elapsed);

    /// <summary>The scanner's latest word on where it is.</summary>
    private DuplicateProgress? _progress;

    /// <summary>How many files the walk found.</summary>
    private int _fileTotal;

    /// <summary>Where the pass under way starts counting its steps.</summary>
    private int _stepBase;

    /// <summary>How many stage names have gone by inside this pass.</summary>
    private int _passOffset;

    private string? _lastStage;

    /// <summary>Opens a pass at its own step number.</summary>
    private void StartPass(int step)
    {
        _stepBase = step;
        _passOffset = 0;
        _lastStage = null;
    }

    /// <summary>
    /// How many steps a scan has, which is what the deep pass adds to.
    /// </summary>
    private int Steps => FootageCheck.IsChecked == true ? 5 : 3;

    /// <summary>
    /// What the scan has to say for itself right now: the step under way
    /// and how many of the folder's files are accounted for, or moving
    /// dots while it has nothing countable to report yet.
    /// </summary>
    /// <remarks>
    /// The step number and that step's own count, and nothing derived
    /// from the folder's total. A count that restarts is a new step, and
    /// saying which step it is explains the restart; subtracting the
    /// step's figures from the folder's would not, because the steps
    /// overlap and do not partition it.
    ///
    /// The stage name goes in its own fixed slot, so the dots can change
    /// width without moving the figures beside them.
    /// </remarks>
    private void ShowWorking()
    {
        // Three dots down to none, then round again.
        var dots = new string('.', 3 - _dots % 4);
        _dots++;

        if (_progress is { Total: > 0 } p)
        {
            var step = Math.Min(_stepBase + _passOffset, Steps);

            Say($"{p.Stage}{dots}",
                $"Step {step}/{Steps} - {p.Done:N0} of {p.Total:N0}", Elapsed);
            return;
        }

        var what = FootageCheck.IsChecked == true ? "Deep scanning" : "Scanning";
        Say($"{what}{dots}", "", Elapsed);
    }

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

    private void Add(DuplicateGroup group)
    {
        var row = new DuplicateGroupRow(group);
        foreach (var file in row.Files) file.CheckedChanged = Refresh;

        _groups.Add(row);
        CheckDuplicates(row);
    }

    /// <summary>
    /// Selects every copy in a group but the first, so acting on the list
    /// leaves exactly one of everything.
    /// </summary>
    private static void CheckDuplicates(DuplicateGroupRow group)
    {
        for (int i = 0; i < group.Files.Count; i++)
            group.Files[i].IsChecked = i > 0;
    }

    private void SelectAll_Click(object sender, RoutedEventArgs e)
    {
        foreach (var file in _groups.SelectMany(g => g.Files)) file.IsChecked = true;
        Refresh();
    }

    private void SelectNone_Click(object sender, RoutedEventArgs e)
    {
        foreach (var file in _groups.SelectMany(g => g.Files)) file.IsChecked = false;
        Refresh();
    }

    private void SelectDuplicates_Click(object sender, RoutedEventArgs e)
    {
        foreach (var group in _groups) CheckDuplicates(group);
        Refresh();
    }

    private IReadOnlyList<DuplicateRow> Checked =>
        _groups.SelectMany(g => g.Files).Where(f => f.IsChecked).ToList();

    private void Refresh()
    {
        var selected = Checked;
        var bytes = selected.Sum(f => f.File.Bytes);

        DeleteButton.IsEnabled = selected.Count > 0;
        ShowDeletable(selected.Count > 0);

        // The one line that says it all, kept current as the selection
        // changes rather than split between a header and a footer.
        if (_finished)
        {
            Say("",
                $"Finished! Found {_foundCopies} file(s) separated into " +
                $"{_groups.Count} group(s).   {selected.Count} selected" +
                (selected.Count > 0 ? $" - {bytes / 1024.0 / 1024:0.#} MB" : ""),
                _tookTime);
        }
    }

    /// <summary>
    /// Paints the delete button red once it has something to delete.
    /// </summary>
    /// <remarks>
    /// The red is a warning, and a warning about nothing is noise — with
    /// an empty list, or a scanned list with every box clear, there is
    /// nothing for it to be a warning about. This is a state the button
    /// is in rather than the usual "unavailable" dimming: the colour says
    /// what pressing it would cost, so it arrives when the cost does.
    /// </remarks>
    private void ShowDeletable(bool any)
    {
        if (any)
        {
            DeleteButton.Background = new SolidColorBrush(Color.FromRgb(0xB3, 0x31, 0x2C));
            DeleteButton.BorderBrush = new SolidColorBrush(Color.FromRgb(0x8E, 0x26, 0x22));
            DeleteButton.Foreground = Brushes.White;
            DeleteButton.Tag = new SolidColorBrush(Color.FromRgb(0xC9, 0x3A, 0x34));
            return;
        }

        // Back to resource references rather than brushes frozen out of
        // whichever theme was current when this last ran.
        DeleteButton.SetResourceReference(BackgroundProperty, "ControlBackground");
        DeleteButton.SetResourceReference(BorderBrushProperty, "BorderBrush");
        DeleteButton.SetResourceReference(ForegroundProperty, "TextPrimary");
        DeleteButton.SetResourceReference(TagProperty, "ControlHover");
    }

    /// <summary>
    /// Deletes what is checked, after saying how much and whether it can be
    /// undone.
    /// </summary>
    /// <remarks>
    /// Through RecycleBin, which already honours the program's own
    /// "delete permanently" setting — so this cannot be the one place that
    /// quietly bypasses it.
    ///
    /// A group emptied entirely is still shown, with its rows gone. Removing
    /// it would make the list jump about while it is being read.
    /// </remarks>
    private async void Delete_Click(object sender, RoutedEventArgs e)
    {
        var rows = Checked;
        if (rows.Count == 0) return;

        var bytes = rows.Sum(f => f.File.Bytes);
        var fate = RecycleBin.SendToBin
            ? "They go to the Recycle Bin and can be put back."
            : "They are deleted permanently and cannot be recovered.";

        // Named outright, because a group holding only copies of each other
        // is exactly where checking every row is an easy mistake.
        var whole = _groups.Count(g => g.Files.All(f => f.IsChecked));
        var warning = whole > 0
            ? $"\n\n{whole} group(s) would have every copy removed, leaving none."
            : "";

        var answer = MessageBox.Show(this,
            $"Delete {rows.Count} file(s), freeing {bytes / 1024.0 / 1024:0.#} MB?\n\n{fate}{warning}",
            "Delete duplicates", MessageBoxButton.YesNo, MessageBoxImage.Warning,
            MessageBoxResult.No);

        if (answer != MessageBoxResult.Yes) return;

        DeleteButton.IsEnabled = false;
        var failures = new List<string>();

        foreach (var row in rows)
        {
            var (ok, error) = await RecycleBin.TryDeleteAsync(row.Path);

            if (ok)
            {
                foreach (var group in _groups) group.Files.Remove(row);
            }
            else
            {
                row.IsChecked = false;
                failures.Add($"{row.Name} — {error}");
            }
        }

        Refresh();

        if (failures.Count > 0)
            MessageBox.Show(this,
                "These could not be deleted:\n\n" + string.Join("\n", failures.Take(10)),
                "Delete duplicates", MessageBoxButton.OK, MessageBoxImage.Warning);

        if (_groups.All(g => g.Files.Count == 0))
        {
            EmptyText.Text = "Done. Scan again to check what is left.";
            EmptyText.Visibility = Visibility.Visible;
        }
    }

    private void Play_Click(object sender, RoutedEventArgs e)
    {
        var path = (string?)((Button)sender).Tag;
        if (!string.IsNullOrWhiteSpace(path))
        {
            try { System.Diagnostics.Process.Start(path); }
            catch (Exception ex) { Diagnostics.Swallowed("DuplicatesDialog.Play", ex); }
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        _running?.Cancel();
        base.OnClosed(e);
    }
}
