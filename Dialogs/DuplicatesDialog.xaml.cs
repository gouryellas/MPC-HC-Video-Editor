using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
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

        Heading = group.Kind switch
        {
            DuplicateKind.Identical => $"{group.Files.Count} identical files",
            DuplicateKind.NearIdentical =>
                $"{group.Files.Count} files, same length and all but the same size",
            _ => $"{group.Files.Count} files, same footage · {group.Confidence}% alike"
        };

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

    private async void Scan_Click(object sender, RoutedEventArgs e)
    {
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
        Refresh();

        StopButton.IsEnabled = true;
        EmptyText.Visibility = Visibility.Collapsed;

        StageText.Visibility = Visibility.Visible;
        _dots = 0;
        _clock.Restart();
        _tick.Start();
        ShowWorking();

        try
        {
            var recursive = RecurseCheck.IsChecked == true;
            var folder = _folder!;

            var files = await Task.Run(
                () => DuplicateScanner.FindVideos(folder, recursive, ct), ct);

            var found = new List<DuplicateGroup>();

            // Narrowing, pass by pass. Each one only looks at what the ones
            // before it could not account for — a file already known to be a
            // copy is not worth probing, let alone decoding.
            found.AddRange(await _scanner.FindIdenticalAsync(files, null, ct));
            found.AddRange(await _scanner.FindNearIdenticalAsync(Rest(files, found), null, ct));

            if (FootageCheck.IsChecked == true)
                found.AddRange(await _scanner.FindSameFootageAsync(Rest(files, found), null, ct));

            foreach (var group in found) Add(group);

            var copies = found.Sum(g => g.Files.Count);

            StageText.Text = copies == 0
                ? "Finished! Didn't find any."
                : $"Finished! Found {copies} file(s).   {Elapsed}";

            EmptyText.Text = $"No duplicates found among {files.Count} file(s).";
            EmptyText.Visibility = _groups.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }
        catch (OperationCanceledException)
        {
            StageText.Text = $"Stopped.   {Elapsed}";

            if (_groups.Count == 0)
            {
                EmptyText.Text = "Stopped before anything was found.";
                EmptyText.Visibility = Visibility.Visible;
            }
        }
        catch (Exception ex)
        {
            Diagnostics.Swallowed("DuplicatesDialog.Scan", ex);
            StageText.Text = "The scan could not finish.";
            EmptyText.Text = "The scan could not finish: " + ex.Message;
            EmptyText.Visibility = Visibility.Visible;
        }
        finally
        {
            _tick.Stop();
            _clock.Stop();
            StopButton.IsEnabled = false;
            Refresh();
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

    /// <summary>
    /// "Scanning..." with the dots counting down and back, so a long scan is
    /// visibly still going without claiming a percentage it does not have.
    /// </summary>
    private void ShowWorking()
    {
        var what = FootageCheck.IsChecked == true ? "Deep scanning" : "Scanning";

        // Three dots down to none, then round again. Padded so the line does
        // not change width and shuffle the text beside it.
        var dots = new string('.', 3 - _dots % 4).PadRight(3);
        _dots++;

        StageText.Text = $"{what}{dots}   {Elapsed}";
    }

    private void Add(DuplicateGroup group)
    {
        var row = new DuplicateGroupRow(group);
        foreach (var file in row.Files) file.CheckedChanged = Refresh;

        _groups.Add(row);
        CheckDuplicates(row);
    }

    private void Stop_Click(object sender, RoutedEventArgs e) => _running?.Cancel();

    /// <summary>
    /// Checks every copy in a group but the first, so acting on the list
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
        var checkedRows = Checked;
        var bytes = checkedRows.Sum(f => f.File.Bytes);

        DeleteButton.IsEnabled = checkedRows.Count > 0;

        SummaryText.Text = _groups.Count == 0
            ? ""
            : $"{_groups.Count} group(s) · {checkedRows.Count} checked" +
              (checkedRows.Count > 0 ? $" · {bytes / 1024.0 / 1024:0.#} MB" : "");
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

    protected override void OnClosed(EventArgs e)
    {
        _running?.Cancel();
        base.OnClosed(e);
    }
}
