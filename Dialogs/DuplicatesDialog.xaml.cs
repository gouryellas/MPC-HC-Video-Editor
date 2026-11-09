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
    public DuplicateRow(DuplicateFile file, bool isLargest)
    {
        File = file;
        IsLargest = isLargest;
    }

    public DuplicateFile File { get; }

    public string Name => File.Name;
    public string Folder => File.Folder;
    public string Path => File.Path;
    public string Size => File.SizeDisplay;

    /// <summary>The copy a "keep the best" rule would keep.</summary>
    public bool IsLargest { get; }

    public Visibility LargestVisibility => IsLargest ? Visibility.Visible : Visibility.Collapsed;

    private bool _isChecked;

    /// <summary>
    /// Whether this file is marked for deletion. Nothing is checked when a
    /// scan finishes: the program found these, it did not decide about them.
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
        var largest = group.Files.OrderByDescending(f => f.Bytes).First();

        Files = new ObservableCollection<DuplicateRow>(
            group.Files.Select(f => new DuplicateRow(f, ReferenceEquals(f, largest))));

        Heading = group.Kind == DuplicateKind.Identical
            ? $"{group.Files.Count} identical files"
            : $"{group.Files.Count} files, same footage · {group.Confidence}% alike";

        // Certainty and likelihood should not look the same. An exact match
        // is a fact; a footage match is a suggestion, and is coloured as the
        // caution it is.
        HeadingBrush = group.Kind == DuplicateKind.Identical
            ? Brush("ValueGreen")
            : Brush("ValueYellow");

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
/// Nothing is checked when a scan finishes, and nothing is deleted without a
/// count being put to the user first. The program is good at finding
/// candidates and has no idea which copy anybody wants to keep.
/// </remarks>
public partial class DuplicatesDialog : Window
{
    private readonly DuplicateScanner _scanner;
    private readonly ObservableCollection<DuplicateGroupRow> _groups = new();
    private CancellationTokenSource? _running;
    private string? _folder;

    public DuplicatesDialog(FFmpegService ffmpeg, string? startIn = null)
    {
        InitializeComponent();

        _scanner = new DuplicateScanner(ffmpeg);
        GroupList.ItemsSource = _groups;

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
        ProgressArea.Visibility = Visibility.Visible;
        EmptyText.Visibility = Visibility.Collapsed;
        StageText.Text = "Listing files…";
        Bar.Value = 0;

        try
        {
            var recursive = RecurseCheck.IsChecked == true;
            var folder = _folder!;

            var files = await Task.Run(
                () => DuplicateScanner.FindVideos(folder, recursive, ct), ct);

            var progress = new Progress<DuplicateProgress>(p =>
            {
                StageText.Text = p.Total > 0
                    ? $"{p.Stage} — {p.Done} of {p.Total}"
                    : p.Stage;
                Bar.Value = p.Percent;
            });

            var found = new List<DuplicateGroup>();
            found.AddRange(await _scanner.FindIdenticalAsync(files, progress, ct));

            if (FootageCheck.IsChecked == true)
            {
                // Only what the exact pass did not already account for.
                // Fingerprinting a file already known to be a copy spends a
                // decode to learn nothing.
                var settled = found.SelectMany(g => g.Files).Select(f => f.Path)
                                   .ToHashSet(StringComparer.OrdinalIgnoreCase);

                var rest = files.Where(f => !settled.Contains(f)).ToList();
                found.AddRange(await _scanner.FindSameFootageAsync(rest, progress, ct));
            }

            foreach (var group in found) Add(group);

            EmptyText.Text = _groups.Count == 0
                ? $"No duplicates found among {files.Count} file(s)."
                : "";
            EmptyText.Visibility = _groups.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }
        catch (OperationCanceledException)
        {
            StageText.Text = "Stopped.";
            if (_groups.Count == 0)
            {
                EmptyText.Text = "Stopped before anything was found.";
                EmptyText.Visibility = Visibility.Visible;
            }
        }
        catch (Exception ex)
        {
            Diagnostics.Swallowed("DuplicatesDialog.Scan", ex);
            EmptyText.Text = "The scan could not finish: " + ex.Message;
            EmptyText.Visibility = Visibility.Visible;
        }
        finally
        {
            StopButton.IsEnabled = false;
            ProgressArea.Visibility = Visibility.Collapsed;
            Refresh();
        }
    }

    private void Add(DuplicateGroup group)
    {
        var row = new DuplicateGroupRow(group);
        foreach (var file in row.Files) file.CheckedChanged = Refresh;
        _groups.Add(row);
    }

    private void Stop_Click(object sender, RoutedEventArgs e) => _running?.Cancel();

    private void CheckAllButLargest_Click(object sender, RoutedEventArgs e)
    {
        foreach (var group in _groups)
            foreach (var file in group.Files)
                file.IsChecked = !file.IsLargest;

        Refresh();
    }

    private void ClearChecks_Click(object sender, RoutedEventArgs e)
    {
        foreach (var group in _groups)
            foreach (var file in group.Files)
                file.IsChecked = false;

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
