using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;

namespace MpcHcVideoEditor.Dialogs;

/// <summary>
/// Where a batch of files should be written: beside each source, in the
/// program's Save to folder, or in a folder chosen on the spot.
/// </summary>
/// <remarks>
/// A control rather than three radio buttons copied into each dialog, because
/// the question is the same wherever it is asked and the answer has to mean the
/// same thing — an empty string being "beside each source" is a convention the
/// callers should not each have to know.
/// </remarks>
public partial class OutputFolderPicker : UserControl
{
    /// <summary>Raised when the answer changes, so a host can re-check it.</summary>
    public event Action? ChoiceChanged;

    private string _saveTo = string.Empty;
    private string _chosen = string.Empty;

    public OutputFolderPicker()
    {
        InitializeComponent();
        OtherNote.Text = "No folder chosen yet.";
    }

    /// <summary>
    /// The program's current Save to folder. Empty disables that option — an
    /// offer to write somewhere unset is an offer to write nowhere.
    /// </summary>
    public string SaveTo
    {
        get => _saveTo;
        set
        {
            _saveTo = value ?? string.Empty;

            var set = !string.IsNullOrWhiteSpace(_saveTo);
            SaveToFolder.IsEnabled = set;
            SaveToNote.Text = set ? _saveTo : "Not set — pick one under File ▸ Save to folder.";

            if (!set && SaveToFolder.IsChecked == true)
                BesideSource.IsChecked = true;
        }
    }

    /// <summary>
    /// The folder to write into, or an empty string meaning "beside each
    /// source" — which is what the output-path helpers already take as "work it
    /// out from the file".
    /// </summary>
    public string Directory =>
        SaveToFolder.IsChecked == true ? _saveTo
        : OtherFolder.IsChecked == true ? _chosen
        : string.Empty;

    /// <summary>
    /// False only while "Somewhere else" is selected with nothing chosen yet.
    /// </summary>
    public bool IsReady => OtherFolder.IsChecked != true || _chosen.Length > 0;

    /// <summary>Preselects a previous answer. Empty means beside each source.</summary>
    public void Preselect(string? directory)
    {
        if (string.IsNullOrWhiteSpace(directory))
        {
            BesideSource.IsChecked = true;
            return;
        }

        if (string.Equals(directory, _saveTo, StringComparison.OrdinalIgnoreCase)
            && SaveToFolder.IsEnabled)
        {
            SaveToFolder.IsChecked = true;
            return;
        }

        _chosen = directory;
        OtherNote.Text = directory;
        OtherFolder.IsChecked = true;
    }

    private void Browse_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFolderDialog { Title = "Where should the files be written?" };

        if (_chosen.Length > 0 && System.IO.Directory.Exists(_chosen))
            dlg.InitialDirectory = _chosen;

        if (dlg.ShowDialog() != true) return;

        _chosen = dlg.FolderName;
        OtherNote.Text = _chosen;

        // Browsing is the answer, so it selects the option it belongs to rather
        // than leaving a chosen folder sitting next to an unselected button.
        OtherFolder.IsChecked = true;
        ChoiceChanged?.Invoke();
    }

    private void Choice_Changed(object sender, RoutedEventArgs e) => ChoiceChanged?.Invoke();

    /// <summary>
    /// The folder a summary should name, given where a run actually wrote.
    /// </summary>
    /// <param name="writtenFiles">The outputs, for the beside-each-source case
    /// where there is no one folder decided in advance. A run that happened to
    /// touch only one folder still names it; one that spanned several says so,
    /// since a single path would be a half-truth.</param>
    public static string Describe(string directory, IEnumerable<string> writtenFiles)
    {
        if (!string.IsNullOrWhiteSpace(directory)) return directory;

        var folders = writtenFiles
            .Select(f => Path.GetDirectoryName(f) ?? string.Empty)
            .Where(f => f.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return folders.Count == 1 ? folders[0] : "their own folders";
    }
}
